using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace GameServerMono
{
    class F
    {
        public const double ToRad = 0.0174532925199432;
        public const double ToDeg = 57.295779513082320;
        public static Random rand = new Random();

        public static double Angle(double cx, double cy, double px, double py)
        {
            double dir = ToDeg * Math.Atan2(py - cy, px - cx);

            if (dir < 0)
                dir += 360;

            return dir;
        }

        public static double Distance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
        }

    }
}
