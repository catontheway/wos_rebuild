using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace MasterServer
{
    class UserConnection
    {
        public NetConnection netConnection;
        public int pID = 0;
        public int tID = 0;
        public string username = "";
        public byte platform = 0;
        public int version = 0;

        public UserConnection(NetConnection netConnection)
        {
            this.netConnection = netConnection;
        }
    }
}
