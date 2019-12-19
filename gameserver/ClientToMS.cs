using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;

namespace GameServerMono
{
    class ClientToMS
    {
        NetPeerConfiguration config;
        public NetClient client;
        public RoomHandler roomHandler;
        ClientToBS clientToBS;
        ServerForU serverForU;

        public Thread thread;

        public ClientToMS(ServerForU serverForU)
        {
            this.serverForU = serverForU;
            config = new NetPeerConfiguration("NSMobile");
            client = new NetClient(config);
            thread = new Thread(new ThreadStart(Handler));

            //client.RegisterReceivedCallback (new SendOrPostCallback (Handler));
            client.Start();
            thread.Start();
        }

        public void SetReferences(ClientToBS clientToBS)
        {
            this.clientToBS = clientToBS;
        }

        void Handler()
        {
            NetIncomingMessage inmsg;
            byte b;

            while (true)
            {
                while ((inmsg = client.ReadMessage()) != null)
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

                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)inmsg.ReadByte();

                            if (status == NetConnectionStatus.Connected)
                                ConnectedToMasterServer();

                            if (status == NetConnectionStatus.Disconnected)
                            {
                                Console.WriteLine("disconnected from masterserver");
                                clientToBS.attempReconnectToBalanceServer = true;
                                clientToBS.reconnectToBalanceServer = true;
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.Data:
                            if (inmsg.LengthBytes < 1)
                                break;

                            b = inmsg.ReadByte();

                            //masterserver have verified user
                            if (b == 10)
                                Packet10(inmsg);

                            //chat message
                            if (b == 45)
                                Packet45(inmsg);

                            //masterserver inform gameserver to start challenge
                            if (b == 47)
                                Packet47(inmsg);

                            //info about created match
                            if (b == 52)
                                Packet52(inmsg);

                            //team have invited user, lets inform possible online user about invite
                            if (b == 57)
                                Packet57(inmsg);

                            //admin message
                            if (b == 86)
                                Packet86(inmsg);

                            //MS inform GS about user, which needs to be kicked out from server
                            if (b == 87)
                                Packet87(inmsg);

                            break;

                        //*************************************************************************

                        default:
                            Console.WriteLine("Unhandled type: " + inmsg.MessageType + " " + inmsg.LengthBytes + " bytes " + inmsg.DeliveryMethod + "|" + inmsg.SequenceChannel);
                            break;
                    }
                    client.Recycle(inmsg);
                }

                Thread.Sleep(1);
            }
        }

        //masterserver have verified user
        void Packet10(NetIncomingMessage inmsg)
        {
            int pID;
            int tID;
            int lockedTID;
            bool isSpectator;
            string nation;
            int _selectedServerUniqueID;
            long GSUniqueIdentifier;
            byte admas;
            byte body;
            byte skin;
            byte hair;
            byte number;
            string username;
            bool isVip;
            byte[] shoe = new byte[3];

            try
            {
                pID = inmsg.ReadInt32();
            }  //pID
            catch
            {
                return;
            }
            try
            {
                tID = inmsg.ReadInt32();
            }  //tID
            catch
            {
                return;
            }
            try
            {
                lockedTID = inmsg.ReadInt32();
            }
            catch
            {
                return;
            }
            try
            {
                isSpectator = inmsg.ReadBoolean();
            }
            catch
            {
                return;
            }
            try
            {
                nation = inmsg.ReadString();
            }  //nation
            catch
            {
                return;
            }
            try
            {
                _selectedServerUniqueID = inmsg.ReadInt32();
            }  //selectedServerUniqueID
            catch
            {
                return;
            }
            try
            {
                GSUniqueIdentifier = inmsg.ReadInt64();
            }  //GSUniqueIdentifier
            catch
            {
                return;
            }
            try
            {
                admas = inmsg.ReadByte();
            }
            catch
            {
                return;
            }
            try
            {
                body = inmsg.ReadByte();
            }  //body
            catch
            {
                return;
            }
            try
            {
                skin = inmsg.ReadByte();
            }  //skin
            catch
            {
                return;
            }
            try
            {
                hair = inmsg.ReadByte();
            }  //hair
            catch
            {
                return;
            }
            try
            {
                number = inmsg.ReadByte();
            }  //number
            catch
            {
                return;
            }
            try
            {
                username = inmsg.ReadString();
            }  //username
            catch
            {
                return;
            }
            try
            {
                isVip = inmsg.ReadBoolean();
            }  //vip
            catch
            {
                return;
            }
            for (int i = 0; i < 3; i++)
                try
                {
                    shoe[i] = inmsg.ReadByte();
                }  //shoe[]
                catch
                {
                    return;
                }

            //lets find connection, who wants to join to room
            UserConnection conn = serverForU.userConnections.Find(p => p.netConnection.RemoteUniqueIdentifier == GSUniqueIdentifier);
            if (conn == null)
                return;

            if (isSpectator)
                AddSpectatorToServer(
                    _selectedServerUniqueID,
                    conn.netConnection,
                    pID,
                    tID,
                    username);
            else
                TryToAddUserToServer(
                    _selectedServerUniqueID,
                    conn.netConnection,
                    pID,
                    tID,
                    lockedTID,
                    username,
                    nation,
                    isVip,
                    admas,
                    body,
                    skin,
                    hair,
                    number
                    /*shoe.ToArray()*/
                );
        }

        //chat message
        void Packet45(NetIncomingMessage inmsg)
        {
            byte receiverType;  //0=to user, 1=to team
            int receiverPID = 0;
            int receiverTID = 0;
            string message;
            string senderUsername = "";
            string senderTeamname = "";

            try
            {
                receiverType = inmsg.ReadByte();
            }
            catch
            {
                return;
            }
            try
            {
                receiverPID = inmsg.ReadInt32();
            }
            catch
            {
                return;
            }
            try
            {
                receiverTID = inmsg.ReadInt32();
            }
            catch
            {
                return;
            }
            try
            {
                message = inmsg.ReadString();
            }
            catch
            {
                return;
            }
            try
            {
                senderUsername = inmsg.ReadString();
            }
            catch
            {
                return;
            }
            try
            {
                senderTeamname = inmsg.ReadString();
            }
            catch
            {
                return;
            }

            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)45);

            outmsg.Write(receiverType);
            outmsg.Write(message);
            outmsg.Write(senderUsername);
            outmsg.Write(senderTeamname);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            //message to user (user may not be at all in gameserver)
            bool msgSent = false;
            if (receiverType == 0)
                for (int u = 0; u < roomHandler.rooms.Count; u++)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        for (int j = 0; j < roomHandler.rooms[u].maxPlayers; j++)
                        {
                            if (roomHandler.rooms[u].users[i, j].connection == null)
                                continue;

                            if (roomHandler.rooms[u].users[i, j].pID == receiverPID)
                            {
                                var another = serverForU.server.CreateMessage();
                                another.Write(saved);
                                another.LengthBits = savedBitLength;
                                serverForU.server.SendMessage(another, roomHandler.rooms[u].users[i, j].connection, NetDeliveryMethod.ReliableOrdered, 3);
                                msgSent = true;
                                break;
                            }
                        }
                        if (msgSent)
                            break;
                    }

                    lock (roomHandler.rooms[u].spectatorData2)
                    {
                        for (int i = 0; i < roomHandler.rooms[u].spectatorData2.Count; i++)
                        {
                            if (roomHandler.rooms[u].spectatorData2[i].connection == null)
                                continue;

                            if (roomHandler.rooms[u].spectatorData2[i].pID == receiverPID)
                            {
                                var another = serverForU.server.CreateMessage();
                                another.Write(saved);
                                another.LengthBits = savedBitLength;
                                serverForU.server.SendMessage(another, roomHandler.rooms[u].spectatorData2[i].connection, NetDeliveryMethod.ReliableOrdered, 3);
                                msgSent = true;
                                break;
                            }
                        }
                    }

                    if (msgSent)
                        break;
                }

            //message to team
            if (receiverType == 1)
                for (int u = 0; u < roomHandler.rooms.Count; u++)
                {
                    for (int i = 0; i < 2; i++)
                        for (int j = 0; j < roomHandler.rooms[u].maxPlayers; j++)
                        {
                            if (roomHandler.rooms[u].users[i, j].connection == null)
                                continue;

                            if (roomHandler.rooms[u].users[i, j].tID == receiverTID)
                            {
                                var another = serverForU.server.CreateMessage();
                                another.Write(saved);
                                another.LengthBits = savedBitLength;
                                serverForU.server.SendMessage(another, roomHandler.rooms[u].users[i, j].connection, NetDeliveryMethod.ReliableOrdered, 3);
                            }
                        }

                    lock (roomHandler.rooms[u].spectatorData2)
                    {
                        for (int i = 0; i < roomHandler.rooms[u].spectatorData2.Count; i++)
                        {
                            if (roomHandler.rooms[u].spectatorData2[i].connection == null)
                                continue;

                            if (roomHandler.rooms[u].spectatorData2[i].tID == receiverTID)
                            {
                                var another = serverForU.server.CreateMessage();
                                another.Write(saved);
                                another.LengthBits = savedBitLength;
                                serverForU.server.SendMessage(another, roomHandler.rooms[u].spectatorData2[i].connection, NetDeliveryMethod.ReliableOrdered, 3);
                            }
                        }
                    }

                }


        }

        //masterserver inform gameserver to start challenge
        void Packet47(NetIncomingMessage inmsg)
        {
            bool officiallyStarted;
            int fixtureID;
            bool botsEnabled;
            byte maxPlayers;
            int[] tID = new int[2];
            string[] teamnames = new string[2];
            bool[] isOfficialLeagueBotTeam = new bool[2];
            byte[] selectedKit = new byte[2];  //0-1 (home or away colors)
            byte[] shirtstyle = new byte[2];   //0-9 (which mask is used)  
            byte[,] shirtRgb = new byte[2, 24];
            byte[,] shirtColorsH = new byte[4, 3];
            byte[,] shirtColorsA = new byte[4, 3];

            try
            {
                officiallyStarted = inmsg.ReadBoolean();
            }
            catch
            {
                return;
            }
            try
            {
                fixtureID = inmsg.ReadInt32();
            }
            catch
            {
                return;
            }
            //****
            try
            {
                botsEnabled = inmsg.ReadBoolean();
            }
            catch
            {
                return;
            }
            try
            {
                maxPlayers = inmsg.ReadByte();
            }
            catch
            {
                return;
            }

            //****
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    isOfficialLeagueBotTeam[i] = inmsg.ReadBoolean();
                }
                catch
                {
                    return;
                }
                try
                {
                    tID[i] = inmsg.ReadInt32();
                }
                catch
                {
                    return;
                }
                try
                {
                    teamnames[i] = inmsg.ReadString();
                }
                catch
                {
                    return;
                }
                try
                {
                    selectedKit[i] = inmsg.ReadByte();
                }
                catch
                {
                    return;
                }
                try
                {
                    shirtstyle[i] = inmsg.ReadByte();
                }
                catch
                {
                    return;
                }

                for (int j = 0; j < 24; j++)
                    try
                    {
                        shirtRgb[i, j] = inmsg.ReadByte();
                    }
                    catch
                    {
                        return;
                    }
            }

            //data readed

            //"convert" shirtRgb->shirtColors
            //home
            int startID;
            int nextID = 0;
            if (selectedKit[0] == 0)
                startID = 0;
            else
                startID = 12;

            for (int j = 0; j < 4; j++)
                for (int k = 0; k < 3; k++)
                {
                    shirtColorsH[j, k] = shirtRgb[0, nextID + startID];
                    nextID++;
                }

            //away
            nextID = 0;
            if (selectedKit[1] == 0)
                startID = 0;
            else
                startID = 12;

            for (int j = 0; j < 4; j++)
                for (int k = 0; k < 3; k++)
                {
                    shirtColorsA[j, k] = shirtRgb[1, nextID + startID];
                    nextID++;
                }



            TeamData[] teams = new TeamData[2];

            teams[0] = new TeamData(isOfficialLeagueBotTeam[0], tID[0], teamnames[0], "", shirtstyle[0], shirtColorsH);
            teams[1] = new TeamData(isOfficialLeagueBotTeam[1], tID[1], teamnames[1], "", shirtstyle[1], shirtColorsA);

            roomHandler.CreateChallengeRoom(officiallyStarted, fixtureID, teams[0], teams[1], botsEnabled, maxPlayers);

            //inform masterserver, that match created
            NetOutgoingMessage outmsg = client.CreateMessage();
            outmsg.Write((byte)52);

            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(tID[i]);
                outmsg.Write(teamnames[i]);
            }

            client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        //info about created match
        void Packet52(NetIncomingMessage inmsg)
        {
            int[] tID = new int[2];
            string[] teamnames = new string[2];

            for (int i = 0; i < 2; i++)
            {
                try
                {
                    tID[i] = inmsg.ReadInt32();
                }
                catch
                {
                    return;
                }
                try
                {
                    teamnames[i] = inmsg.ReadString();
                }
                catch
                {
                    return;
                }
            }

            //data readed

            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)52);

            for (int i = 0; i < 2; i++)
                outmsg.Write(teamnames[i]);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            lock (roomHandler.rooms)
            {
                for (int u = 0; u < roomHandler.rooms.Count; u++)
                {
                    for (int j = 0; j < 2; j++)
                        for (int k = 0; k < roomHandler.rooms[u].maxPlayers; k++)
                        {
                            if (roomHandler.rooms[u].users[j, k].connection == null)
                                continue;
                            if (roomHandler.rooms[u].users[j, k].tID == 0)
                                continue;

                            if (roomHandler.rooms[u].users[j, k].tID == tID[0] || roomHandler.rooms[u].users[j, k].tID == tID[1])
                            {
                                var another = serverForU.server.CreateMessage();
                                another.Write(saved);
                                another.LengthBits = savedBitLength;
                                serverForU.server.SendMessage(another, roomHandler.rooms[u].users[j, k].connection, NetDeliveryMethod.ReliableOrdered, 3);
                            }
                        }

                    lock (roomHandler.rooms[u].spectatorData2)
                    {
                        for (int j = 0; j < roomHandler.rooms[u].spectatorData2.Count; j++)
                        {
                            if (roomHandler.rooms[u].spectatorData2[j].connection == null)
                                continue;
                            if (roomHandler.rooms[u].spectatorData2[j].tID == 0)
                                continue;

                            if (roomHandler.rooms[u].spectatorData2[j].tID == tID[0] || roomHandler.rooms[u].spectatorData2[j].tID == tID[1])
                            {
                                var another = serverForU.server.CreateMessage();
                                another.Write(saved);
                                another.LengthBits = savedBitLength;
                                serverForU.server.SendMessage(another, roomHandler.rooms[u].spectatorData2[j].connection, NetDeliveryMethod.ReliableOrdered, 3);
                            }
                        }
                    }

                }
            }

        }

        //team have invited user, lets inform possible online user about invite
        void Packet57(NetIncomingMessage inmsg)
        {
            int pID;

            try
            {
                pID = inmsg.ReadInt32();
            }
            catch
            {
                return;
            }

            //data readed

            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)57);

            lock (roomHandler.rooms)
            {
                for (int u = 0; u < roomHandler.rooms.Count; u++)
                {
                    for (int j = 0; j < 2; j++)
                        for (int k = 0; k < roomHandler.rooms[u].maxPlayers; k++)
                        {
                            if (roomHandler.rooms[u].users[j, k].connection == null)
                                continue;
                            if (roomHandler.rooms[u].users[j, k].pID == pID)
                            {
                                serverForU.server.SendMessage(outmsg, roomHandler.rooms[u].users[j, k].connection, NetDeliveryMethod.ReliableOrdered, 0);
                                break;
                            }
                        }

                    lock (roomHandler.rooms[u].spectatorData2)
                    {
                        for (int j = 0; j < roomHandler.rooms[u].spectatorData2.Count; j++)
                        {
                            if (roomHandler.rooms[u].spectatorData2[j].connection == null)
                                continue;
                            if (roomHandler.rooms[u].spectatorData2[j].pID == pID)
                            {
                                serverForU.server.SendMessage(outmsg, roomHandler.rooms[u].spectatorData2[j].connection, NetDeliveryMethod.ReliableOrdered, 0);
                                break;
                            }
                        }
                    }

                }
            }

        }

        //admin message
        void Packet86(NetIncomingMessage inmsg)
        {
            string message;

            try
            {
                message = inmsg.ReadString();
            }
            catch
            {
                return;
            }

            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)86);
            outmsg.Write(message);

            List<NetConnection> recipients = new List<NetConnection>();

            for (int i = 0; i < roomHandler.rooms.Count; i++)
            {
                for (int j = 0; j < 2; j++)
                    for (int k = 0; k < roomHandler.rooms[i].maxPlayers; k++)
                        if (roomHandler.rooms[i].users[j, k].connection != null && roomHandler.rooms[i].IsPlayerOnline(j, k))
                            recipients.Add(roomHandler.rooms[i].users[j, k].connection);

                lock (roomHandler.rooms[i].spectatorData2)
                    for (int j = 0; j < roomHandler.rooms[i].spectatorData2.Count; j++)
                        if (roomHandler.rooms[i].spectatorData2[j].connection != null)
                            recipients.Add(roomHandler.rooms[i].spectatorData2[j].connection);

            }

            if (recipients.Count > 0)
                serverForU.server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);
        }

        //MS inform GS about user, which needs to be kicked out from server
        void Packet87(NetIncomingMessage inmsg)
        {
            int bannedPID;

            try { bannedPID = inmsg.ReadInt32(); }
            catch { return; }

            lock (roomHandler.rooms)
            {
                for (int i = 0; i < roomHandler.rooms.Count; i++)
                {

                    for (int j = 0; j < 2; j++)
                        for (int k = 0; k < roomHandler.rooms[i].maxPlayers; k++)
                            if (roomHandler.rooms[i].users[j, k].pID == bannedPID)
                                roomHandler.DisconnectUser(roomHandler.rooms[i].users[j, k].connection, 3);

                }
            }
        }

        void AddSpectatorToServer(int selectedServerUniqueID, NetConnection netConnection, int pID, int tID, string username)
        {
            //lets check, that room still exists
            RoomData roomData = roomHandler.FindRoomByUniqueID(selectedServerUniqueID);
            if (roomData == null)
            {
                serverForU.SendInfoMsg(netConnection, 3);
                return;
            }

            lock (roomData.spectatorData2)
                roomData.spectatorData2.Add(new SpectatorData(netConnection, pID, tID, username));

            int uniquePID = 0;

            bool statsWindowVisible = false;
            if (roomData.autoMoving == 2)
                statsWindowVisible = true;

            //send data to client
            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)10);

            outmsg.Write(statsWindowVisible);
            outmsg.Write(roomData.botsEnabled);
            outmsg.Write(roomData.uniqueID);
            outmsg.Write(uniquePID);
            outmsg.Write((byte)roomData.roomState);
            outmsg.Write((byte)roomData.roomType);

            outmsg.Write(roomData.officiallyStarted);
            outmsg.Write(roomData.autostartTimeout);
            outmsg.Write(roomData.time);
            outmsg.Write(roomData.maxPlayers);
            outmsg.Write(roomData.homeSide);
            outmsg.Write(roomData.period);
            outmsg.Write(roomData.timerEnabled);
            //outmsg.Write(Proc.getSpectatorCount(sID));

            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(roomData.teams[i].score);
                outmsg.Write(roomData.teams[i].name);
                outmsg.Write(roomData.teams[i].shortName);
                outmsg.Write(roomData.teams[i].shirtStyle);
            }

            //kits colors
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 4; j++)
                    for (int k = 0; k < 3; k++)
                        outmsg.Write(roomData.teams[i].shirtColors[j, k]);

            serverForU.server.SendMessage(outmsg, netConnection, NetDeliveryMethod.ReliableOrdered, 0);

        }

        public void TryToAddUserToServer(int selectedServerUniqueID, NetConnection netConnection, int pID, int tID, int lockedTID, string username, string nation, bool isVip, byte admas, byte body, byte skin, byte hair, byte number/*, byte[] shoe*/)
        {

            //lets check, that room still exists
            RoomData roomData = roomHandler.FindRoomByUniqueID(selectedServerUniqueID);
            if (roomData == null)
            {
                serverForU.SendInfoMsg(netConnection, 3);
                return;
            }

            //check, that user is allowed to join this challenge
            if (roomData.roomType == RoomType.Challenge)
                if (roomData.teams[0].tID != tID && roomData.teams[1].tID != tID)
                    return;

            if (roomData.officiallyStarted)
                if (lockedTID > 0)
                    if (tID != lockedTID)
                    {
                        serverForU.SendInfoMsg(netConnection, 6);
                        return;
                    }


            //check, that room isnt full
            if (roomData.IsRoomFull(tID))
            {
                serverForU.SendInfoMsg(netConnection, 5);
                return;
            }

            if (roomData.IsUserAlreadyInServer(username))
            {
                Console.WriteLine("user already in server");
                return;
            }

            int uniquePID;

            uniquePID = roomData.AddPlayerToServer(netConnection, tID, pID, nation, username, isVip, admas, body, skin, hair, number/*, shoe.ToArray()*/);

            bool statsWindowVisible = false;
            if (roomData.autoMoving == 2)
                statsWindowVisible = true;

            //send data to client
            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)10);

            outmsg.Write(statsWindowVisible);
            outmsg.Write(roomData.botsEnabled);
            outmsg.Write(roomData.uniqueID);
            outmsg.Write(uniquePID);
            outmsg.Write((byte)roomData.roomState);
            outmsg.Write((byte)roomData.roomType);

            outmsg.Write(roomData.officiallyStarted);
            outmsg.Write(roomData.autostartTimeout);
            outmsg.Write(roomData.time);
            outmsg.Write(roomData.maxPlayers);
            outmsg.Write(roomData.homeSide);
            outmsg.Write(roomData.period);
            outmsg.Write(roomData.timerEnabled);
            //outmsg.Write(Proc.getSpectatorCount(sID));

            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(roomData.teams[i].score);
                outmsg.Write(roomData.teams[i].name);
                outmsg.Write(roomData.teams[i].shortName);
                outmsg.Write(roomData.teams[i].shirtStyle);
            }

            //kits colors
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 4; j++)
                    for (int k = 0; k < 3; k++)
                        outmsg.Write(roomData.teams[i].shirtColors[j, k]);

            serverForU.server.SendMessage(outmsg, netConnection, NetDeliveryMethod.ReliableOrdered, 0);

            roomData.BroadcastJoinerData(username, null, 1);
        }

        void ConnectedToMasterServer()
        {
            Console.WriteLine("connected to masterserver");
            //incase gameserver have reconnected and it contains rooms&players, inform about those here

            //incase reconnect, we should NOT start room handler 
            if (roomHandler == null)
                roomHandler = new RoomHandler(serverForU.server, client);
        }


    }
}
