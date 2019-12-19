using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseServer
{
    class Fixture
    {
        public int fixtureID;
        public int[] tID = new int[2];  //db TID
        public byte location;
        public int mID = -1;  //masterserver id in array
        public string IP = "";

        public Fixture(int fixtureID, int tID0, int tID1, byte location)
        {
            this.fixtureID = fixtureID;
            this.tID[0] = tID0;
            this.tID[1] = tID1;
            this.location = location;
        }

    }
}
