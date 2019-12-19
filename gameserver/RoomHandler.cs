using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace GameServerMono
{

    class RoomHandler
    {
        int ticksPerSecond = 50;
        volatile bool isRunning;
        double nextTime10;
        double nextTime1;
        Thread thread;
        NetClient clientToMS;
        NetServer server;

        public List<RoomData> rooms = new List<RoomData>();

        public RoomHandler(NetServer server, NetClient clientToMS)
        {
            this.server = server;
            this.clientToMS = clientToMS;
            isRunning = true;
            thread = new Thread(new ThreadStart(RunGameLoop));
            thread.Start();
            Thread.Sleep(500);
        }

        public void StopGameLoop()
        {
            isRunning = false;
            if (thread != null)
                thread.Join();
        }

        void RunGameLoop()
        {

            int sendRate = 1000 / ticksPerSecond; // 1 sec = 1000ms as Sleep uses ms.
            for (isRunning = true; isRunning; Thread.Sleep(sendRate))
            {

                for (int i = 0; i < rooms.Count; i++)
                    rooms[i].GameLoop(i);

                if (NetTime.Now > nextTime10)
                {
                    CreateAndRemovePublicRooms(true);
                    CreateAndRemovePublicRooms(false);
                    SendRoomDataToMasterserver();
                    nextTime10 = NetTime.Now + 10;
                    TimeoutUsers();
                }

                if (NetTime.Now > nextTime1)
                {
                    TimeoutChallenges();
                    AutostartPublic();
                    AutostartOfficialLeagueMatch();
                    nextTime1 = NetTime.Now + 1;
                }


            }
        }

        int GetUniqueID()
        {
            int uniqueID = 0;

            while (true)
            {
                bool isInvidualID = true;

                uniqueID++;

                for (int i = 0; i < rooms.Count; i++)
                    if (rooms[i].uniqueID == uniqueID)
                    {
                        isInvidualID = false;
                        break;
                    }

                if (isInvidualID) return uniqueID;
            }
        }

        void TimeoutChallenges()
        {
            bool isEmptyRoom;
            int removeID = -1;

            //calculate timeout for empty challenges
            lock (rooms)
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (rooms[i].roomType == RoomType.Public) continue;

                    isEmptyRoom = true;

                    for (int j = 0; j < 2; j++)
                        for (int k = 0; k < rooms[i].maxPlayers; k++)
                            if (rooms[i].IsPlayerOnline(j, k))
                                isEmptyRoom = false;

                    if (isEmptyRoom)
                        rooms[i].timeout++;
                    else
                        rooms[i].timeout = 0;
                }
            }

            //remove challenge, if timeout 60 (autostarted league matches 120)
            lock (rooms)
            {
                for (int i = rooms.Count - 1; i >= 0; --i)
                {
                    if (rooms[i].roomType == RoomType.Public) continue;
                    if (!rooms[i].officiallyStarted && rooms[i].timeout < 60) continue;
                    if (rooms[i].officiallyStarted && rooms[i].timeout < 120) continue;

                    rooms[i].InformSpectatorsAboutServerClose();
                    if (rooms[i].officiallyStarted)
                        rooms[i].InformAboutCanceledLeagueMatch();

                    rooms.RemoveAt(i);
                }
            }

        }

        void TimeoutUsers()
        {

            lock (rooms)
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (rooms[i].roomState == RoomState.ReadyScreen) continue;

                    for (int j = 0; j < 2; j++)
                        for (int k = 0; k < rooms[i].maxPlayers; k++)
                            if (rooms[i].IsPlayerOnline(j, k))
                                if (NetTime.Now > rooms[i].users[j, k].timeout + 10)
                                {
                                    Console.WriteLine("timeout");
                                    DisconnectUser(rooms[i].users[j, k].connection, 3);
                                }
                    //rooms[i].DisconnectUser(j, k, 3);



                }
            }
        }

        void AutostartPublic()
        {
            bool isEmptyRoom;

            lock (rooms)
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (rooms[i].roomType != RoomType.Public) continue;
                    if (rooms[i].roomState != RoomState.ReadyScreen) continue;

                    isEmptyRoom = true;

                    for (int j = 0; j < 2; j++)
                        for (int k = 0; k < rooms[i].maxPlayers; k++)
                            if (rooms[i].IsPlayerOnline(j, k))
                                isEmptyRoom = false;

                    if (isEmptyRoom)
                        rooms[i].timeout = 0;
                    else
                        rooms[i].timeout++;

                    if (rooms[i].timeout > 30)
                    {
                        rooms[i].timeout = 0;
                        rooms[i].StartGame();
                    }
                }
            }
        }

        void AutostartOfficialLeagueMatch()
        {

            lock (rooms)
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (rooms[i].roomType == RoomType.Public) continue;
                    if (!rooms[i].officiallyStarted) continue;
                    if (rooms[i].roomState != RoomState.ReadyScreen) continue;

                    rooms[i].autostartTimeout--;

                    if (rooms[i].autostartTimeout < 0)
                    {
                        rooms[i].autostartTimeout = 300;
                        rooms[i].StartGame();
                    }

                }
            }
        }

        void CreateAndRemovePublicRooms(bool botsEnabled)
        {
            int emptyRooms = 0;
            int populatedServers = 0;
            bool isEmptyRoom;

            lock (rooms)
            {
                populatedServers = rooms.Count;

                for (int i = 0; i < rooms.Count; i++)
                {
                    if (rooms[i].roomType == RoomType.Challenge) continue;
                    if (rooms[i].botsEnabled != botsEnabled) continue;

                    isEmptyRoom = true;

                    for (int j = 0; j < 2; j++)
                        for (int k = 0; k < rooms[i].maxPlayers; k++)
                            if (rooms[i].IsPlayerOnline(j, k))
                                isEmptyRoom = false;

                    if (isEmptyRoom)
                        emptyRooms++;
                }
            }

            if (emptyRooms == 0 && populatedServers < 2)
                CreateRoom(botsEnabled);

            if (emptyRooms > 1)
                RemoveRoom(botsEnabled);

        }

        void CreateRoom(bool botsEnabled)
        {
            NationTeam[] nationTeam = new NationTeam[2];
            nationTeam[0] = GetNationData(-1);

            while (true)
            {
                nationTeam[1] = GetNationData(nationTeam[0].nID);
                if (nationTeam[1].nID > -1 && nationTeam[0].shirtType != nationTeam[1].shirtType)
                    break;
            }


            TeamData[] teams = new TeamData[2];

            for (int i = 0; i < 2; i++)
            {
                teams[i] = new TeamData(false, -1, nationTeam[i].name, nationTeam[i].shortName, nationTeam[i].shirtStyle, nationTeam[i].shirtColors);
            }

            lock (rooms)
            {
                rooms.Add(new RoomData(false, 0, server, clientToMS, RoomType.Public, GetUniqueID(), teams[0], teams[1], botsEnabled, 6));
            }
        }

        public void CreateChallengeRoom(bool officiallyStarted, int fixtureID, TeamData teams0, TeamData teams1, bool botsEnabled, byte maxPlayers)
        {
            lock (rooms)
            {
                rooms.Add(new RoomData(officiallyStarted, fixtureID, server, clientToMS, RoomType.Challenge, GetUniqueID(), teams0, teams1, botsEnabled, maxPlayers));
            }
        }

        void RemoveRoom(bool botsEnabled)
        {
            bool isEmptyRoom;
            int removeID = -1;

            lock (rooms)
            {
                for (int i = rooms.Count - 1; i >= 0; --i)
                {
                    if (rooms[i].roomType == RoomType.Challenge) continue;
                    if (rooms[i].botsEnabled != botsEnabled) continue;

                    isEmptyRoom = true;

                    for (int j = 0; j < 2; j++)
                        for (int k = 0; k < rooms[i].maxPlayers; k++)
                            if (rooms[i].IsPlayerOnline(j, k))
                                isEmptyRoom = false;

                    if (isEmptyRoom)
                    {
                        removeID = i;
                        break;
                    }
                }

                if (removeID > -1)
                {
                    rooms[removeID].InformSpectatorsAboutServerClose();
                    rooms.RemoveAt(removeID);
                }

            }
        }

        void SendRoomDataToMasterserver()
        {
            if (rooms.Count < 1) return;

            int plrCount = 0;

            NetOutgoingMessage outmsg = clientToMS.CreateMessage();
            outmsg.Write((byte)8);

            outmsg.Write(Form1.servername);
            outmsg.Write(rooms.Count); //total room count

            for (int u = 0; u < rooms.Count; u++)
            {
                outmsg.Write(rooms[u].uniqueID);
                outmsg.Write(rooms[u].maxPlayers);
                outmsg.Write((byte)rooms[u].roomType);
                outmsg.Write(rooms[u].time);
                outmsg.Write((byte)rooms[u].roomState);
                outmsg.Write(rooms[u].botsEnabled);
                outmsg.Write(rooms[u].officiallyStarted);

                for (int i = 0; i < 2; i++)
                {
                    outmsg.Write(rooms[u].teams[i].tID);
                    outmsg.Write(rooms[u].teams[i].name);
                    outmsg.Write(rooms[u].teams[i].score);
                }

                //pID's
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < rooms[u].maxPlayers; j++)
                    {
                        outmsg.Write(rooms[u].users[i, j].pID);
                        outmsg.Write(rooms[u].users[i, j].tID);

                        if (rooms[u].IsPlayerOnline(i, j)) plrCount++;
                    }

                //spectators
                lock (rooms[u].spectatorData2)
                {
                    outmsg.Write(rooms[u].spectatorData2.Count);

                    for (int i = 0; i < rooms[u].spectatorData2.Count; i++)
                    {
                        outmsg.Write(rooms[u].spectatorData2[i].pID);
                        outmsg.Write(rooms[u].spectatorData2[i].tID);
                        plrCount++;
                    }
                }

            }

            outmsg.Write(plrCount); //NOTE!!! this value contains player count (also spectators) in whole server (so it maybe in example 165)

            clientToMS.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);

        }

        public RoomData FindRoomByUniqueID(int uniqueID)
        {
            return rooms.Find(p => p.uniqueID == uniqueID);
        }

        public void DisconnectUser(NetConnection conn, byte disconnectType)  //disconnectType: 2=left from server, 3=kicked from server
        {

            for (int u = 0; u < rooms.Count; u++)
            {
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < rooms[u].maxPlayers; j++)
                        if (rooms[u].users[i, j].connection == conn)
                        {
                            rooms[u].DisconnectUser(i, j, disconnectType);
                            return;
                        }

                lock (rooms[u].spectatorData2)
                    rooms[u].spectatorData2.RemoveAll(p => p.connection == conn);



            }
        }

        NationTeam GetNationData(int nationToSkip)
        {
            NationTeam result = new NationTeam();

            int i = F.rand.Next(1, 23);   //1-22

            if (nationToSkip > -1)
                if (i == nationToSkip)
                {
                    result.nID = -1;
                    return result;
                }

            #region nations
            /*
            Argentina
            Australia
            Brazil
            Chile
            England
            Finland
            France
            Germany
            Ghana
            Greece
            Holland
            Italy
            Ivory coast
            Japan
            Norway
            Poland
            Portugal
            Russia
            Spain
            Sweden
            Uruguay
            USA
            */

            if (i == 1)
            {
                result.name = "Argentina";
                result.shortName = "ARG";
                result.shirtStyle = 1;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 106;
                result.shirtColors[0, 1] = 181;
                result.shirtColors[0, 2] = 255;

                result.shirtColors[1, 0] = 255;
                result.shirtColors[1, 1] = 255;
                result.shirtColors[1, 2] = 255;

                result.shirtColors[2, 0] = 0;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 0;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 255;
            }

            if (i == 2)
            {
                result.name = "Brazil";
                result.shortName = "BRA";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 218;
                result.shirtColors[0, 2] = 0;

                result.shirtColors[2, 0] = 0;
                result.shirtColors[2, 1] = 96;
                result.shirtColors[2, 2] = 191;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 255;
            }

            if (i == 3)
            {
                result.name = "Finland";
                result.shortName = "FIN";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 255;
                result.shirtColors[0, 2] = 255;

                result.shirtColors[2, 0] = 0;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 255;
            }

            if (i == 4)
            {
                result.name = "Sweden";
                result.shortName = "SWE";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 255;
                result.shirtColors[0, 2] = 0;

                result.shirtColors[2, 0] = 0;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 0;
            }

            if (i == 5)
            {
                result.name = "Spain";
                result.shortName = "SPA";
                result.shirtStyle = 0;
                result.shirtType = 1; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 0;
                result.shirtColors[0, 2] = 0;

                result.shirtColors[2, 0] = 0;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 129;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 0;
                result.shirtColors[3, 2] = 0;
            }

            if (i == 6)
            {
                result.name = "Holland";
                result.shortName = "HOL";
                result.shirtStyle = 0;
                result.shirtType = 1; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 128;
                result.shirtColors[0, 2] = 0;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 255;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 128;
                result.shirtColors[3, 2] = 0;
            }

            if (i == 7)
            {
                result.name = "Germany";
                result.shortName = "GER";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 255;
                result.shirtColors[0, 2] = 255;

                result.shirtColors[2, 0] = 0;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 0;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 255;
            }

            if (i == 8)
            {
                result.name = "England";
                result.shortName = "ENG";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 255;
                result.shirtColors[0, 2] = 255;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 255;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 255;
            }

            if (i == 9)
            {
                result.name = "Uruguay";
                result.shortName = "URU";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 112;
                result.shirtColors[0, 1] = 169;
                result.shirtColors[0, 2] = 226;

                result.shirtColors[2, 0] = 0;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 0;

                result.shirtColors[3, 0] = 0;
                result.shirtColors[3, 1] = 0;
                result.shirtColors[3, 2] = 0;
            }

            if (i == 10)
            {
                result.name = "Portugal";
                result.shortName = "POR";
                result.shirtStyle = 0;
                result.shirtType = 1; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 0;
                result.shirtColors[0, 2] = 0;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 0;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 0;
                result.shirtColors[3, 2] = 0;
            }

            if (i == 11)
            {
                result.name = "Italy";
                result.shortName = "ITA";
                result.shirtStyle = 0;
                result.shirtType = 1; //1=bright, 2=dark
                result.shirtColors[0, 0] = 0;
                result.shirtColors[0, 1] = 0;
                result.shirtColors[0, 2] = 255;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 255;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 0;
                result.shirtColors[3, 1] = 0;
                result.shirtColors[3, 2] = 255;
            }

            if (i == 12)
            {
                result.name = "Norway";
                result.shortName = "NOR";
                result.shirtStyle = 0;
                result.shirtType = 1; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 0;
                result.shirtColors[0, 2] = 0;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 255;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 32;
                result.shirtColors[3, 1] = 48;
                result.shirtColors[3, 2] = 96;
            }

            if (i == 13)
            {
                result.name = "Greece";
                result.shortName = "GRE";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 255;
                result.shirtColors[0, 2] = 255;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 255;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 255;
            }

            if (i == 14)
            {
                result.name = "Chile";
                result.shortName = "CHI";
                result.shirtStyle = 0;
                result.shirtType = 1; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 0;
                result.shirtColors[0, 2] = 0;

                result.shirtColors[2, 0] = 0;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 255;
            }

            if (i == 15)
            {
                result.name = "Japan";
                result.shortName = "JPN";
                result.shirtStyle = 0;
                result.shirtType = 1; //1=bright, 2=dark
                result.shirtColors[0, 0] = 0;
                result.shirtColors[0, 1] = 0;
                result.shirtColors[0, 2] = 64;

                result.shirtColors[2, 0] = 0;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 64;

                result.shirtColors[3, 0] = 0;
                result.shirtColors[3, 1] = 0;
                result.shirtColors[3, 2] = 64;
            }

            if (i == 16)
            {
                result.name = "Ghana";
                result.shortName = "GHA";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 255;
                result.shirtColors[0, 2] = 255;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 255;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 255;
            }

            if (i == 17)
            {
                result.name = "Russia";
                result.shortName = "RUS";
                result.shirtStyle = 0;
                result.shirtType = 1; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 0;
                result.shirtColors[0, 2] = 0;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 0;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 0;
                result.shirtColors[3, 2] = 0;
            }

            if (i == 18)
            {
                result.name = "France";
                result.shortName = "FRA";
                result.shirtStyle = 0;
                result.shirtType = 1; //1=bright, 2=dark
                result.shirtColors[0, 0] = 0;
                result.shirtColors[0, 1] = 0;
                result.shirtColors[0, 2] = 128;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 255;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 0;
                result.shirtColors[3, 2] = 0;
            }

            if (i == 19)
            {
                result.name = "Australia";
                result.shortName = "AUS";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 190;
                result.shirtColors[0, 2] = 0;

                result.shirtColors[2, 0] = 0;
                result.shirtColors[2, 1] = 64;
                result.shirtColors[2, 2] = 68;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 190;
                result.shirtColors[3, 2] = 0;

            }

            if (i == 20)
            {
                result.name = "Ivory coast";
                result.shortName = "IVO";
                result.shirtStyle = 0;
                result.shirtType = 1; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 128;
                result.shirtColors[0, 2] = 0;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 128;
                result.shirtColors[2, 2] = 0;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 128;
                result.shirtColors[3, 2] = 0;
            }

            if (i == 21)
            {
                result.name = "USA";
                result.shortName = "USA";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 255;
                result.shirtColors[0, 2] = 255;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 255;
                result.shirtColors[2, 2] = 255;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 255;
            }

            if (i == 22)
            {
                result.name = "Poland";
                result.shortName = "POL";
                result.shirtStyle = 0;
                result.shirtType = 0; //1=bright, 2=dark
                result.shirtColors[0, 0] = 255;
                result.shirtColors[0, 1] = 255;
                result.shirtColors[0, 2] = 255;

                result.shirtColors[2, 0] = 255;
                result.shirtColors[2, 1] = 0;
                result.shirtColors[2, 2] = 0;

                result.shirtColors[3, 0] = 255;
                result.shirtColors[3, 1] = 255;
                result.shirtColors[3, 2] = 255;
            }
            #endregion

            result.nID = i;
            return result;
        }

    }
}
