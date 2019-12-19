using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;

namespace DatabaseServer
{
    class ServerForMS
    {
        public NetServer server;
        NetPeerConfiguration config;
        public List<MasterServer> masterServers = new List<MasterServer>();
        public List<PlayerCountInTeam> playerCountInTeams = new List<PlayerCountInTeam>();
        int totalOnlinePlayerCount = 0;
        public Thread thread;

        public ServerForMS()
        {
            config = new NetPeerConfiguration("NSMobile");
            config.MaximumConnections = 10000;
            config.Port = 14246;
            config.ConnectionTimeout = 10;
            config.PingInterval = 3;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            server = new NetServer(config);
            thread = new Thread(new ThreadStart(Handler));
            server.Start();
            thread.Start();
        }

        void Handler()
        {
            NetIncomingMessage inmsg;
            byte b;
            MasterServer ms = null;

            while (true)
            {
                while ((inmsg = server.ReadMessage()) != null)
                {
                    switch (inmsg.MessageType)
                    {
                        case NetIncomingMessageType.DebugMessage:
                        case NetIncomingMessageType.ErrorMessage:
                        case NetIncomingMessageType.WarningMessage:
                        case NetIncomingMessageType.VerboseDebugMessage:
                            //Console.WriteLine(inmsg.ReadString());
                            break;


                        //*************************************************************************

                        case NetIncomingMessageType.ConnectionApproval:
                            inmsg.SenderConnection.Approve();

                            Console.WriteLine("masterserver approved");

                            lock (masterServers)
                            {
                                masterServers.Add(new MasterServer(inmsg.SenderConnection));
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)inmsg.ReadByte();

                            if (status == NetConnectionStatus.Disconnected)
                            {
                                lock (masterServers)
                                    masterServers.RemoveAll(p => p.netConnection == inmsg.SenderConnection);
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.Data:
                            ms = masterServers.Find(p => p.netConnection == inmsg.SenderConnection);
                            if (ms == null || inmsg.LengthBytes < 1) break;

                            if (masterServers.Find(p => p.netConnection == inmsg.SenderConnection) == null) break;
                            if (inmsg.LengthBytes < 1) break;

                            b = inmsg.ReadByte();

                            //masterserver have re-send rooms to DS
                            if (b == 8)
                                Packet8(inmsg, ms);

                            //masterserver sends pID to DS (checking if online)
                            if (b == 27)
                                Packet27(inmsg);

                            //masterserver sends tID and wants online users
                            if (b == 28)
                                Packet28(inmsg);

                            //masterserver sends lobbyusers to DS
                            if (b == 44)
                                Packet44(inmsg, ms);

                            //chat message
                            if (b == 45)
                                Packet45(inmsg);

                            //masterserver re-send challenge start request to DS
                            if (b == 47)
                                Packet47(inmsg);

                            //masterserver informs, that gameserver have disconnected
                            if (b == 49)
                                Packet49(inmsg, ms);

                            //info about created match
                            if (b == 52)
                                Packet52(inmsg);

                            //team have invited user, lets inform possible online user about invite
                            if (b == 57)
                                Packet57(inmsg);

                            //MS tells league matches, which needs to be started
                            if (b == 62)
                                Packet62(inmsg);

                            // /info msg
                            if (b == 85)
                                Packet85(inmsg);

                            break;

                        //*************************************************************************

                        default:
                            Console.WriteLine("Unhandled type: " + inmsg.MessageType + " " + inmsg.LengthBytes + " bytes " + inmsg.DeliveryMethod + "|" + inmsg.SequenceChannel);
                            break;
                    }
                    server.Recycle(inmsg);
                }

                Thread.Sleep(1);
            }
        }

        //masterserver have re-send rooms to DS
        void Packet8(NetIncomingMessage inmsg, MasterServer ms)
        {
            string natCode; //FI
            byte location;
            string servername;
            int roomCount;
            int plrCount;
            string gameserverIP = "";

            try { location = inmsg.ReadByte(); }
            catch { return; }
            try { natCode = inmsg.ReadString(); }
            catch { return; }
            try { servername = inmsg.ReadString(); }
            catch { return; }
            try { roomCount = inmsg.ReadInt32(); }
            catch { return; }
            try { plrCount = inmsg.ReadInt32(); }
            catch { return; }
            try { gameserverIP = inmsg.ReadString(); }
            catch { return; }

            if (roomCount < 1) return;
            
            int[] uniqueID = new int[roomCount];
            byte[] maxPlayers = new byte[roomCount];
            byte[] roomType = new byte[roomCount];
            byte[] time = new byte[roomCount];
            byte[] roomState = new byte[roomCount];
            bool[] botsEnabled = new bool[roomCount];
            bool[] officiallyStarted = new bool[roomCount];
            int[, ,] pID = new int[2, 10, roomCount];
            int[, ,] plrTID = new int[2, 10, roomCount];
            int[,] teamIDs = new int[2, roomCount];
            string[,] teamnames = new string[2, roomCount];
            byte[,] score = new byte[2, roomCount];

            int spectatorCount;
            List<List<int>> _spectatorPID = new List<List<int>>();
            List<List<int>> _spectatorTID = new List<List<int>>();
            List<User> spectators = new List<User>();

            for (int i = 0; i < roomCount; i++)
            {
                try { uniqueID[i] = inmsg.ReadInt32(); }
                catch { return; }
                try { maxPlayers[i] = inmsg.ReadByte(); }
                catch { return; }
                try { roomType[i] = inmsg.ReadByte(); }
                catch { return; }
                try { time[i] = inmsg.ReadByte(); }
                catch { return; }
                try { roomState[i] = inmsg.ReadByte(); }
                catch { return; }
                try { botsEnabled[i] = inmsg.ReadBoolean(); }
                catch { return; }
                try { officiallyStarted[i] = inmsg.ReadBoolean(); }
                catch { return; }

                for (int j = 0; j < 2; j++)
                {
                    try { teamIDs[j, i] = inmsg.ReadInt32(); }
                    catch { return; }
                    try { teamnames[j, i] = inmsg.ReadString(); }
                    catch { return; }
                    try { score[j, i] = inmsg.ReadByte(); }
                    catch { return; }
                }

                for (int j = 0; j < 2; j++)
                    for (int k = 0; k < maxPlayers[i]; k++)
                    {
                        try { pID[j, k, i] = inmsg.ReadInt32(); }
                        catch { return; }
                        try { plrTID[j, k, i] = inmsg.ReadInt32(); }
                        catch { return; }
                    }

                try { spectatorCount = inmsg.ReadInt32(); }
                catch { return; }

                _spectatorPID.Add(new List<int>());
                _spectatorTID.Add(new List<int>());

                for (int j = 0; j < spectatorCount; j++)
                {
                    try { _spectatorPID[i].Add(inmsg.ReadInt32()); }
                    catch { return; }
                    try { _spectatorTID[i].Add(inmsg.ReadInt32()); }
                    catch { return; }
                }

            }

            //add rooms to list
            lock (ms)
            {
                //remove gameservers by IP
                ms.gameServers.RemoveAll(p => p.IP == gameserverIP);

                for (int i = 0; i < roomCount; i++)
                {
                    int[] _teamIDs = new int[2];
                    string[] _teamnames = new string[2];
                    byte[] _score = new byte[2];

                    int[,] _pID = new int[2, maxPlayers[i]];
                    int[,] _plrTID = new int[2, maxPlayers[i]];

                    for (int j = 0; j < 2; j++)
                    {
                        _teamIDs[j] = teamIDs[j, i];
                        _teamnames[j] = teamnames[j, i];
                        _score[j] = score[j, i];

                        for (int k = 0; k < maxPlayers[i]; k++)
                        {
                            _pID[j, k] = pID[j, k, i];
                            _plrTID[j, k] = plrTID[j, k, i];
                        }
                    }

                    spectators.Clear();

                    for (int j = 0; j < _spectatorPID[i].Count; j++)
                        spectators.Add(new User(_spectatorPID[i][j], _spectatorTID[i][j]));

                    ms.gameServers.Add(new GameServer(servername, plrCount, location, natCode, gameserverIP, uniqueID[i], maxPlayers[i], (RoomType)roomType[i], time[i], (RoomState)roomState[i], botsEnabled[i], officiallyStarted[i], _pID, _plrTID, _teamIDs, _score, _teamnames, spectators.ToList<User>()));
                }
            }

        }

        //masterserver sends pID to DS (checking if online)
        void Packet27(NetIncomingMessage inmsg)
        {
            int pID = 0;
            long remoteUniqueIdentifier = 0;
            bool isOnline = false;
            bool userFound = false;

            try { pID = inmsg.ReadInt32(); }
            catch { return; }
            try { remoteUniqueIdentifier = inmsg.ReadInt64(); }
            catch { return; }

            lock (masterServers)
            {
                for (int t = 0; t < masterServers.Count; t++)
                {
                    for (int y = 0; y < masterServers[t].lobbyUsers.Count; y++)
                    {
                        if (masterServers[t].lobbyUsers[y].pID == pID)
                        {
                            isOnline = true;
                            userFound = true;
                            break;
                        }
                    }

                    if (userFound) break;

                    for (int y = 0; y < masterServers[t].gameServers.Count; y++)
                    {
                        for (int i = 0; i < 2; i++)
                            for (int j = 0; j < masterServers[t].gameServers[y].maxPlayers; j++)
                                if (masterServers[t].gameServers[y].users[i, j].pID == pID)
                                {
                                    isOnline = true;
                                    userFound = true;
                                    break;
                                }
                    }

                    if (userFound) break;
                }
            }

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)27);

            outmsg.Write(pID);
            outmsg.Write(remoteUniqueIdentifier);
            outmsg.Write(isOnline);

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
        }

        //masterserver sends tID and wants online users
        void Packet28(NetIncomingMessage inmsg)
        {

            int tID = 0;
            long remoteUniqueIdentifier = 0;
            List<int> pIDOnline = new List<int>();

            try { tID = inmsg.ReadInt32(); }
            catch { return; }
            try { remoteUniqueIdentifier = inmsg.ReadInt64(); }
            catch { return; }

            lock (masterServers)
            {
                for (int t = 0; t < masterServers.Count; t++)
                {
                    for (int y = 0; y < masterServers[t].lobbyUsers.Count; y++)
                    {
                        if (masterServers[t].lobbyUsers[y].tID == tID) pIDOnline.Add(masterServers[t].lobbyUsers[y].pID);
                    }

                    for (int y = 0; y < masterServers[t].gameServers.Count; y++)
                    {
                        for (int i = 0; i < 2; i++)
                            for (int j = 0; j < masterServers[t].gameServers[y].maxPlayers; j++)
                                if (masterServers[t].gameServers[y].users[i, j].tID == tID)
                                    pIDOnline.Add(masterServers[t].gameServers[y].users[i, j].pID);

                        for (int i = 0; i < masterServers[t].gameServers[y].spectators.Count; i++)
                            if (masterServers[t].gameServers[y].spectators[i].tID == tID)
                                pIDOnline.Add(masterServers[t].gameServers[y].spectators[i].pID);
                    }
                }
            }

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)28);

            outmsg.Write(tID);
            outmsg.Write(remoteUniqueIdentifier);

            outmsg.Write(pIDOnline.Count);
            for (int i = 0; i < pIDOnline.Count; i++)
                outmsg.Write(pIDOnline[i]);

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
        }

        //masterserver sends lobbyusers to DS
        void Packet44(NetIncomingMessage inmsg, MasterServer ms)
        {
            int userCount;
            List<int> pID = new List<int>();
            List<int> tID = new List<int>();

            try { userCount = inmsg.ReadInt32(); }
            catch { return; }

            for (int i = 0; i < userCount; i++)
            {
                try { pID.Add(inmsg.ReadInt32()); }
                catch { return; }
                try { tID.Add(inmsg.ReadInt32()); }
                catch { return; }
            }

            lock (ms)
            {
                ms.timeout = 0;
                ms.lobbyUsers.Clear();

                for (int i = 0; i < pID.Count; i++)
                {
                    ms.lobbyUsers.Add(new User(pID[i], tID[i]));
                }
            }

        }

        //chat message
        void Packet45(NetIncomingMessage inmsg)
        {
            byte receiverType;  //0=user, 1=team, 2=public
            string receiver;
            string message;
            int senderPID;
            int senderTID;

            try { receiverType = inmsg.ReadByte(); }
            catch { return; }
            try { receiver = inmsg.ReadString(); }
            catch { return; }
            try { message = inmsg.ReadString(); }
            catch { return; }
            try { senderPID = inmsg.ReadInt32(); }
            catch { return; }
            try { senderTID = inmsg.ReadInt32(); }
            catch { return; }

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)45);

            outmsg.Write(receiverType);
            outmsg.Write(receiver);
            outmsg.Write(message);
            outmsg.Write(senderPID);
            outmsg.Write(senderTID);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            lock (masterServers)
            {
                for (int t = 0; t < masterServers.Count; t++)
                {
                    if (masterServers[t].netConnection != null)
                    {
                        var another = server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        server.SendMessage(another, masterServers[t].netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                    }
                }
            }

        }

        //masterserver re-send challenge start request to DS
        void Packet47(NetIncomingMessage inmsg)
        {
            int[] tID = new int[2];
            byte[] selectedKit = new byte[2];
            byte location;
            bool botsEnabled;
            byte maxPlayers;

            for (int i = 0; i < 2; i++)
            {
                try { tID[i] = inmsg.ReadInt32(); }
                catch { return; }
                try { selectedKit[i] = inmsg.ReadByte(); }
                catch { return; }
            }

            try { location = inmsg.ReadByte(); }
            catch { return; }
            try { botsEnabled = inmsg.ReadBoolean(); }
            catch { return; }
            try { maxPlayers = inmsg.ReadByte(); }
            catch { return; }

            int mID = -1;  //masterserver id in array
            string _IP = "";

            List<PlayerCount> playerCounts = new List<PlayerCount>();
 
            lock (masterServers)
            {
                #region calculate gameservers total player counts
                for (int i = 0; i < masterServers.Count; i++)
                    for (int j = 0; j < masterServers[i].gameServers.Count; j++)
                    {
                        if (masterServers[i].gameServers[j].location != location) continue;

                        bool alreadyAdded = false;

                        for (int k = 0; k < playerCounts.Count; k++)
                            if (playerCounts[k].IP == masterServers[i].gameServers[j].IP)
                            {
                                alreadyAdded = true;
                                playerCounts[k].plrCount += masterServers[i].gameServers[j].plrCount;
                            }

                        if (!alreadyAdded)
                            playerCounts.Add(new PlayerCount(i, masterServers[i].gameServers[j].IP, masterServers[i].gameServers[j].plrCount, masterServers[i].gameServers[j].location));
                    }
                #endregion

                int smallestPlrCount = int.MaxValue;

                //check, which IP contains fewest users
                for (int i = 0; i < playerCounts.Count; i++)
                {
                    if (playerCounts[i].plrCount < smallestPlrCount)
                    {
                        smallestPlrCount = playerCounts[i].plrCount;
                        mID = playerCounts[i].mID;
                        _IP = playerCounts[i].IP;
                    }
                }

                //masterserver&gameserver found
                if (mID > -1)
                {
                    NetOutgoingMessage outmsg = server.CreateMessage();
                    outmsg.Write((byte)47);

                    outmsg.Write(_IP);  //gameserver with this IP should create challenge room
                    outmsg.Write(botsEnabled);
                    outmsg.Write(maxPlayers);

                    for (int i = 0; i < 2; i++)
                    {
                        outmsg.Write(tID[i]);
                        outmsg.Write(selectedKit[i]);
                    }

                    //do not copy paste this SendMessage!!!
                    server.SendMessage(outmsg, masterServers[mID].netConnection, NetDeliveryMethod.ReliableOrdered, 0);
                }
                //couldn't find gameserver and create match
                else
                {
                    //todo...
                    Console.WriteLine("todo... couldn't find gameserver and create match");
                }

            }

        }

        //masterserver informs, that gameserver have disconnected
        void Packet49(NetIncomingMessage inmsg, MasterServer ms)
        {
            string gameserverIP;

            try { gameserverIP = inmsg.ReadString(); }
            catch { return; }

            //remove rooms of disconnected gameserver
            lock (ms)
            {
                //remove gameservers by IP
                ms.gameServers.RemoveAll(p => p.IP == gameserverIP);
            }

        }

        //info about created match
        void Packet52(NetIncomingMessage inmsg)
        {
            int[] tID = new int[2];
            string[] teamnames = new string[2];

            for (int i = 0; i < 2; i++)
            {
                try { tID[i] = inmsg.ReadInt32(); }
                catch { return; }
                try { teamnames[i] = inmsg.ReadString(); }
                catch { return; }
            }

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)52);

            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(tID[i]);
                outmsg.Write(teamnames[i]);
            }

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            lock (masterServers)
            {
                for (int t = 0; t < masterServers.Count; t++)
                {
                    if (masterServers[t].netConnection != null)
                    {
                        var another = server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        server.SendMessage(another, masterServers[t].netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                    }
                }
            }
        }

        //team have invited user, lets inform possible online user about invite
        void Packet57(NetIncomingMessage inmsg)
        {
            int pID;

            try { pID = inmsg.ReadInt32(); }
            catch { return; }

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)57);
            outmsg.Write(pID);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            lock (masterServers)
            {
                for (int t = 0; t < masterServers.Count; t++)
                {
                    if (masterServers[t].netConnection != null)
                    {
                        var another = server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        server.SendMessage(another, masterServers[t].netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                    }
                }
            }

        }

        //MS tells league matches, which needs to be started
        void Packet62(NetIncomingMessage inmsg)
        {
            int matchCount;
            List<Fixture> fixtures = new List<Fixture>();

            try { matchCount = inmsg.ReadInt32(); }
            catch { return; }

            int fixtureID;
            byte location;
            int[] tID = new int[2];

            for (int i = 0; i < matchCount; i++)
            {
                try { fixtureID = inmsg.ReadInt32(); }
                catch { return; }
                try { location = inmsg.ReadByte(); }
                catch { return; }
                try { tID[0] = inmsg.ReadInt32(); }
                catch { return; }
                try { tID[1] = inmsg.ReadInt32(); }
                catch { return; }

                if (!IsTeamAlreadyPlayingLeagueMatch(tID[0], tID[1]))
                    fixtures.Add(new Fixture(fixtureID, tID[0], tID[1], location));
            }

            //**************

            List<PlayerCount> playerCounts = new List<PlayerCount>();

            lock (masterServers)
            {
                #region calculate gameservers total player counts
                for (int i = 0; i < masterServers.Count; i++)
                    for (int j = 0; j < masterServers[i].gameServers.Count; j++)
                    {
                        bool alreadyAdded = false;

                        for (int k = 0; k < playerCounts.Count; k++)
                            if (playerCounts[k].IP == masterServers[i].gameServers[j].IP)
                            {
                                alreadyAdded = true;
                                playerCounts[k].plrCount += masterServers[i].gameServers[j].plrCount;
                            }

                        if (!alreadyAdded)
                            playerCounts.Add(new PlayerCount(i, masterServers[i].gameServers[j].IP, masterServers[i].gameServers[j].plrCount, masterServers[i].gameServers[j].location));
                    }
                #endregion

                //find gameserver for fixture, which contains fewest players (note, that we simulate more users, when fixture is setted)
                for (int k = 0; k < fixtures.Count; k++)
                {
                    int smallestPlrCount = int.MaxValue;
                    int _i = -1;

                    //check, which IP contains fewest users
                    for (int i = 0; i < playerCounts.Count; i++)
                    {
                        if (playerCounts[i].location != fixtures[k].location) continue;

                        if (playerCounts[i].plrCount < smallestPlrCount)
                        {
                            smallestPlrCount = playerCounts[i].plrCount;
                            _i = i;
                        }
                    }

                    if (_i == -1) return;

                    playerCounts[_i].plrCount += 12;
                    fixtures[k].IP = playerCounts[_i].IP;
                    fixtures[k].mID = playerCounts[_i].mID;

                }//end of (for fixtures)

                //send to masterservers list of fixtures, which those needs to start
                for (int i = 0; i < masterServers.Count; i++)
                {
                    NetOutgoingMessage outmsg = server.CreateMessage();
                    outmsg.Write((byte)63);
                    bool packetToBeSent = false;
                    int fixtureCount = 0;

                    //count fixtureCount
                    for (int k = 0; k < fixtures.Count; k++)
                        if (fixtures[k].mID == i) fixtureCount++;

                    outmsg.Write(fixtureCount);

                    for (int k = 0; k < fixtures.Count; k++)
                    {
                        if (fixtures[k].mID != i) continue;

                        packetToBeSent = true;

                        outmsg.Write(fixtures[k].fixtureID);
                        outmsg.Write(fixtures[k].IP);
                        outmsg.Write(fixtures[k].tID[0]);
                        outmsg.Write(fixtures[k].tID[1]);
                    }

                    if (!packetToBeSent) continue;

                    server.SendMessage(outmsg, masterServers[i].netConnection, NetDeliveryMethod.ReliableOrdered, 0);

                }


            }//end of lock



        }

        // /info msg
        void Packet85(NetIncomingMessage inmsg)
        {
            int pID;
            string username;
            long remoteUniqueIdentifier = 0;

            try { pID = inmsg.ReadInt32(); }
            catch { return; }
            try { username = inmsg.ReadString(); }
            catch { return; }
            try { remoteUniqueIdentifier = inmsg.ReadInt64(); }
            catch { return; }

            string where = InfoRequest(pID);


            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)85);

            outmsg.Write(username);
            outmsg.Write(where);
            outmsg.Write(remoteUniqueIdentifier);

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
        }

        string InfoRequest(int pID)
        {
            lock (masterServers)
            {
                for (int t = 0; t < masterServers.Count; t++)
                {
                    for (int y = 0; y < masterServers[t].lobbyUsers.Count; y++)
                        if (masterServers[t].lobbyUsers[y].pID == pID) return "lobby";

                    for (int y = 0; y < masterServers[t].gameServers.Count; y++)
                    {
                        for (int i = 0; i < 2; i++)
                            for (int j = 0; j < masterServers[t].gameServers[y].maxPlayers; j++)
                                if (masterServers[t].gameServers[y].users[i, j].pID == pID)
                                {
                                    if (masterServers[t].gameServers[y].roomType == RoomType.Public)
                                        return "Training->"+masterServers[t].gameServers[y].servername + " " + masterServers[t].gameServers[y].uniqueID;
                                    else
                                        return "Matches->"+masterServers[t].gameServers[y].teamnames[0] + "-" + masterServers[t].gameServers[y].teamnames[1];
                                }

                        for (int i = 0; i < masterServers[t].gameServers[y].spectators.Count; i++)
                            if (masterServers[t].gameServers[y].spectators[i].pID == pID)
                            {
                                if (masterServers[t].gameServers[y].roomType == RoomType.Public)
                                    return "Training->"+masterServers[t].gameServers[y].servername + " " + masterServers[t].gameServers[y].uniqueID + " (spectating)";
                                else
                                    return "Matches->"+masterServers[t].gameServers[y].teamnames[0] + "-" + masterServers[t].gameServers[y].teamnames[1] + " (spectating)";
                            }
                    }
                }
            }

            return "offline";
        }

        public void BroadcastRoomsAndOnlineTeamsToMS()
        {
            if (masterServers.Count == 0) return;

            CalculatePlayerCountInTeams();

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)23);

            outmsg.Write(totalOnlinePlayerCount);
            outmsg.Write(playerCountInTeams.Count);
            for (int i = 0; i < playerCountInTeams.Count; i++)
            {
                outmsg.Write(playerCountInTeams[i].tID);
                outmsg.Write(playerCountInTeams[i].count);
                outmsg.Write(playerCountInTeams[i].isPlaying);
            }

            lock (masterServers)
            {
                outmsg.Write(masterServers.Count);

                for (int i = 0; i < masterServers.Count; i++)
                {
                    outmsg.Write(masterServers[i].gameServers.Count);

                    for (int j = 0; j < masterServers[i].gameServers.Count; j++)
                    {
                        outmsg.Write(masterServers[i].gameServers[j].servername);
                        outmsg.Write(masterServers[i].gameServers[j].IP);
                        outmsg.Write(masterServers[i].gameServers[j].uniqueID);
                        outmsg.Write(masterServers[i].gameServers[j].maxPlayers);
                        outmsg.Write((byte)masterServers[i].gameServers[j].roomType);
                        outmsg.Write(masterServers[i].gameServers[j].time);
                        outmsg.Write((byte)masterServers[i].gameServers[j].roomState);
                        outmsg.Write(masterServers[i].gameServers[j].botsEnabled);
                        outmsg.Write(masterServers[i].gameServers[j].location);
                        outmsg.Write(masterServers[i].gameServers[j].natCode);

                        for (int k = 0; k < 2; k++)
                        {
                            outmsg.Write(masterServers[i].gameServers[j].score[k]);
                            outmsg.Write(masterServers[i].gameServers[j].tID[k]);
                            outmsg.Write(masterServers[i].gameServers[j].teamnames[k]);
                        }

                        for (int k = 0; k < 2; k++)
                            for (int l = 0; l < masterServers[i].gameServers[j].maxPlayers; l++)
                            {
                                outmsg.Write(masterServers[i].gameServers[j].users[k, l].pID);
                                outmsg.Write(masterServers[i].gameServers[j].users[k, l].tID);
                            }
                    }
                }
            }

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            for (int i = 0; i < masterServers.Count; i++)
                if (masterServers[i].netConnection != null)
                {
                    var another = server.CreateMessage();
                    another.Write(saved);
                    another.LengthBits = savedBitLength;
                    server.SendMessage(another, masterServers[i].netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                }

        }

        public void LeagueHandlerMessage_And_DeleteInactiveUsers()
        {
            if (masterServers.Count == 0) return;

            lock (masterServers)
            {
                //lets send message to only 1 masterserver
                for (int i = 0; i < masterServers.Count; i++)

                    if (masterServers[i].netConnection != null)
                    {
                        NetOutgoingMessage outmsg = server.CreateMessage();
                        outmsg.Write((byte)62);

                        server.SendMessage(outmsg, masterServers[i].netConnection, NetDeliveryMethod.ReliableOrdered, 0);
                        return;
                    }
            }

        }

        void CalculatePlayerCountInTeams()
        {

            playerCountInTeams.Clear();
            bool pIDCounted;
            totalOnlinePlayerCount = 0;

            lock (masterServers)
            {
                for (int i = 0; i < masterServers.Count; i++)
                {
                    //count players in lobby
                    for (int j = 0; j < masterServers[i].lobbyUsers.Count; j++)
                    {
                        totalOnlinePlayerCount++;

                        if (masterServers[i].lobbyUsers[j].tID == 0) continue;

                        pIDCounted = false;

                        for (int k = 0; k < playerCountInTeams.Count; k++)
                            if (playerCountInTeams[k].tID == masterServers[i].lobbyUsers[j].tID)
                            {
                                playerCountInTeams[k].count++;
                                pIDCounted = true;
                                break;
                            }

                        if (!pIDCounted)
                            playerCountInTeams.Add(new PlayerCountInTeam(masterServers[i].lobbyUsers[j].tID));
                    }

                    //***************

                    //count players in gameservers
                    for (int j = 0; j < masterServers[i].gameServers.Count; j++)
                    {
                        //players ingame
                        for (int k = 0; k < 2; k++)
                            for (int l = 0; l < masterServers[i].gameServers[j].maxPlayers; l++)
                            {
                                if (masterServers[i].gameServers[j].users[k, l].pID == 0) continue;
                                totalOnlinePlayerCount++;
                                if (masterServers[i].gameServers[j].users[k, l].tID == 0) continue;

                                pIDCounted = false;

                                for (int m = 0; m < playerCountInTeams.Count; m++)
                                    if (playerCountInTeams[m].tID == masterServers[i].gameServers[j].users[k, l].tID)
                                    {
                                        playerCountInTeams[m].count++;
                                        pIDCounted = true;
                                        break;
                                    }

                                if (!pIDCounted)
                                    playerCountInTeams.Add(new PlayerCountInTeam(masterServers[i].gameServers[j].users[k, l].tID));
                            }

                        //spectators
                        for (int k = 0; k < masterServers[i].gameServers[j].spectators.Count; k++)
                        {
                            if (masterServers[i].gameServers[j].spectators[k].pID == 0) continue;
                            totalOnlinePlayerCount++;
                            if (masterServers[i].gameServers[j].spectators[k].tID == 0) continue;

                            pIDCounted = false;

                            for (int m = 0; m < playerCountInTeams.Count; m++)
                                if (playerCountInTeams[m].tID == masterServers[i].gameServers[j].spectators[k].tID)
                                {
                                    playerCountInTeams[m].count++;
                                    pIDCounted = true;
                                    break;
                                }

                            if (!pIDCounted)
                                playerCountInTeams.Add(new PlayerCountInTeam(masterServers[i].gameServers[j].spectators[k].tID));
                        }
                    }

                    //***************

                    //check, if team is playing
                    for (int j = 0; j < masterServers[i].gameServers.Count; j++)
                        for (int k = 0; k < 2; k++)
                            for (int m = 0; m < playerCountInTeams.Count; m++)
                                if (masterServers[i].gameServers[j].tID[k] == playerCountInTeams[m].tID)
                                {
                                    playerCountInTeams[m].isPlaying = true;
                                    break;
                                }

                    //***************

                }
            }

        }

        bool IsTeamAlreadyPlayingLeagueMatch(int tID0, int tID1)
        {
            lock (masterServers)
            {
                for (int i = 0; i < masterServers.Count; i++)
                    for (int j = 0; j < masterServers[i].gameServers.Count; j++)
                    {
                        if (!masterServers[i].gameServers[j].officiallyStarted) continue;

                        if ((masterServers[i].gameServers[j].tID[0] == tID0 && masterServers[i].gameServers[j].tID[1] == tID1) || (masterServers[i].gameServers[j].tID[0] == tID1 && masterServers[i].gameServers[j].tID[1] == tID0))
                            return true;
                    }
            }

            return false;
        }

    }

}
