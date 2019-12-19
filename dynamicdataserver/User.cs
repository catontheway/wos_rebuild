using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseServer
{
    class User
    {
        public int pID;
        public int tID;

        public User(int pID, int tID)
        {
            this.pID = pID;
            this.tID = tID;
        }

    }
}
