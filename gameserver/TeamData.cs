using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerMono
{
    class TeamData
    {
        public int tID; //DB id (if challenge)
        public string name;
        public string shortName;
        public bool isOfficialLeagueBotTeam;

        public byte score = 0;
        public byte offsides = 0;
        public byte goalKicks = 0;
        public byte corners = 0;
        public byte throwIns = 0;
        public byte shotsTotal = 0;
        public byte shotsOnGoal = 0;
        public int possession = 0;
        public byte shirtStyle;
        public byte[,] shirtColors = new byte[4, 3];

        public TeamData(bool isOfficialLeagueBotTeam, int tID, string name, string shortName, byte shirtStyle, byte[,] shirtColors)
        {
            this.isOfficialLeagueBotTeam = isOfficialLeagueBotTeam;
            this.tID = tID;
            this.name = name;
            this.shortName = shortName;

            this.shirtStyle = shirtStyle;
            this.shirtColors = shirtColors;
        }
    }
}
