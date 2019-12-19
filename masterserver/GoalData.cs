using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    class GoalData
    {
        public int scorer;  //db id
        public int assister;  //db id
        public byte goalTime;
        public byte teamScored; //contain 0 or 1 (home/away)
        public bool bothTeamsHave5Players;

        public GoalData(int scorer, int assister, byte goalTime, byte teamScored, bool bothTeamsHave5Players)
        {
            this.scorer = scorer;
            this.assister = assister;
            this.goalTime = goalTime;
            this.teamScored = teamScored;
            this.bothTeamsHave5Players = bothTeamsHave5Players;
        }
    }
}
