using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;

namespace GameServerMono
{
    class ServerForU
    {
        NetPeerConfiguration config;
        public NetServer server;
        public List<UserConnection> userConnections = new List<UserConnection>();
        ClientToMS clientToMS;

        public Thread thread;

        public ServerForU()
        {
            config = new NetPeerConfiguration("NSMobile");
            config.MaximumConnections = 1000;
            config.Port = 14245;
            config.ConnectionTimeout = 10;
            config.PingInterval = 3;
            config.UseMessageRecycling = true;
            config.DisableMessageType(NetIncomingMessageType.DebugMessage);
            config.DisableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            config.DisableMessageType(NetIncomingMessageType.DiscoveryRequest);
            config.DisableMessageType(NetIncomingMessageType.DiscoveryResponse);
            config.DisableMessageType(NetIncomingMessageType.Error);
            config.DisableMessageType(NetIncomingMessageType.ErrorMessage);
            config.DisableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
            config.DisableMessageType(NetIncomingMessageType.Receipt);
            config.DisableMessageType(NetIncomingMessageType.UnconnectedData);
            config.DisableMessageType(NetIncomingMessageType.WarningMessage);
            config.DisableMessageType(NetIncomingMessageType.VerboseDebugMessage);
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            server = new NetServer(config);
            thread = new Thread(new ThreadStart(Handler));

            //server.RegisterReceivedCallback(new SendOrPostCallback(Handler));
            server.Start();
            thread.Start();
        }

        public void SetReferences(ClientToMS clientToMS)
        {
            this.clientToMS = clientToMS;
        }

        void Handler()
        {
            NetIncomingMessage inmsg;
            byte b;

            while (true)
            {
                while ((inmsg = server.ReadMessage()) != null)
                {
                    switch (inmsg.MessageType)
                    {
                        //case NetIncomingMessageType.DebugMessage:
                        //case NetIncomingMessageType.ErrorMessage:
                        //case NetIncomingMessageType.WarningMessage:
                        //case NetIncomingMessageType.VerboseDebugMessage:
                        //Console.WriteLine(inmsg.ReadString());
                        //    break;


                        //*************************************************************************

                        case NetIncomingMessageType.ConnectionApproval:
                            inmsg.SenderConnection.Approve();

                            lock (userConnections)
                            {
                                userConnections.Add(new UserConnection(inmsg.SenderConnection));
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)inmsg.ReadByte();

                            if (status == NetConnectionStatus.Disconnected)
                            {
                                clientToMS.roomHandler.DisconnectUser(inmsg.SenderConnection, 2);

                                lock (userConnections)
                                {
                                    userConnections.RemoveAll(p => p.netConnection == inmsg.SenderConnection);
                                }
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.Data:
                            if (userConnections.Find(p => p.netConnection == inmsg.SenderConnection) == null)
                                break;
                            if (inmsg.LengthBytes < 1)
                                break;

                            b = inmsg.ReadByte();

                            //user have sent data after connecting
                            if (b == 9)
                                Packet9(inmsg);

                            //user sends control data
                            if (b == 11)
                                Packet11(inmsg);

                            //user clicks ready button
                            if (b == 12)
                                Packet12(inmsg);

                            //user clicks changeteam button
                            if (b == 13)
                                Packet13(inmsg);

                            //user requests broadcast about players
                            if (b == 17)
                                Packet17(inmsg);

                            //chat message
                            if (b == 45)
                                Packet45(inmsg);

                            //test message
                            if (b == 59)
                                Packet59(inmsg);

                            //user sends control data
                            if (b == 82)
                                Packet82(inmsg);

                            break;

                        //*************************************************************************

                        default:
                            //Console.WriteLine("Unhandled type: " + inmsg.MessageType + " " + inmsg.LengthBytes + " bytes " + inmsg.DeliveryMethod + "|" + inmsg.SequenceChannel);
                            break;
                    }
                    server.Recycle(inmsg);
                }

                Thread.Sleep(1);
            }
        }

        //user have sent data after connecting
        void Packet9(NetIncomingMessage inmsg)
        {
            int pID = 0;
            int selectedServerUniqueID = 0;
            int uniqueID = 0;
            bool isSpectator;

            try { selectedServerUniqueID = inmsg.ReadInt32(); }
            catch { return; }
            try { pID = inmsg.ReadInt32(); }
            catch { return; }
            try { uniqueID = inmsg.ReadInt32(); }
            catch { return; }
            try { isSpectator = inmsg.ReadBoolean(); }
            catch { return; }

            //check, that room exists
            RoomData r = clientToMS.roomHandler.rooms.Find(p => p.uniqueID == selectedServerUniqueID);
            //room doesnt exists
            if (r == null)
            {
                SendInfoMsg(inmsg.SenderConnection, 3);
                return;
            }

            if (!isSpectator)
                if (r.IsRoomFull())
                {
                    SendInfoMsg(inmsg.SenderConnection, 5);
                    return;
                }

            long GSUniqueIdentifier = inmsg.SenderConnection.RemoteUniqueIdentifier;

            //*******

            //lets verify user
            NetOutgoingMessage outmsg = clientToMS.client.CreateMessage();
            outmsg.Write((byte)9);

            //with pID&uniqueID, we can verify user and get his data (username&body&vip etc)
            //with GSUniqueIdentifier we know, which user we will respond, if join to room were succesful

            outmsg.Write(pID);
            outmsg.Write(selectedServerUniqueID);
            outmsg.Write(uniqueID);
            outmsg.Write(GSUniqueIdentifier);
            outmsg.Write(isSpectator);

            clientToMS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);


        }

        //user sends control data
        void Packet11(NetIncomingMessage inmsg)
        {
            CurrentUser user = GetCurrentUser(inmsg.SenderConnection);
            if (user.sID == -1) return;

            RoomData room = clientToMS.roomHandler.rooms[user.sID];

            bool isComputer;
            double[] controlDir = new double[2];
            double controlDistance;
            byte buttons;

            try { isComputer = inmsg.ReadBoolean(); }
            catch { return; }
            try { controlDir[0] = (double)inmsg.ReadInt16() / 30000; }
            catch { return; }
            try { controlDir[1] = (double)inmsg.ReadInt16() / 30000; }
            catch { return; }
            try { controlDistance = (double)inmsg.ReadInt16() / 10; }
            catch { return; }
            try { buttons = inmsg.ReadByte(); }  //buttons
            catch { return; }

            if (isComputer)
                room.users[user.tID, user.pID].controlMethod = ControlMethod.Mouse;   //mouse
            else
                room.users[user.tID, user.pID].controlMethod = ControlMethod.Mobile;   //mobile

            for (int i = 0; i < 2; i++)
                room.users[user.tID, user.pID].controlDir[i] = controlDir[i];
            room.users[user.tID, user.pID].controlDistance = controlDistance;

            room.users[user.tID, user.pID].SetButtons(buttons);

            room.users[user.tID, user.pID].timeout = NetTime.Now;
            Console.WriteLine("11e");
        }

        //user clicks ready button
        void Packet12(NetIncomingMessage inmsg)
        {
            CurrentUser user = GetCurrentUser(inmsg.SenderConnection);
            if (user.sID == -1) return;

            RoomData room = clientToMS.roomHandler.rooms[user.sID];

            if (room.roomState != RoomState.ReadyScreen) return;

            //lets swap ready status
            if (room.users[user.tID, user.pID].ready)
                room.users[user.tID, user.pID].ready = false;
            else
                room.users[user.tID, user.pID].ready = true;

            //lets check if all have clicked ready
            if (CheckIfAllIsReady(room))
            {
                room.StartGame();
                return;  //game begins, so we dont need to broadcast ready changes
            }

            //lets broadcast ready changes
            BroadcastReadyChanges(room);
        }

        //user clicks changeteam button
        void Packet13(NetIncomingMessage inmsg)
        {
            CurrentUser user = GetCurrentUser(inmsg.SenderConnection);
            if (user.sID == -1) return;

            RoomData room = clientToMS.roomHandler.rooms[user.sID];
            if (room.autoMoving > 0) return;
            if (room.roomType != RoomType.Public) return;
            if (room.roomState == RoomState.ReadyScreen && room.users[user.tID, user.pID].ready) return;

            byte count = 0;
            int wantedTeam;
            if (user.tID == 0) wantedTeam = 1; else wantedTeam = 0;

            //lets check, if there is room in another team
            for (int i = 0; i < room.maxPlayers; i++)
                if (room.IsPlayerOnline(wantedTeam, i))
                    count++;

            if (count == room.maxPlayers) return; //team is full

            UserData u = room.users[user.tID, user.pID];

            //find empty slot, and set player to there
            for (int i = 0; i < room.maxPlayers; i++)
                if (!room.IsPlayerOnline(wantedTeam, i))
                {
                    room.users[wantedTeam, i] = u;

                    room.users[wantedTeam, i].coords[0] = Field.BENCHX;
                    room.users[wantedTeam, i].coords[1] = 0;
                    room.users[wantedTeam, i].pos = room.GetAnyAvailablePos(wantedTeam, false);
                    break;
                }
            //reset previous slot
            room.users[user.tID, user.pID] = new UserData(room.roomType);
            //get position for bot
            if (room.botsEnabled)
                room.users[user.tID, user.pID].pos = room.GetAnyAvailablePos(user.tID, true);

            room.BroadcastJoinerData("", null, 0);
        }

        //user requests broadcast about players
        void Packet17(NetIncomingMessage inmsg)
        {
            bool isSpectator;

            try { isSpectator = inmsg.ReadBoolean(); }
            catch { return; }

            CurrentUser user;

            if (isSpectator)
            {
                user = GetCurrentSpectator(inmsg.SenderConnection);
                if (user.sID == -1) return;
            }
            else
            {
                user = GetCurrentUser(inmsg.SenderConnection);
                if (user.sID == -1) return;
            }

            RoomData room = clientToMS.roomHandler.rooms[user.sID];

            if (!isSpectator)
                room.users[user.tID, user.pID].udpTrafficEnabled = true;
            else
            {
                lock (room.spectatorData2)
                {
                    try { room.spectatorData2[user.pID].udpTrafficEnabled = true; }
                    catch { return; }
                }
            }


            room.BroadcastJoinerData("", inmsg.SenderConnection, 0);
        }

        //chat message
        void Packet45(NetIncomingMessage inmsg)
        {
            bool isSpectator;

            try { isSpectator = inmsg.ReadBoolean(); }
            catch { return; }

            CurrentUser user;

            if (isSpectator)
            {
                user = GetCurrentSpectator(inmsg.SenderConnection);
                if (user.sID == -1) return;
            }
            else
            {
                user = GetCurrentUser(inmsg.SenderConnection);
                if (user.sID == -1) return;
            }

            RoomData room = clientToMS.roomHandler.rooms[user.sID];

            byte receiverType;  //0=user, 1=team, 2=ingame all, 3=ingame team
            string receiver;   //this is empty, if message is "ingame" message (receiverType 2 or 3)
            string message;

            try { receiverType = inmsg.ReadByte(); }
            catch { return; }
            try { receiver = inmsg.ReadString(); }
            catch { return; }
            try { message = inmsg.ReadString(); }
            catch { return; }

            if (receiverType > 3) return;
            if (receiverType == 0 || receiverType == 1)
                if (receiver == "") return;

            string _testText = message.ToLower();

            if (_testText.IndexOf("netsoccer") > -1) return;
            if (_testText.IndexOf("n e t s o c c e r") > -1) return;

            #region ingame message
            if (receiverType == 2 || receiverType == 3)
            {
                NetOutgoingMessage outmsg = server.CreateMessage();
                outmsg.Write((byte)45);

                outmsg.Write(receiverType);
                outmsg.Write(message);
                outmsg.Write(room.users[user.tID, user.pID].username);
                outmsg.Write(""); //teamname not required, so lets write empty string

                var saved = new byte[outmsg.LengthBytes];
                Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
                var savedBitLength = outmsg.LengthBits;

                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < room.maxPlayers; j++)
                    {
                        if (i == user.tID && j == user.pID) continue;  //dont send back to sender
                        if (room.users[i, j].connection == null) continue;
                        if (receiverType == 3)
                            if (user.tID != i) continue;

                        var another = server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        server.SendMessage(another, room.users[i, j].connection, NetDeliveryMethod.ReliableOrdered, 3);
                    }
            }
            #endregion

            //re-send message to masterserver
            if (receiverType == 0 || receiverType == 1)
            {
                NetOutgoingMessage outmsg = clientToMS.client.CreateMessage();
                outmsg.Write((byte)45);

                outmsg.Write(receiverType);
                outmsg.Write(receiver);
                outmsg.Write(message);

                // try { selectedServerUniqueID = inmsg.ReadInt32(); }
                // catch { return; }

                if (isSpectator)
                {
                    lock (room.spectatorData2)
                    {
                        try { outmsg.Write(room.spectatorData2[user.pID].pID); }
                        catch { return; }
                        try { outmsg.Write(room.spectatorData2[user.pID].tID); }
                        catch { return; }
                    }
                }
                else
                {
                    outmsg.Write(room.users[user.tID, user.pID].pID);
                    outmsg.Write(room.users[user.tID, user.pID].tID);
                }

                clientToMS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
            }

        }

        //test message
        void Packet59(NetIncomingMessage inmsg)
        {
            CurrentUser user = GetCurrentUser(inmsg.SenderConnection);
            if (user.sID == -1) return;

            RoomData room = clientToMS.roomHandler.rooms[user.sID];

            double[] d = new double[2];
            int i;

            try { d[0] = (double)inmsg.ReadInt16() / 3000; }
            catch { return; }
            try { d[1] = (double)inmsg.ReadInt16() / 3000; }
            catch { return; }
            try { i = inmsg.ReadInt32(); }
            catch { return; }

            room.ball.speed = 0;
            room.ball.zSpeed = 0;
            room.ball.height = 0;
            room.ball.coords[0] = d[0];
            room.ball.coords[1] = d[1];

            room.ball.CalculateKickA(150, i, 1, true, false);
        }

        //user sends control data
        void Packet82(NetIncomingMessage inmsg)
        {
            CurrentUser user = GetCurrentUser(inmsg.SenderConnection);
            if (user.sID == -1) return;

            RoomData room = clientToMS.roomHandler.rooms[user.sID];

            byte controlMethod;  //0=mobile, 1=joystick, 2=mouse, 3=mouse+keyboard
            double[] controlDir = new double[2];
            double controlDistance;
            byte buttons;
            byte keyboardButton;

            try { controlMethod = inmsg.ReadByte(); }
            catch { return; }
            try { controlDir[0] = (double)inmsg.ReadInt16() / 30000; }
            catch { return; }
            try { controlDir[1] = (double)inmsg.ReadInt16() / 30000; }
            catch { return; }
            try { controlDistance = (double)inmsg.ReadInt16() / 10; }
            catch { return; }
            try { buttons = inmsg.ReadByte(); }  //buttons
            catch { return; }
            try { keyboardButton = inmsg.ReadByte(); }
            catch { return; }

            room.users[user.tID, user.pID].controlMethod = (ControlMethod)controlMethod;

            for (int i = 0; i < 2; i++)
                room.users[user.tID, user.pID].controlDir[i] = controlDir[i];
            room.users[user.tID, user.pID].controlDistance = controlDistance;

            room.users[user.tID, user.pID].SetButtons(buttons);
            room.users[user.tID, user.pID].keyboardButton = keyboardButton;

            room.users[user.tID, user.pID].timeout = NetTime.Now;

        }

        public void SendInfoMsg(NetConnection connection, byte infoType)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)5);
            outmsg.Write(infoType);
            server.SendMessage(outmsg, connection, NetDeliveryMethod.ReliableOrdered, 0);
        }

        CurrentUser GetCurrentUser(NetConnection connection)
        {
            CurrentUser res = new CurrentUser();

            for (int i = 0; i < clientToMS.roomHandler.rooms.Count; i++)
                for (int j = 0; j < 2; j++)
                    for (int k = 0; k < clientToMS.roomHandler.rooms[i].maxPlayers; k++)
                        if (clientToMS.roomHandler.rooms[i].users[j, k].connection == connection)
                        {
                            res.sID = i;
                            res.tID = j;
                            res.pID = k;
                            return res;
                        }

            return res;
        }

        CurrentUser GetCurrentSpectator(NetConnection connection)
        {
            CurrentUser res = new CurrentUser();

            for (int i = 0; i < clientToMS.roomHandler.rooms.Count; i++)
                lock (clientToMS.roomHandler.rooms[i].spectatorData2)
                {
                    for (int j = 0; j < clientToMS.roomHandler.rooms[i].spectatorData2.Count; j++)
                        if (clientToMS.roomHandler.rooms[i].spectatorData2[j].connection == connection)
                        {
                            res.sID = i;
                            res.pID = j;
                            return res;
                        }
                }


            return res;
        }

        bool CheckIfAllIsReady(RoomData room)
        {
            int count = 0;
            bool serverEmpty = true;
            bool[] least1Ready = new bool[2];

            #region training server
            if (room.roomType == RoomType.Public)
            {
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < room.maxPlayers; j++)
                    {
                        if (room.IsPlayerOnline(i, j) && room.users[i, j].ready) count++;
                        if (room.users[i, j].pID == 0) count++;
                    }

                if (room.maxPlayers * 2 > count) return false;
            }
            #endregion

            #region challenge (no bots)
            if (room.roomType == RoomType.Challenge && !room.botsEnabled)
            {
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < room.maxPlayers; j++)
                        if (room.IsPlayerOnline(i, j) && room.users[i, j].ready) count++;


                if (room.maxPlayers * 2 > count) return false;
            }
            #endregion

            #region challenge (bots enabled)
            if (room.roomType == RoomType.Challenge && room.botsEnabled)
            {
                //check, that both teams have LEAST 1 ready
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < room.maxPlayers; j++)
                        if (room.IsPlayerOnline(i, j) && room.users[i, j].ready)
                            least1Ready[i] = true;

                if (!least1Ready[0]) return false;
                if (!least1Ready[1]) return false;

                //lets check, that all players in server have ready (bots are counted as ready)
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < room.maxPlayers; j++)
                    {
                        if (room.IsPlayerOnline(i, j) && room.users[i, j].ready) count++;
                        if (room.users[i, j].pID == 0) count++;
                    }

                if (room.maxPlayers * 2 > count) return false;

            }
            #endregion


            //if server is completely empty, lets not start game
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < room.maxPlayers; j++)
                    if (room.IsPlayerOnline(i, j)) serverEmpty = false;

            if (serverEmpty) return false;

            return true;
        }

        //säilytin alkuperäsen backuppina
        /* bool CheckIfAllIsReady(RoomData room)
         {
             int count = 0;
             bool serverEmpty = true;
             bool[] least1Ready = new bool[2];

             //training server
             if (room.roomType == RoomType.Public)
                 for (int i = 0; i < 2; i++)
                     for (int j = 0; j < room.maxPlayers; j++)
                     {
                         if (room.IsPlayerOnline(i, j) && room.users[i, j].ready) count++;
                         if (room.users[i, j].pID == 0) count++;
                     }

             //challenge (original, where every player needs to be ready)
             //if (room.roomType == RoomType.Challenge)
            //     for (int i = 0; i < 2; i++)
             //        for (int j = 0; j < room.maxPlayers; j++)
             //            if (room.IsPlayerOnline(i, j) && room.users[i, j].ready) count++;

             bool[] isAdminOnline = new bool[2];

             //challenge
             if (room.roomType == RoomType.Challenge)
             {
                 //normal challenges (also manually started league matches) needs to have admin or higher online.
                 //auto started league matches doents require admin
                 if (room.officiallyStarted)
                     for (int i = 0; i < 2; i++)
                         isAdminOnline[i] = true;

                 //check, if admin or higher is online
                 for (int i = 0; i < 2; i++)
                     for (int j = 0; j < room.maxPlayers; j++)
                         if (room.users[i, j].admas > 0)
                             isAdminOnline[i] = true;

                 int plrsInServer = 0;

                 //count players in server
                 for (int i = 0; i < 2; i++)
                     for (int j = 0; j < room.maxPlayers; j++)
                         if (room.IsPlayerOnline(i, j))
                             plrsInServer++;

                 for (int i = 0; i < 2; i++)
                 {
                     for (int j = 0; j < room.maxPlayers; j++)
                         if (room.IsPlayerOnline(i, j) && room.users[i, j].ready)
                         {
                             count++;
                             least1Ready[i] = true;
                         }
                     //Console.WriteLine(i+" "+room.teams[i].isOfficialLeagueBotTeam);
                     if (room.teams[i].isOfficialLeagueBotTeam) least1Ready[i] = true;
                 }

                 //if bots enabled, its enough that both teams have least 1 ready
                 if (room.botsEnabled && least1Ready[0] && least1Ready[1] && count >= plrsInServer)
                     count = room.maxPlayers * 2;

                 if (!isAdminOnline[0]) return false;
                 if (!isAdminOnline[1]) return false;
             }


             if (room.maxPlayers * 2 > count) return false;

             //if server is completely empty, lets not start game
             for (int i = 0; i < 2; i++)
                 for (int j = 0; j < room.maxPlayers; j++)
                     if (room.IsPlayerOnline(i, j)) serverEmpty = false;

             if (serverEmpty) return false;

             return true;
         }*/


        void BroadcastReadyChanges(RoomData room)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)12);

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < room.maxPlayers; j++)
                    outmsg.Write(room.users[i, j].ready);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < room.maxPlayers; j++)
                    if (room.users[i, j].connection != null && room.IsPlayerOnline(i, j))
                    {
                        var another = server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        server.SendMessage(another, room.users[i, j].connection, NetDeliveryMethod.ReliableOrdered, 3);
                    }

            lock (room.spectatorData2)
            {
                for (int i = 0; i < room.spectatorData2.Count; i++)
                    if (room.spectatorData2[i].connection != null)
                    {
                        var another = server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        server.SendMessage(another, room.spectatorData2[i].connection, NetDeliveryMethod.ReliableOrdered, 3);
                    }
            }

        }

    }
}
