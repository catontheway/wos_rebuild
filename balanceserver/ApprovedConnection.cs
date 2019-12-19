using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace BalanceServer
{
    class ApprovedConnection
    {
        public NetConnection netConnection;

        public ApprovedConnection(NetConnection netConnection)
        {
            this.netConnection = netConnection;
        }
    }
}
