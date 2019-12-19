using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;
using System.Diagnostics;

namespace DatabaseServer
{
    class ServerForUpd
    {
        NetServer server;
        NetPeerConfiguration config;
        public ServerForMS serverForMS;
        public Thread thread;

        public ServerForUpd()
        {
            config = new NetPeerConfiguration("NSMobile");
            config.MaximumConnections = 10000;
            config.Port = 14248;
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

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)inmsg.ReadByte();

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.Data:

                            b = inmsg.ReadByte();

                            //updater client tells to start updater
                            if (b == 56)
                                Packet56(inmsg);

                            //maintenance server sends msg to DS
                            if (b == 83)
                                Packet83(inmsg);

                            //admin message
                            if (b == 86)
                                Packet86(inmsg);

                            //shutdown msg
                            if (b == 90)
                                Packet90(inmsg);

                            //addvip
                            if (b == 91)
                                Packet91(inmsg);

                            break;

                        //*************************************************************************

                        default:
                            Console.WriteLine("Unhandled type: " + inmsg.MessageType + " " + inmsg.LengthBytes + " bytes " + inmsg.DeliveryMethod + "|" + inmsg.SequenceChannel);
                            break;
                    }
                }

                Thread.Sleep(1);
            }
        }

        //updater client tells to start updater
        void Packet56(NetIncomingMessage inmsg)
        {
            Int64 secVal1;
            Int64 secVal2;

            try { secVal1 = inmsg.ReadInt64(); }
            catch { return; }
            try { secVal2 = inmsg.ReadInt64(); }
            catch { return; }

            if (secVal1 != Form1.secVal1) return;
            if (secVal2 != Form1.secVal2) return;

            server.Shutdown("");
            serverForMS.server.Shutdown("");

            Thread.Sleep(1000);

            Process.Start("Updater.exe");
        }

        //maintenance server sends msg to DS
        void Packet83(NetIncomingMessage inmsg)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)83);

            outmsg.Write(serverForMS.masterServers.Count);

            for (int i = 0; i < serverForMS.masterServers.Count; i++)
            {
                outmsg.Write(serverForMS.masterServers[i].netConnection.RemoteEndPoint.Address.ToString());
                outmsg.Write(serverForMS.masterServers[i].lobbyUsers.Count);

                outmsg.Write(serverForMS.masterServers[i].gameServers.Count);  //esim Dutch1, Dutch2, NY1, NY2 jne

                for (int j = 0; j < serverForMS.masterServers[i].gameServers.Count; j++) //esim Dutch1 (yksittäinen huone, ei koko serveri käsittelyssä)
                {
                    outmsg.Write(serverForMS.masterServers[i].gameServers[j].IP);
                    outmsg.Write(serverForMS.masterServers[i].gameServers[j].GetUserCount());
                    outmsg.Write(serverForMS.masterServers[i].gameServers[j].spectators.Count);
                }
            }

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
        }

        //admin message
        void Packet86(NetIncomingMessage inmsg)
        {
            string message;

            try { message = inmsg.ReadString(); }
            catch { return; }

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)86);

            outmsg.Write(message);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            lock (serverForMS.masterServers)
            {
                for (int t = 0; t < serverForMS.masterServers.Count; t++)
                {
                    if (serverForMS.masterServers[t].netConnection != null)
                    {
                        var another = server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        server.SendMessage(another, serverForMS.masterServers[t].netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                    }
                }
            }

        }

        //shutdown msg
        void Packet90(NetIncomingMessage inmsg)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)90);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            lock (serverForMS.masterServers)
            {
                for (int t = 0; t < serverForMS.masterServers.Count; t++)
                {
                    if (serverForMS.masterServers[t].netConnection != null)
                    {
                        var another = server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        server.SendMessage(another, serverForMS.masterServers[t].netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                    }
                }
            }

        }

        //addvip
        void Packet91(NetIncomingMessage inmsg)
        {
            string username;
            int days;

            try { username = inmsg.ReadString(); }
            catch { return; }
            try { days = inmsg.ReadInt32(); }
            catch { return; }

            lock (serverForMS.masterServers)
            {
                //lets send message to only 1 masterserver
                for (int i = 0; i < serverForMS.masterServers.Count; i++)

                    if (serverForMS.masterServers[i].netConnection != null)
                    {
                        NetOutgoingMessage outmsg = server.CreateMessage();
                        outmsg.Write((byte)91);

                        outmsg.Write(username);
                        outmsg.Write(days);

                        server.SendMessage(outmsg, serverForMS.masterServers[i].netConnection, NetDeliveryMethod.ReliableOrdered, 0);
                        return;
                    }
            }


        }


    }
}
