using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;

namespace BalanceServer
{
    class ServerForU_GS
    {
        public NetServer server;
        NetPeerConfiguration config;
        List<ApprovedConnection> approvedConnections = new List<ApprovedConnection>();
        ServerForMS serverForMS;
        public Thread thread;

        public ServerForU_GS()
        {
            config = new NetPeerConfiguration("NSMobile");
            config.MaximumConnections = 10000;
            config.Port = 14242;
            config.ConnectionTimeout = 10;
            config.PingInterval = 3;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            server = new NetServer(config);
            thread = new Thread(new ThreadStart(Handler));
            server.Start();
            thread.Start();
        }

        public void SetReferenceToServerForMS(ServerForMS serverForMS)
        {
            this.serverForMS = serverForMS;
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
                        //DiscoveryRequest is used only in local testing (when need to get IP 192.168.1.xxx) 
                        case NetIncomingMessageType.DiscoveryRequest:

                            NetOutgoingMessage response = server.CreateMessage();
                            server.SendDiscoveryResponse(response, inmsg.SenderEndPoint);
                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.ConnectionApproval:
                            inmsg.SenderConnection.Approve();

                            lock (approvedConnections)
                            {
                                approvedConnections.Add(new ApprovedConnection(inmsg.SenderConnection));
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)inmsg.ReadByte();

                            if (status == NetConnectionStatus.Disconnected)
                            {
                                lock (approvedConnections)
                                    approvedConnections.RemoveAll(p => p.netConnection == inmsg.SenderConnection);
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.Data:
                            if (approvedConnections.Find(p => p.netConnection == inmsg.SenderConnection) == null) break;
                            if (inmsg.LengthBytes < 1) break;

                            b = inmsg.ReadByte();

                            //user/gameserver have connected BS and are asking, which masterserver they should connect
                            if (b == 2)
                                Packet2(inmsg);

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

        //user/gameserver have connected BS and are asking, which masterserver they should connect
        void Packet2(NetIncomingMessage inmsg)
        {
            int masterserverID = serverForMS.GetMasterserverWithLeastConnections();

            NetOutgoingMessage outmsg = server.CreateMessage();

            if (masterserverID > -1)
            {
                outmsg.Write((byte)2);
                outmsg.Write(Form1.version);
                outmsg.Write(Form1.minAndroidVersion);
                outmsg.Write(Form1.minIOSVersion);
                outmsg.Write(serverForMS.GetMasterserverIP(masterserverID));
                server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            }
            //all masterservers are offline
            else
            {
                outmsg.Write((byte)3);
                server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            }

        }

        void AddText(string s)
        {
            Console.WriteLine(s);
        }

    }
}
