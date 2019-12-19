using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    class ChallengePlayerData
    {
        public int pID;
        public byte tID;   //contain 0 or 1 (home/away)
        public byte timePlayed;
        public byte shotsTotal;
        public byte shotsOnTarget;
        public byte offsides;
        public int _posUP;
        public int _posDown;
        public int _posLeft;
        public int _posRight;
        public int _teamGoals;

        //these values comes from database (users)
        public string username;
        public int careerGoals;
        public int careerAsts;
        public int careerTeamGoals;
        public int posUP;
        public int posDown;
        public int posLeft;
        public int posRight;

        //current career data
        public int careerID;
        public int goals;
        public int asts;
        public int teamGoals;

        public ChallengePlayerData(byte tID, int pID, byte timePlayed, byte shotsTotal, byte shotsOnTarget, byte offsides, int _posUP, int _posDown, int _posLeft, int _posRight, int _teamGoals)
        {
            this.username = "";

            this.pID = pID;
            this.tID = tID;
            this.timePlayed = timePlayed;
            this.shotsTotal = shotsTotal;
            this.shotsOnTarget = shotsOnTarget;
            this.offsides = offsides;
            this._posUP = _posUP;
            this._posDown = _posDown;
            this._posLeft = _posLeft;
            this._posRight = _posRight;
            this._teamGoals = _teamGoals;
        }
    }
}
