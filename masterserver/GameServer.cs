using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace MasterServer
{
    class GameServer
    {
        public NetConnection netConnection;
        public int timeout = 0;

        public GameServer(NetConnection netConnection)
        {
            this.netConnection = netConnection;
        }
    }
}
