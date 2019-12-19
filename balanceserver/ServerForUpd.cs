using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;
using System.Diagnostics;

namespace BalanceServer
{
    class ServerForUpd
    {
        NetServer server;
        NetPeerConfiguration config;
        public ServerForU_GS serverForU_GS;
        public ServerForMS serverForMS;
        public Thread thread;

        public ServerForUpd()
        {
            config = new NetPeerConfiguration("NSMobile");
            config.MaximumConnections = 10000;
            config.Port = 14247;
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
                            Console.WriteLine(inmsg.ReadString());
                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.ConnectionApproval:
                            inmsg.SenderConnection.Approve();

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.Data:

                            b = inmsg.ReadByte();

                            //updater client tells to start updater
                            if (b == 56)
                                Packet56(inmsg);

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
            serverForU_GS.server.Shutdown("");
            serverForMS.server.Shutdown("");

            Thread.Sleep(1000);

            Process.Start("Updater.exe");
        }

    }
}
