using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    class RankCalculation
    {
        public int[] rankChange = new int[2];

        public RankCalculation(int currentRank0, int currentRank1, int score0, int score1)
        {
            int[,] calculatedRanks = new int[2, 2];

            double r1, r2, r3;

            if (currentRank0 == currentRank1)
            {
                calculatedRanks[0, 0] = 260;
                calculatedRanks[0, 1] = 260;
                calculatedRanks[1, 0] = 0;
                calculatedRanks[1, 1] = 0;
            }
            else
            {
                if (currentRank0 > currentRank1)
                    r1 = (100f / currentRank0) * currentRank1;  
                else
                    r1 = (100f / currentRank1) * currentRank0;

                r2 = (100 - r1) * 2; 
                if (r2 > 100) r2 = 100;
                if (r2 > 0) r2 = r2 / 100f; 
                r3 = 250 * r2;

                if (currentRank0 > currentRank1)
                {
                    calculatedRanks[0, 0] = Convert.ToInt32(250 - Math.Round(r3) + 10);
                    calculatedRanks[0, 1] = Convert.ToInt32(250 + Math.Round(r3) + 10);
                    calculatedRanks[1, 0] = calculatedRanks[0, 0] - calculatedRanks[0, 1];
                    calculatedRanks[1, 1] = calculatedRanks[0, 1] - calculatedRanks[0, 0];
                    calculatedRanks[1, 0] = Convert.ToInt32(calculatedRanks[1, 0] / 2);
                    calculatedRanks[1, 1] = Convert.ToInt32(calculatedRanks[1, 1] / 2);
                }
                else
                {
                    calculatedRanks[0, 1] = Convert.ToInt32(250 - Math.Round(r3) + 10);
                    calculatedRanks[0, 0] = Convert.ToInt32(250 + Math.Round(r3) + 10);
                    calculatedRanks[1, 1] = calculatedRanks[0, 1] - calculatedRanks[0, 0];
                    calculatedRanks[1, 0] = calculatedRanks[0, 0] - calculatedRanks[0, 1];
                    calculatedRanks[1, 0] = Convert.ToInt32(calculatedRanks[1, 0] / 2);
                    calculatedRanks[1, 1] = Convert.ToInt32(calculatedRanks[1, 1] / 2);
                }
            }

            if (score0 > score1)
            {
                rankChange[0] = calculatedRanks[0, 0];
                rankChange[1] = 0 - calculatedRanks[0, 0];
            }
            if (score1 > score0)
            {
                rankChange[0] = 0 - calculatedRanks[0, 1];
                rankChange[1] = calculatedRanks[0, 1];
            }
            if (score0 == score1)
            {
                rankChange[0] = calculatedRanks[1, 0];
                rankChange[1] = calculatedRanks[1, 1];
            }
        }
    }
}
