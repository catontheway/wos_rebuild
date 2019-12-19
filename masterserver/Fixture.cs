using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    class Fixture
    {
        public int fixtureID;
        public int[] tIDSlot = new int[2]; //0-11
        public int[] tID = new int[2];  //db TID
        public byte location;
        public byte division;
        public UInt16 _group;

        public Fixture(int fixtureID, int tID0, int tID1, byte location, byte division, UInt16 _group)
        {
            this.fixtureID = fixtureID;
            this.tIDSlot[0] = tID0;
            this.tIDSlot[1] = tID1;
            this.location = location;
            this.division = division;
            this._group = _group;
        }

    }
}
