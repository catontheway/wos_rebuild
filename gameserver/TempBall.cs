using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerMono
{
    class TempBall
    {
        public double[] coords = new double[2];
        double angle;
        public double speed;
        double height;
        double zSpeed;
        double[] distShotCoords; //if ball will go to goal, here is coordinates. used only for distance shot

        public TempBall(double coordsX, double coordsY, double angle, double speed, double height, double zSpeed)
        {
            this.coords[0] = coordsX;
            this.coords[1] = coordsY;
            this.angle = angle;
            this.speed = speed;
            this.height = height;
            this.zSpeed = zSpeed;
        }

        void Move()
        {
            double r;

            r = Math.Cos(F.ToRad * angle) * speed;
            coords[0] = coords[0] + r;
            r = Math.Sin(F.ToRad * angle) * speed;
            coords[1] = coords[1] + r;

            AltitudeCalculations();

            if (height <= 0) speed -= 0.0005f;
            if (speed < 0) speed = 0;
        }

        void AltitudeCalculations()
        {
            double b1, b2, a1;

            b1 = 0.933;
            b2 = 1.052;
            a1 = 0.08;

            double r;

            if (zSpeed != 0)
            {
                r = Math.Sin(F.ToRad * zSpeed) * a1;
                height += r;
                if (height < 0) height = 0;
            }

            //ball going up
            if (zSpeed > 0)
            {
                zSpeed *= b1;
                if (zSpeed < 4) zSpeed = -3.67;
            }

            //ball going down
            if (zSpeed < 0)
            {
                if (zSpeed > -3.67) zSpeed = -3.67;
                zSpeed *= b2;
            }

            if (zSpeed > 90) zSpeed = 90;
            if (zSpeed < -90) zSpeed = -90;

            if (height > 0)
                if (zSpeed < 0.1 && zSpeed > -0.1)
                    zSpeed = -4;

            //bounce
            if (height <= 0 && zSpeed < 0)
            {
                zSpeed *= 0.63;
                height = 0;
                zSpeed = 0 - zSpeed;
                if (zSpeed < 4)
                    zSpeed = 0;
                else
                    speed -= 0.01;
            }

        }

        public void CalculateTempBall()
        {

            bool done = false;

            while (!done)
            {
                Move();

                //temp ball goes goal
                if (coords[1] < Field.GOALLINE_N || coords[1] > Field.GOALLINE_P)
                {
                    distShotCoords = coords;
                    done = true;
                }

                if (coords[0] < Field.THROWIN_N) done = true;
                if (coords[0] > Field.THROWIN_P) done = true;
                if (speed <= 0) done = true;

            }
        }

        public void CalculateTempBall2(double keeperCoordY)
        {

            bool done = false;

            while (!done)
            {
                Move();

                if (coords[1] < Field.GOALLINE_N) done = true;
                if (coords[1] > Field.GOALLINE_P) done = true;
                if (coords[0] < Field.THROWIN_N) done = true;
                if (coords[0] > Field.THROWIN_P) done = true;

                //ball going down
                if (angle > 180)
                {
                    if (coords[1] < keeperCoordY)
                        done = true;
                }
                //ball going up
                else
                {
                    if (coords[1] > keeperCoordY)
                        done = true;
                }

                if (speed <= 0) done = true;
            }
        }


    }
}
