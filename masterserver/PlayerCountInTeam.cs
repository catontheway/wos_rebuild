using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    class PlayerCountInTeam
    {
        public int tID;
        public byte plrCount;
        public bool isPlaying;

        public PlayerCountInTeam(int tID, byte plrCount, bool isPlaying)
        {
            this.tID = tID;
            this.plrCount = plrCount;
            this.isPlaying = isPlaying;
        }
    }
}
