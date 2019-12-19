using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace GameServerMono
{
    class UserConnection
    {
        public NetConnection netConnection;

        public UserConnection(NetConnection netConnection)
        {
            this.netConnection = netConnection;
        }
    }
}
