using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace DatabaseServer
{
    class MasterServer
    {
        public NetConnection netConnection;
        public List<GameServer> gameServers = new List<GameServer>(); //mainly these are rooms, not whole gameservers
        public List<User> lobbyUsers = new List<User>();
        public int timeout=0;

        public MasterServer(NetConnection netConnection)
        {
            this.netConnection = netConnection;
        }
        
    }
}
