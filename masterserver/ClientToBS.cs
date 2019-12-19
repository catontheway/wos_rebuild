using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Diagnostics;

namespace MasterServer
{
    class ClientToBS : BaseStuff
    {
        public string ipToBalanceServer = "188.166.30.215";
        public NetClient client;
        NetPeerConfiguration config;
        public ClientToDS clientToDS;
        public ServerForGS serverForGS;
        public ServerForU serverForU;
        public Thread thread;

        public ClientToBS()
        {
            if (Form1.isLocal)
                ipToBalanceServer = "127.0.0.1";

            config = new NetPeerConfiguration("NSMobile");
            client = new NetClient(config);
            thread = new Thread(new ThreadStart(Handler));
            client.Start();
            client.Connect(ipToBalanceServer, 14241);
            thread.Start();
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
                                ConnectedToBalanceServer();

                            if (status == NetConnectionStatus.Disconnected)
                            {
                                Form1.Timer2Enabled = true;
                                AddText("Disconnected from BS");
                                //client.Connect(ipToBalanceServer, 14241);
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.Data:
                            if (inmsg.LengthBytes < 1) break;

                            b = inmsg.ReadByte();

                            //BS sends version to MS
                            if (b == 2)
                                Packet2(inmsg);

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

        //BS sends version to MS
        void Packet2(NetIncomingMessage inmsg)
        {
            int version;

            try { version = inmsg.ReadInt32(); }
            catch { return; }


            if (Form1.version != version)
            {
                client.Disconnect("");
                clientToDS.client.Disconnect("");
                serverForGS.server.Shutdown("");
                serverForU.server.Shutdown("");

                Thread.Sleep(1000);

                Process.Start("Updater.exe");
                return;
            }

            AddText("version ok");

            if (clientToDS.client.ConnectionStatus == NetConnectionStatus.Connected)
                InformAboutEnabled(true);

        }

        void ConnectedToBalanceServer()
        {
            AddText("connected to BalanceServer");

            NetOutgoingMessage outmsg = client.CreateMessage();
            outmsg.Write((byte)2);
            client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void InformAboutEnabled(bool isEnabled)
        {
            if (client.ConnectionStatus != NetConnectionStatus.Connected) return;

            NetOutgoingMessage outmsg = client.CreateMessage();
            outmsg.Write((byte)1);
            outmsg.Write(isEnabled);
            client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void SendConnectionCountToBS(int connectionsCount)
        {
            NetOutgoingMessage outmsg = client.CreateMessage();
            outmsg.Write((byte)21);
            outmsg.Write(connectionsCount);
            client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void SendTimeoutMSG()
        {
            NetOutgoingMessage outmsg = client.CreateMessage();
            outmsg.Write((byte)84);
            client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

    }
}
