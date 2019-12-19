using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;

namespace BalanceServer
{
    class ServerForMS
    {
        public NetServer server;
        NetPeerConfiguration config;
        public List<Masterserver> masterServers = new List<Masterserver>();
        ServerForU_GS serverForU_GS;
        public Thread thread;

        public ServerForMS()
        {
            config = new NetPeerConfiguration("NSMobile");
            config.MaximumConnections = 10000;
            config.Port = 14241;
            config.ConnectionTimeout = 10;
            config.PingInterval = 3;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            server = new NetServer(config);
            thread = new Thread(new ThreadStart(Handler));
            server.Start();
            thread.Start();
        }

        public void SetReferenceToServerForU_GS(ServerForU_GS serverForU_GS)
        {
            this.serverForU_GS = serverForU_GS;
        }

        void Handler()
        {
            NetIncomingMessage inmsg;
            byte b;
            Masterserver ms = null;

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

                            lock (masterServers)
                            {
                                masterServers.Add(new Masterserver(inmsg.SenderConnection));
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

                            b = inmsg.ReadByte();

                            //masterserver informs, if its enabled for hosting
                            if (b == 1)
                                Packet1(inmsg, ms);

                            //ms requests version
                            if (b == 2)
                                Packet2(inmsg);

                            //masterserver sends connection count to balanceserver
                            if (b == 21)
                                Packet21(inmsg, ms);

                            //timeout message
                            if (b == 84)
                                Packet84(inmsg, ms);

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

        //masterserver informs, if its enabled for hosting
        void Packet1(NetIncomingMessage inmsg, Masterserver ms)
        {
            bool isEnabled;
            try { isEnabled = inmsg.ReadBoolean(); }
            catch { return; }

            ms.enabled = isEnabled;

            /*if (!ms.enabled)
            {
                lock (masterServers)
                    masterServers.RemoveAll(p => ms.netConnection == inmsg.SenderConnection);
            }*/
            AddText("masterserver enabled: " + isEnabled);
        }

        //MS requests version
        void Packet2(NetIncomingMessage inmsg)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)2);
            outmsg.Write(Form1.version);
            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
        }

        //masterserver sends connection count to balanceserver
        void Packet21(NetIncomingMessage inmsg, Masterserver ms)
        {
            int totalConnections;
            try { totalConnections = inmsg.ReadInt32(); }
            catch { return; }

            ms.timeout = 0;
            ms.totalConnections = totalConnections;
        }

        //timeout message
        void Packet84(NetIncomingMessage inmsg, Masterserver ms)
        {
            ms.timeout = 0;
        }

        public int GetMasterserverWithLeastConnections()
        {
            int masterserverID = -1;
            int lowestConnectionCount = Int32.MaxValue;

            //search, which server is least connections
            for (int i = 0; i < masterServers.Count; i++)
            {
                if (masterServers[i].timeout > 3) continue;
                if (!masterServers[i].enabled) continue;
                if (masterServers[i].totalConnections < lowestConnectionCount)
                {
                    lowestConnectionCount = masterServers[i].totalConnections;
                    masterserverID = i;
                }
            }

            return masterserverID;
        }

        public string GetMasterserverIP(int masterserverID)
        {
            string IP = "";

            try
            {
                IP = masterServers[masterserverID].netConnection.RemoteEndPoint.Address.ToString();
            }
            catch { }

            return IP;
        }

        void AddText(string s)
        {
            Console.WriteLine(s);
        }

    }
}
