using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    class NewDivisionData
    {
        public int location;
        public int division;
        public int _group;

        public bool isGroupFull = false;
        public int[] tIDs = new int[Form1.maxTeamsInDiv];

        public string tIDString;

        //when creating new league
        public NewDivisionData(int location, int division, int _group)
        {
            this.location = location;
            this.division = division;
            this._group = _group;

            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                tIDs[i] = -1;
        }

        public void SetAsBotsGroup()
        {
            isGroupFull = true;

            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                tIDs[i] = 0;
        }

        public void InsertTeam(int tID)
        {
            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                if (tIDs[i] == -1)
                {
                    tIDs[i] = tID;
                    break;
                }
        }

        public int GetTeamCount()
        {
            int count = 0;

            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                if (tIDs[i] > -1)
                    count++;

            return count;
        }

        public void GenerateTIDString()
        {
            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                tIDString += tIDs[i] + ",";
            tIDString = tIDString.TrimEnd(new char[] { ',' });
        }
    }
}
