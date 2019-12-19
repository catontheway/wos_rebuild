using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace GameServerMono
{
    class SpectatorData
    {
        public NetConnection connection;
        public int pID = 0;     //database id
        public int tID = 0;     //database team id
        public string username = "";
        public bool udpTrafficEnabled;

        public SpectatorData(NetConnection connection, int pID, int tID, string username)
        {
            this.connection = connection;
            this.pID = pID;
            this.tID = tID;
            this.username = username;
            this.udpTrafficEnabled = false;
        }
    }
}
