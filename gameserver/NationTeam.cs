using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerMono
{
    class NationTeam
    {
        public int nID;
        public string name = "";
        public string shortName = "";
        public byte shirtStyle = 0;
        public byte shirtType = 0;  //1=bright, 2=dark
        public byte[,] shirtColors = new byte[4, 3];
    }
}
