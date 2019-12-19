using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Lidgren.Network;
using System.Threading;

namespace GameServerMono
{
    public class Form1
    {
        public static bool isLocal = false;
        public static int version = 42;

        ClientToBS clientToBS;
        ClientToMS clientToMS;
        ServerForU serverForU;

        public static string servername = "";

        public Form1()
        {
            /*while (true) {
				Thread.Sleep(1);
			}*/

            if (File.Exists(Environment.CurrentDirectory + "/local.txt"))
                isLocal = true;

            StreamReader sr = new StreamReader($"settings.txt");
            String line = sr.ReadToEnd();
            servername = line;

            serverForU = new ServerForU();
            clientToMS = new ClientToMS(serverForU);
            clientToBS = new ClientToBS(clientToMS);

            clientToMS.SetReferences(clientToBS);
            serverForU.SetReferences(clientToMS);
            clientToBS.serverForU = serverForU;

            Timer t = new Timer(TimerCallback, null, 0, 10000);

            //serverForU.SetReferences(clientToMS);
            //clientToBS.serverForU = serverForU;

        }

        void TimerCallback(Object o)
        {
            if (clientToBS.reconnectToBalanceServer)
            {
                Console.WriteLine("reconnecting to balanceserver...");
                clientToBS.reconnectToBalanceServer = false;
                clientToBS.client.Connect(clientToBS.ipToBalanceServer, 14242);
            }
        }
    }
}

