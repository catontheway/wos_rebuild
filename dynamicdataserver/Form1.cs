using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lidgren.Network;

namespace DatabaseServer
{
    public class Form1
    {
        public static int version = 42;
        ServerForMS serverForMS;
        ServerForUpd serverForUpd;
        public static Int64 secVal1 = 9223372026854775807;
        public static Int64 secVal2 = -9223372006854775808;

        public Form1()
        {
            serverForMS = new ServerForMS();
            serverForUpd = new ServerForUpd();
            serverForUpd.serverForMS = serverForMS;

            Timer timer1 = new Timer(TimerCallback1, null, 0, 10000);
            Timer timer2 = new Timer(TimerCallback2, null, 0, 30000);
        }

        void TimerCallback1(Object o)
        {
            //timeout masterservers
            /*lock (serverForMS.masterServers)
            {
                for (int i = serverForMS.masterServers.Count - 1; i >= 0; --i)
                {
                    serverForMS.masterServers[i].timeout++;

                    if (serverForMS.masterServers[i].timeout >= 3)
                    {
                        serverForMS.masterServers.RemoveAt(i);
                        Console.WriteLine("MS timeoutted");
                        break;
                    }
                }
            }*/


            //send rooms and online teams to masterservers every 10 seconds
            serverForMS.BroadcastRoomsAndOnlineTeamsToMS();
        }

        void TimerCallback2(Object o)
        {
            serverForMS.LeagueHandlerMessage_And_DeleteInactiveUsers();
        }

    }
}
