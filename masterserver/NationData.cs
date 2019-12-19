using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    public class NationData
    {
        public string countryCode;    //FI
        public string countryName;    //Finland
        public string country3Letter; //FIN
        public int plrCount;

        public NationData(string countryCode, string countryName, string country3Letter)
        {
            this.countryCode = countryCode;
            this.countryName = countryName;
            this.country3Letter = country3Letter;
            this.plrCount = 0;
        }

    }
}
