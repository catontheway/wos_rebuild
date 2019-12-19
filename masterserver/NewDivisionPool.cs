using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    class NewDivisionPool
    {
        public int location;
        public int division;

        public List<int> tIDs = new List<int>();
        public List<bool> settedToLeague = new List<bool>();

        public NewDivisionPool(int location, int division)
        {
            this.location = location;
            this.division = division;
        }

        public void AddTeam(int tID)
        {
            tIDs.Add(tID);
            settedToLeague.Add(false);
        }

        public bool IsEnoughBotsForFullBotDivision()
        {
            int botsFoundCount = 0;
            bool res = false;

            for (int i = 0; i < tIDs.Count; i++)
            {
                if (settedToLeague[i]) continue;
                if (tIDs[i] > 0) continue;

                botsFoundCount++;

                if (botsFoundCount == Form1.maxTeamsInDiv)
                {
                    res = true;
                    break;
                }
            }

            return res;
        }

        public void SetBotTeamsToLeague()
        {
            int botsFoundCount = 0;

            for (int i = 0; i < tIDs.Count; i++)
            {
                if (settedToLeague[i]) continue;
                if (tIDs[i] > 0) continue;

                botsFoundCount++;
                settedToLeague[i] = true;

                if (botsFoundCount == 12) break;
            }
        }
    }
}
