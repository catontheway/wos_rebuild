using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace BalanceServer
{
    class Masterserver
    {
        public NetConnection netConnection;
        public int totalConnections = 0;
        public bool enabled;    //when MS have connected to DS&BS, it will send message, that its enabled for hosting
        public int timeout = 0;

        public Masterserver(NetConnection netConnection)
        {
            enabled = false;
            this.netConnection = netConnection;
        }
    }
}
