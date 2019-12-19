using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerMono
{
    class Keeper
    {
        public double[] coords = new double[2];
        public double angle;
        public double speed;
        public int ballInPossesDelay;
        public byte animID;
        public int divingDelay;
        public int touchDelay;
        public int divePossesDelay;
        public double distToBall;
        public double[] kickoffCoords = new double[2];
        public byte anim;

        public Keeper()
        {
            coords[0] = -6;
            coords[1] = 0;
        }

        public double GetMaxSpeed()
        {
            return 0.0249;
        }

        public void Move()
        {
            double r;
            r = Math.Cos(F.ToRad * angle) * speed;
            coords[0] += r;
            r = Math.Sin(F.ToRad * angle) * speed;
            coords[1] += r;
        }

    }
}
