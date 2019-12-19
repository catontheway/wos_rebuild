using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerMono
{
    class ChallengePlayerData
    {
        public int pID;
        public byte tID;   //contain 0 or 1 (home/away)
        public byte timePlayed = 0;
        public byte shotsTotal = 0;
        public byte shotsOnTarget = 0;
        public byte offsides = 0;
        public int posUP = 0;
        public int posDown = 0;
        public int posLeft = 0;
        public int posRight = 0;
        public int teamGoals = 0;


        public ChallengePlayerData(byte tID, int pID)
        {
            this.pID = pID;
            this.tID = tID;
        }
    }
}
