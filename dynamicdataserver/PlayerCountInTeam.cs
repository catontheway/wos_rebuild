using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseServer
{
    class PlayerCountInTeam
    {
        public int tID;
        public byte count;
        public bool isPlaying = false;

        public PlayerCountInTeam(int tID)
        {
            this.tID = tID;
            count++;
        }
    }
}
