using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerMono
{
    class CurrentUser
    {
        public int sID = -1;
        public int pID = -1;    //important!!! this is not vipID, its 0 - maxPlayers (10)
        public int tID = -1;

        public CurrentUser()
        {

        }
    }
}
