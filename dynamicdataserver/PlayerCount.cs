using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseServer
{
    class PlayerCount
    {
        public int mID = -1;  //masterserver id in array
        public string IP;
        public int plrCount;
        public byte location;

        public PlayerCount(int mID, string IP, int plrCount, byte location)
        {
            this.mID = mID;
            this.IP = IP;
            this.plrCount = plrCount;
            this.location = location;
        }

    }
}
