using System;
using System.Threading;

namespace BalanceServer
{
    public class Form1
    {
        public static int version = 42;
        public static int minAndroidVersion = 24;
        public static int minIOSVersion = 24;
        ServerForMS serverForMS;
        ServerForU_GS serverForU_GS;
        ServerForUpd serverForUpd;
        public static Int64 secVal1 = 9223372026854775807;
        public static Int64 secVal2 = -9223372006854775808;

        public Form1()
        {
            serverForMS = new ServerForMS();
            serverForU_GS = new ServerForU_GS();
            serverForUpd = new ServerForUpd();
            serverForUpd.serverForMS = serverForMS;
            serverForUpd.serverForU_GS = serverForU_GS;
            serverForMS.SetReferenceToServerForU_GS(serverForU_GS);
            serverForU_GS.SetReferenceToServerForMS(serverForMS);

            Timer t = new Timer(TimerCallback, null, 0, 2000);
        }

        void TimerCallback(Object o)
        {
            //timeout masterservers
            lock (serverForMS.masterServers)
            {
                for (int i = 0; i < serverForMS.masterServers.Count; i++)
                    serverForMS.masterServers[i].timeout++;
            }
        }

    }
}
