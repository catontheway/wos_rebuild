using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    class PromotionPool
    {
        public int location;
        public int division; //division to promote
        public int teamsToPromoteCount;

        public List<int> tIDs = new List<int>();
        public List<int> gf = new List<int>();
        public List<int> ga = new List<int>();
        public List<int> pts = new List<int>();

        //public List<bool> isPromoted = new List<bool>();
        
        public PromotionPool(int location, int division)
        {
            this.location = location;
            this.division = division;
        }

        public void SortPromotionPool()
        {
            #region count sorting points

            int[] competitionSortingPoints = new int[tIDs.Count];

            for (int i = 0; i < tIDs.Count; i++)
                for (int j = i + 1; j < tIDs.Count; j++)
                {
                    //compare by points
                    if (pts[i] > pts[j])
                        competitionSortingPoints[i]++;
                    if (pts[i] < pts[j])
                        competitionSortingPoints[j]++;

                    //points are equal
                    if (pts[i] == pts[j])
                    {
                        //compare goal difference
                        if ((gf[i] - ga[i]) > (gf[j] - ga[j]))
                            competitionSortingPoints[i]++;
                        if ((gf[i] - ga[i]) < (gf[j] - ga[j]))
                            competitionSortingPoints[j]++;

                        //goal difference is equal
                        if ((gf[i] - ga[i]) == (gf[j] - ga[j]))
                        {
                            //compare goals scored
                            if (gf[i] > gf[j])
                                competitionSortingPoints[i]++;
                            if (gf[i] < gf[j])
                                competitionSortingPoints[j]++;

                            //goals scored is equal
                            if (gf[i] == gf[j])
                            {
                                //compare id
                                if (tIDs[i] > tIDs[j])
                                    competitionSortingPoints[i]++;
                                else
                                    competitionSortingPoints[j]++;
                            }
                        }
                    }

                }

            #endregion

            #region bubble sort

            int _tID;
            int _gf;
            int _ga;
            int _pts;
            int _competitionSortingPoints;

            for (int i = tIDs.Count - 1; i > 0; i--)
            {
                for (int j = 0; j < i; j++)
                {
                    if (competitionSortingPoints[j] < competitionSortingPoints[j + 1])
                    {
                        _tID = tIDs[j];
                        _gf = gf[j];
                        _ga = ga[j];
                        _pts = pts[j];
                        _competitionSortingPoints = competitionSortingPoints[j];

                        tIDs[j] = tIDs[j + 1];
                        gf[j] = gf[j + 1];
                        ga[j] = ga[j + 1];
                        pts[j] = pts[j + 1];
                        competitionSortingPoints[j] = competitionSortingPoints[j + 1];

                        tIDs[j + 1] = _tID;
                        gf[j + 1] = _gf;
                        ga[j + 1] = _ga;
                        pts[j + 1] = _pts;
                        competitionSortingPoints[j + 1] = _competitionSortingPoints;
                    }
                }
            }
            #endregion

        }

    }
}
