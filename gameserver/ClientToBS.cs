using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;
using System.Diagnostics;

namespace GameServerMono
{
    class ClientToBS 
    {
        public string ipToBalanceServer = "188.166.30.215";
        string ipToMasterServer = "";
        NetPeerConfiguration config;
        public NetClient client;
        ClientToMS clientToMS;
        public ServerForU serverForU;
        public bool reconnectToBalanceServer;
        public bool attempReconnectToBalanceServer = true;

		public Thread thread;

		public ClientToBS(ClientToMS clientToMS)
        {            
			this.clientToMS = clientToMS;

            if (Form1.isLocal)
                ipToBalanceServer = "127.0.0.1";

            config = new NetPeerConfiguration("NSMobile");
            client = new NetClient(config);

			thread = new Thread(new ThreadStart(Handler));


            //client.RegisterReceivedCallback(new SendOrPostCallback(Handler));
            client.Start();
            client.Connect(ipToBalanceServer, 14242);
			thread.Start ();
        }

        public void SetReferences(ClientToMS clientToMS)
        {
            this.clientToMS = clientToMS;
        }

        void Handler()
        {
            NetIncomingMessage inmsg;
            byte b;

			while (true) {
				while ((inmsg = client.ReadMessage ()) != null) {
					switch (inmsg.MessageType) {
					case NetIncomingMessageType.DebugMessage:
					case NetIncomingMessageType.ErrorMessage:
					case NetIncomingMessageType.WarningMessage:
					case NetIncomingMessageType.VerboseDebugMessage:
                        //Console.WriteLine(inmsg.ReadString());
						break;

					//*************************************************************************

					case NetIncomingMessageType.StatusChanged:
						NetConnectionStatus status = (NetConnectionStatus)inmsg.ReadByte ();

						if (status == NetConnectionStatus.Connected)
							ConnectedToBalanceServer ();

						if (status == NetConnectionStatus.Disconnected && attempReconnectToBalanceServer) {
							reconnectToBalanceServer = true;
						}

						break;

					//*************************************************************************

					case NetIncomingMessageType.Data:
						if (inmsg.LengthBytes < 1)
							break;

						b = inmsg.ReadByte ();

                        //balanceserver tells, which masterserver user/gameserver needs to connect
						if (b == 2)
							Packet2 (inmsg);

                        //all masterservers were offline
						if (b == 3)
							Packet3 (inmsg);

						break;

					//*************************************************************************

					default:
						Console.WriteLine ("Unhandled type: " + inmsg.MessageType + " " + inmsg.LengthBytes + " bytes " + inmsg.DeliveryMethod + "|" + inmsg.SequenceChannel);
						break;
					}
					client.Recycle (inmsg);
				}

				Thread.Sleep (1);
			}
        }

        //balanceserver tells, which masterserver user/gameserver needs to connect
        void Packet2(NetIncomingMessage inmsg)
        {
            int version;
            int minAndroidVersion;
            int minIOSVersion;
            string _IPtoMS;

            try { version = inmsg.ReadInt32(); }
            catch { return; }
            try { minAndroidVersion = inmsg.ReadInt32(); }
            catch { return; }
            try { minIOSVersion = inmsg.ReadInt32(); }
            catch { return; }
            try { _IPtoMS = inmsg.ReadString(); }
            catch { return; }

            ipToMasterServer = _IPtoMS;

            attempReconnectToBalanceServer = false;
            client.Disconnect("");

            if (Form1.version != version)
            {
                client.Disconnect("");
                clientToMS.client.Disconnect("");
                serverForU.server.Shutdown("");

                Thread.Sleep(1000);

                //Process.Start("UpdaterGameServer.exe");
				Environment.Exit (0);
                return;
            }

            clientToMS.client.Connect(ipToMasterServer, 14243);
        }

        //all masterservers were offline
        void Packet3(NetIncomingMessage inmsg)
        {
			Console.WriteLine("all masterservers are offline");
            client.Disconnect("");
            //Thread.Sleep(5000);
            //clientToBS.Connect(ipToBalanceServer, 14242);
            reconnectToBalanceServer = true;
        }

        void ConnectedToBalanceServer()
        {
            NetOutgoingMessage outmsg = client.CreateMessage();
            outmsg.Write((byte)2);
            client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }


    }
}
