using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    class DivisionData
    {
        public int location;
        public int division;
        public int _group;

        public int[] tIDs = new int[Form1.maxTeamsInDiv];
        public int[] gf = new int[Form1.maxTeamsInDiv];
        public int[] ga = new int[Form1.maxTeamsInDiv];
        public int[] pts = new int[Form1.maxTeamsInDiv];

        public bool[] isRelegating = new bool[Form1.maxTeamsInDiv];
        public bool[] isPromoting = new bool[Form1.maxTeamsInDiv];

        public DivisionData(int location, int division, int _group, string tIDString, string gfString, string gaString, string ptsString)
        {
            this.location = location;
            this.division = division;
            this._group = _group;

            string[] arrayStr;

            arrayStr = tIDString.Split(',');
            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                int.TryParse(arrayStr[i], out tIDs[i]);

            arrayStr = gfString.Split(',');
            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                int.TryParse(arrayStr[i], out gf[i]);

            arrayStr = gaString.Split(',');
            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                int.TryParse(arrayStr[i], out ga[i]);

            arrayStr = ptsString.Split(',');
            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                int.TryParse(arrayStr[i], out pts[i]);

            SortDivision();

            SetRelegatingTeams();
            SetPromotingTeams();
        }

        void SortDivision()
        {
            #region count sorting points

            int[] competitionSortingPoints = new int[Form1.maxTeamsInDiv];

            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                for (int j = i + 1; j < Form1.maxTeamsInDiv; j++)
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

            for (int i = Form1.maxTeamsInDiv - 1; i > 0; i--)
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

        void SetRelegatingTeams()
        {
            if (division == Form1.MAXDIVISION) return;

            //all bots teams will be relegating pool by default
            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                if (tIDs[i] == 0)
                    isRelegating[i] = true;

            isRelegating[8] = true;
            isRelegating[9] = true;
            isRelegating[10] = true;
            isRelegating[11] = true;

            //set -1000 points for bot teams
            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                if (isRelegating[i] && tIDs[i] == 0)
                    pts[i] -= 1000;
        }

        void SetPromotingTeams()
        {
            if (division == 1) return;

            //only human teams may get promoted
            if (tIDs[0] > 0)
                isPromoting[0] = true;
            if (tIDs[1] > 0)
                isPromoting[1] = true;

            //if human team, they will get +1000, bots -1000
            if (tIDs[0] > 0)
                pts[0] += 1000;
            else
                pts[0] -= 1000;

            if (tIDs[1] > 0)
                pts[1] += 1000;
            else
                pts[1] -= 1000;
        }
    }
}
