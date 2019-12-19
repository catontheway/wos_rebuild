using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerMono
{
    class Ball
    {
        public double[] coords = new double[2];
        public double[] autopassCoords = new double[2];
        public double angle;
        public double speed;
        public double height;
        public double zSpeed;
        RoomData room;

        public Ball(RoomData room)
        {
            this.room = room;
            coords[0] = 0;
            coords[1] = 0;
        }

        public void BallCalculations()
        {
            int j = IsBallNearAtGoal();
            
            for (int i = 0; i < j; i++)
            {
                Move(speed / j);

                AltitudeCalculations(j);

                if (height <= 0)
                {
                    if (j == 1)
                        speed -= 0.0005;
                    else
                        speed -= 0.00005;
                }

                if (speed < 0) speed = 0;

                if (j == 10) CheckIfBallHitsBar();
            }

            //lets put ball to throwin/corner/goalkick coordinates
            if (speed <= 0 && room.IsThrowInCornerFreekick())
                SetBallToFreekickCoords();

            //this is just visual. this will erase ball bouncing in throwin takers hands
            if (room.throwInTID > -1)
                if (speed <= 0)
                    zSpeed = 0;

        }

        void SetBallToFreekickCoords()
        {
            if (room.throwInTID > -1)
            {
                for (int i = 0; i < 2; i++)
                    coords[i] = room.throwInCoords[i];

                //lets reset throwin, if no players online
                if (!room.botsEnabled)
                    if (room.CountPlayersInTeam(room.throwInTID) == 0)
                    {
                        room.throwInTID = -1;
                        room.timerEnabled = true;
                        room.ResetAutomoveFromFreekickArea();
                        room.BroadcastBlockedAreaRemove(false);
                    }
            }

            //***************

            if (room.goalkickTID > -1)
            {
                zSpeed = 0;
                height = 0;
                for (int i = 0; i < 2; i++)
                    coords[i] = room.goalkickCoords[i];
            }

            //***************

            if (room.cornerTID > -1)
            {
                for (int i = 0; i < 2; i++)
                    coords[i] = room.cornerCoords[i];

                //lets reset corner, if no players online
                if (!room.botsEnabled)
                    if (room.CountPlayersInTeam(room.cornerTID) == 0)
                    {
                        room.cornerTID = -1;
                        room.timerEnabled = true;
                        room.ResetAutomoveFromFreekickArea();
                        room.BroadcastBlockedAreaRemove(false);
                    }
            }

            //***************

            if (room.freekickTID > -1)
            {
                for (int i = 0; i < 2; i++)
                    coords[i] = room.freekickCoords[i];

                //lets reset freekick, if no players online
                if (!room.botsEnabled)
                    if (room.CountPlayersInTeam(room.freekickTID) == 0)
                    {
                        room.freekickTID = -1;
                        room.timerEnabled = true;
                        room.ResetAutomoveFromFreekickArea();
                        room.BroadcastBlockedAreaRemove(false);
                    }
            }

        }

        void CheckIfBallHitsBar()
        {
            if (IsKeeperBallInPossesDelay()) return;

            TopPost();
            Post();
            TopNet();
            BackNet();
            SideNet();
            Goal();
        }

        void TopPost()
        {
            if (coords[0] > Field.SIDENET_P + Field.POSTRADIUS) return;
            if (coords[0] < Field.SIDENET_N - Field.POSTRADIUS) return;

            double pAng, a1, f, heSpeed, r;

            if (F.Distance(coords[1], height, Field.GOALLINE_N, Field.TOPPOST) < Field.POSTRADIUS * 2)
            {
                pAng = F.Angle(Field.GOALLINE_N, Field.TOPPOST, coords[1], height);
                if (pAng > 90) pAng = 180 - pAng;
                if (pAng < -90) pAng = 0 - (180 + pAng);

                a1 = HeAngleToSpeed(zSpeed) / 100;
                if (speed > a1) a1 = speed;

                f = 1.0 / 90.0 * pAng;
                if (f < 0) f = 0 - f;

                heSpeed = f * a1;
                speed = a1 - heSpeed;
                zSpeed = SpeedToHeAngle(a1, pAng);

                height = Field.TOPPOST;
                r = Math.Sin(F.ToRad * pAng) * (Field.POSTRADIUS * 2 + 0.001);
                height -= r;

                pAng = F.Angle(0, Field.GOALLINE_N, 0, coords[1]);
                r = Math.Sin(F.ToRad * pAng) * (Field.POSTRADIUS * 2 + 0.001);

                double _y = Field.GOALLINE_N + r;//////////////
                coords[1] = _y;

                if (coords[1] > Field.GOALLINE_N)
                    angle = 360 - angle;

                speed *= 0.5;
                zSpeed *= 0.5;

                room.SoundBroadcast(9, room.timerEnabled);
                room.keeperDistanceShot = -1;
                if (room.IsOpponetsShot()) room.AddShotStat(false);
            }

            if (F.Distance(coords[1], height, Field.GOALLINE_P, Field.TOPPOST) < Field.POSTRADIUS * 2)
            {
                pAng = F.Angle(Field.GOALLINE_P, Field.TOPPOST, coords[1], height);
                if (pAng > 90) pAng = 180 - pAng;
                if (pAng < -90) pAng = 0 - (180 + pAng);

                a1 = HeAngleToSpeed(zSpeed) / 100;
                if (speed > a1) a1 = speed;

                f = 1.0 / 90.0 * pAng;
                if (f < 0) f = 0 - f;

                heSpeed = f * a1;
                speed = a1 - heSpeed;
                zSpeed = SpeedToHeAngle(a1, pAng);

                height = Field.TOPPOST;
                r = Math.Sin(F.ToRad * pAng) * (Field.POSTRADIUS * 2 + 0.001);
                height -= r;

                pAng = F.Angle(0, Field.GOALLINE_P, 0, coords[1]);
                r = Math.Sin(F.ToRad * pAng) * (Field.POSTRADIUS * 2 + 0.001);

                double _y = Field.GOALLINE_P + r;///////////////
                coords[1] = _y;

                if (coords[1] < Field.GOALLINE_P)   
                    angle = 360 - angle;                

                speed *= 0.5;
                zSpeed *= 0.5;

                room.SoundBroadcast(9, room.timerEnabled);
                room.keeperDistanceShot = -1;
                if (room.IsOpponetsShot()) room.AddShotStat(false);
            }
        }

        void Post()
        {
            if (height > (Field.TOPPOST + Field.POSTRADIUS)) return;

            if (F.Distance(Field.SIDENET_N, Field.GOALLINE_N, coords[0], coords[1]) < (Field.POSTRADIUS * 2))
                PostBounce(Field.SIDENET_N, Field.GOALLINE_N);

            if (F.Distance(Field.SIDENET_P, Field.GOALLINE_N, coords[0], coords[1]) < (Field.POSTRADIUS * 2))
                PostBounce(Field.SIDENET_P, Field.GOALLINE_N);

            if (F.Distance(Field.SIDENET_N, Field.GOALLINE_P, coords[0], coords[1]) < (Field.POSTRADIUS * 2))
                PostBounce(Field.SIDENET_N, Field.GOALLINE_P);

            if (F.Distance(Field.SIDENET_P, Field.GOALLINE_P, coords[0], coords[1]) < (Field.POSTRADIUS * 2))
                PostBounce(Field.SIDENET_P, Field.GOALLINE_P);

        }

        void PostBounce(double x, double y)
        {
            angle = F.Angle(x, y, coords[0], coords[1]);
            coords[0] = x;
            coords[1] = y;

            double r;

            r = Math.Cos(F.ToRad * angle) * (Field.POSTRADIUS * 2);
            coords[0] += r;
            r = Math.Sin(F.ToRad * angle) * (Field.POSTRADIUS * 2);
            coords[1] += r;

            speed *= 0.5;

            room.keeperDistanceShot = -1;
            room.SoundBroadcast(9, room.timerEnabled);
            if (room.IsOpponetsShot()) room.AddShotStat(false);
        }

        void TopNet()
        {
            if (height > (Field.TOPPOST + Field.POSTRADIUS) || height < Field.TOPPOST) return;

            if (coords[0] <= (Field.SIDENET_P + Field.POSTRADIUS) && coords[0] >= (Field.SIDENET_N - Field.POSTRADIUS))
            {
                if (coords[1] >= Field.GOALLINE_P && coords[1] <= Field.BACKNET_P)
                {
                    if (height > Field.TOPPOST + (Field.BALLRADIUS / 2))
                        height = Field.TOPPOST + Field.POSTRADIUS + 0.001;
                    else
                        height = Field.TOPPOST - 0.001;
                    zSpeed = 0 - zSpeed / 2;
                }

                if (coords[1] <= Field.GOALLINE_N && coords[1] >= Field.BACKNET_N)
                {
                    if (height > Field.TOPPOST + (Field.BALLRADIUS / 2))
                        height = Field.TOPPOST + Field.POSTRADIUS + 0.001;
                    else
                        height = Field.TOPPOST - 0.001;
                    zSpeed = 0 - zSpeed / 2;
                }
            }
        }

        void BackNet()
        {
            if (height > Field.TOPPOST) return;

            if (coords[0] <= Field.SIDENET_P && coords[0] >= Field.SIDENET_N)
            {
                if (coords[1] >= Field.BACKNET_P && coords[1] <= (Field.BACKNET_P + 0.01))
                {
                    speed = 0;
                    coords[1] = Field.BACKNET_P - 0.001;
                }

                if (coords[1] <= Field.BACKNET_N && coords[1] >= (Field.BACKNET_N - 0.01))
                {
                    speed = 0;
                    coords[1] = Field.BACKNET_N + 0.001;
                }
            }
        }

        void SideNet()
        {
            if (height > Field.TOPPOST) return;

            if (coords[1] >= Field.GOALLINE_P && coords[1] <= Field.BACKNET_P)
            {
                if (coords[0] <= Field.SIDENET_P + Field.POSTRADIUS && coords[0] >= Field.SIDENET_P - Field.POSTRADIUS)
                {
                    if (coords[0] > Field.SIDENET_P)
                    {
                        coords[0] = Field.SIDENET_P + Field.POSTRADIUS + 0.001;
                        angle = 90 - (angle - 90);
                        speed *= 0.5;
                    }
                    else
                    {
                        coords[0] = Field.SIDENET_P - Field.POSTRADIUS - 0.001;
                        angle = 90 - angle + 90;
                        speed *= 0.5;
                    }
                }

                if (coords[0] >= Field.SIDENET_N - Field.POSTRADIUS && coords[0] <= Field.SIDENET_N + Field.POSTRADIUS)
                {
                    if (coords[0] < Field.SIDENET_N)
                    {
                        coords[0] = Field.SIDENET_N - Field.POSTRADIUS - 0.001;
                        angle = 90 - angle + 90;
                        speed *= 0.5;
                    }
                    else
                    {
                        coords[0] = Field.SIDENET_N + Field.POSTRADIUS + 0.001;
                        angle = 90 - (angle - 90);
                        speed *= 0.5;
                    }
                }
            }

            //*************

            if (coords[1] <= Field.GOALLINE_N && coords[1] >= Field.BACKNET_N)
            {
                if (coords[0] <= Field.SIDENET_P + Field.POSTRADIUS && coords[0] >= Field.SIDENET_P - Field.POSTRADIUS)
                {
                    if (coords[0] > Field.SIDENET_P)
                    {
                        coords[0] = Field.SIDENET_P + Field.POSTRADIUS + 0.001;
                        angle = 270 - angle + 270;
                        speed *= 0.5;
                    }
                    else
                    {
                        coords[0] = Field.SIDENET_P - Field.POSTRADIUS - 0.001;
                        angle = 270 - (angle - 270);
                        speed *= 0.5;
                    }
                }

                if (coords[0] >= Field.SIDENET_N - Field.POSTRADIUS && coords[0] <= Field.SIDENET_N + Field.POSTRADIUS)
                {
                    if (coords[0] < Field.SIDENET_N)
                    {
                        coords[0] = Field.SIDENET_N - Field.POSTRADIUS - 0.001;
                        angle = 270 - (angle - 270);
                        speed *= 0.5;
                    }
                    else
                    {
                        coords[0] = Field.SIDENET_N + Field.POSTRADIUS + 0.001;
                        angle = 270 - angle + 270;
                        speed *= 0.5;
                    }
                }
            }
        }

        void Goal()
        {
            if (room.autoMoving > 0) return;
            if (room.ballInNetDelayer > 0) return;
            if (room.freekickTID > -1) return;

            if (height > Field.TOPPOST) return;
            if (coords[0] > Field.SIDENET_P) return;
            if (coords[0] < Field.SIDENET_N) return;

            if (coords[1] > 0)
            {
                if (coords[1] > Field.BACKNET_P) return;
                if (coords[1] < (Field.GOALLINE_P + Field.BALLRADIUS)) return;
            }
            else
            {
                if (coords[1] < Field.BACKNET_N) return;
                if (coords[1] > (Field.GOALLINE_N - Field.BALLRADIUS)) return;
            }

            if (coords[1] > 0)
                GoalRoutine(0);   //upper net
            else
                GoalRoutine(1);   //lower net

        }

        void GoalRoutine(int whichGoal)
        {
            room.ResetOffside();
            room.ballInNetDelayer = 100;
            room.autoMoving = 1;
            room.timerEnabled = false;
            room.keeperDistanceShot = -1;
            bool isOwnGoal;
            room.fouledTID = -1;

            for (int i = 0; i < 2; i++)
                if (room.keepers[i].divingDelay == 0)
                    room.keepers[i].speed = 0;

            //upper net
            if (whichGoal == 0)
            {
                //away scored
                if (room.homeSide == 1)
                {
                    room.teams[1].score++;
                    room.kickoff = 0;
                    isOwnGoal = room.IsOwnGoal(whichGoal);
                    room.SendTrainingGoalData(1, isOwnGoal);
                    room.AddGoalStats(1, isOwnGoal);
                    if (!isOwnGoal) room.AddShotStat(true);
                    room.BroadcastInfoAboutGoal(5, room.timerEnabled, room.IsOwnGoal(whichGoal));
                }
                //home scored
                else
                {
                    room.teams[0].score++;
                    room.kickoff = 1;
                    isOwnGoal = room.IsOwnGoal(whichGoal);
                    room.SendTrainingGoalData(0, isOwnGoal);
                    room.AddGoalStats(0, isOwnGoal);
                    if (!isOwnGoal) room.AddShotStat(true);
                    room.BroadcastInfoAboutGoal(4, room.timerEnabled, room.IsOwnGoal(whichGoal));
                }
            }
            //lower net
            else
            {
                //home scored
                if (room.homeSide == 1)
                {
                    room.teams[0].score++;
                    room.kickoff = 1;
                    isOwnGoal = room.IsOwnGoal(whichGoal);
                    room.SendTrainingGoalData(0, isOwnGoal);
                    room.AddGoalStats(0, isOwnGoal);
                    if (!isOwnGoal) room.AddShotStat(true);
                    room.BroadcastInfoAboutGoal(4, room.timerEnabled, room.IsOwnGoal(whichGoal));
                }
                //away scored
                else
                {
                    room.teams[1].score++;
                    room.kickoff = 0;
                    isOwnGoal = room.IsOwnGoal(whichGoal);
                    room.SendTrainingGoalData(1, isOwnGoal);
                    room.AddGoalStats(1, isOwnGoal);
                    if (!isOwnGoal) room.AddShotStat(true);
                    room.BroadcastInfoAboutGoal(5, room.timerEnabled, room.IsOwnGoal(whichGoal));
                }
            }

            room.ResetSlideTacklesAndFallDelays();
            room.SetKickOffCoords();
        }

        public bool IsKeeperBallInPossesDelay()
        {
            if (room.keepers[0].ballInPossesDelay > 0 || room.keepers[1].ballInPossesDelay > 0)
                return true;
            else
                return false;
        }

        void AltitudeCalculations(int j)
        {
            double b1, b2, a1;

            if (j == 1)
            {
                b1 = 0.933;
                b2 = 1.052;
                a1 = 0.08;
            }
            else
            {
                b1 = 0.9933;
                b2 = 1.0052;
                a1 = 0.008;
            }

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

        byte IsBallNearAtGoal()
        {
            if (height > 0.5) return 1;

            if (coords[0] < (Field.SIDENET_P + 0.2f) && coords[0] > (Field.SIDENET_N - 0.2f))
            {
                if (coords[1] > Field.GOALLINE_P - 0.3f && coords[1] < Field.GOALLINE_P + 0.3f) return 10;
                if (coords[1] < Field.GOALLINE_N + 0.3f && coords[1] > Field.GOALLINE_N - 0.3f) return 10;
            }

            return 1;
        }

        public void Move()
        {
            double r;
            r = Math.Cos(F.ToRad * angle) * speed;
            coords[0] += r;
            r = Math.Sin(F.ToRad * angle) * speed;
            coords[1] += r;
        }

        public void Move(double speed)
        {
            double r;
            r = Math.Cos(F.ToRad * angle) * speed;
            coords[0] += r;
            r = Math.Sin(F.ToRad * angle) * speed;
            coords[1] += r;
        }

        double SpeedToHeAngle(double pSpeed, double pAng)
        {
            double f = 1 / 90 * pAng;
            return pSpeed * f / 0.12;
        }

        double HeAngleToSpeed(double heA)
        {
            heA *= 0.12;
            if (heA < 0) heA = 0 - heA;
            return heA;
        }

        //normal shoot
        public void CalculateKickA(double power, double shootDirection, double controlDistance, bool isMouseControl, bool extraPowerForCorner)
        {
            angle = shootDirection;

            double p = power / 15.0 + 7.0;
            speed = p / 200.0;

            #region mouse
            if (isMouseControl)
            {
                if (p >= 16)
                {
                    if (controlDistance <= 30) zSpeed = 50;
                    if (controlDistance > 30) zSpeed = 45;
                    if (controlDistance > 60) zSpeed = 40;
                    if (controlDistance > 90) zSpeed = 35;
                    if (controlDistance > 120) zSpeed = 30;
                    if (controlDistance > 150) zSpeed = 25;
                    if (controlDistance > 180) zSpeed = 20;
                    if (controlDistance > 210) zSpeed = 15;
                    if (controlDistance > 240) zSpeed = 10;
                    if (controlDistance > 270) zSpeed = 5;
                    if (controlDistance > 300) zSpeed = 0;
                }

                if (p >= 14 && p < 16)
                {
                    if (controlDistance <= 30) zSpeed = 40;
                    if (controlDistance > 30) zSpeed = 35;
                    if (controlDistance > 60) zSpeed = 30;
                    if (controlDistance > 90) zSpeed = 25;
                    if (controlDistance > 120) zSpeed = 20;
                    if (controlDistance > 150) zSpeed = 15;
                    if (controlDistance > 180) zSpeed = 10;
                    if (controlDistance > 210) zSpeed = 5;
                    if (controlDistance > 240) zSpeed = 0;
                    if (controlDistance > 270) zSpeed = 0;
                    if (controlDistance > 300) zSpeed = 0;
                }
                if (p >= 12 && p < 14)
                {
                    if (controlDistance <= 30) zSpeed = 30;
                    if (controlDistance > 30) zSpeed = 25;
                    if (controlDistance > 60) zSpeed = 20;
                    if (controlDistance > 90) zSpeed = 15;
                    if (controlDistance > 120) zSpeed = 10;
                    if (controlDistance > 150) zSpeed = 5;
                    if (controlDistance > 180) zSpeed = 0;
                    if (controlDistance > 210) zSpeed = 0;
                    if (controlDistance > 240) zSpeed = 0;
                    if (controlDistance > 270) zSpeed = 0;
                    if (controlDistance > 300) zSpeed = 0;
                }

                if (p >= 10 && p < 12)
                {
                    if (controlDistance <= 30) zSpeed = 20;
                    if (controlDistance > 30) zSpeed = 15;
                    if (controlDistance > 60) zSpeed = 10;
                    if (controlDistance > 90) zSpeed = 5;
                    if (controlDistance > 120) zSpeed = 0;
                    if (controlDistance > 150) zSpeed = 0;
                    if (controlDistance > 180) zSpeed = 0;
                    if (controlDistance > 210) zSpeed = 0;
                    if (controlDistance > 240) zSpeed = 0;
                    if (controlDistance > 270) zSpeed = 0;
                    if (controlDistance > 300) zSpeed = 0;
                }

                if (p < 10)
                {
                    if (controlDistance <= 30) zSpeed = 10;
                    if (controlDistance > 30) zSpeed = 5;
                    if (controlDistance > 60) zSpeed = 0;
                    if (controlDistance > 90) zSpeed = 0;
                    if (controlDistance > 120) zSpeed = 0;
                    if (controlDistance > 150) zSpeed = 0;
                    if (controlDistance > 180) zSpeed = 0;
                    if (controlDistance > 210) zSpeed = 0;
                    if (controlDistance > 240) zSpeed = 0;
                    if (controlDistance > 270) zSpeed = 0;
                    if (controlDistance > 300) zSpeed = 0;
                }
            }
            #endregion
            #region pad&AI
            else
            {
                if (p >= 16) zSpeed = 20;
                if (p >= 14 && p < 16) zSpeed = 15;
                if (p >= 12 && p < 14) zSpeed = 10;
                if (p >= 10 && p < 12) zSpeed = 5;
                if (p < 10) zSpeed = 0;

            }
            #endregion

            if (zSpeed <= 0) speed -= 0.005;
            if (zSpeed == 5) speed -= 0.0085;
            if (zSpeed == 10) speed -= 0.0110;
            if (zSpeed == 15) speed -= 0.0135;
            if (zSpeed == 20) speed -= 0.016;
            if (zSpeed == 25) speed -= 0.0185;
            if (zSpeed == 30) speed -= 0.0210;
            if (zSpeed == 35) speed -= 0.0235;
            if (zSpeed == 40) speed -= 0.026;
            if (zSpeed == 45) speed -= 0.0285;
            if (zSpeed == 50) speed -= 0.031;

            if (extraPowerForCorner)
                speed += 0.015;

        }

        //incase autopass receiver arent found, player will do this (direct shoot)
        public void CalculateKickB(double power, double shootDirection)
        {
            angle = shootDirection;

            double p = power / 15.0 + 7.0;
            zSpeed = 0;

            if (p >= 16) speed = 0.065;
            if (p >= 14 && p < 16) speed = 0.060;
            if (p >= 12 && p < 14) speed = 0.055;
            if (p >= 10 && p < 12) speed = 0.050;
            if (p < 10) speed = 0.045;

            if (height > 0) speed *= 0.75;
        }

        //crossing
        public void CalculateKickC(double power, double shootDirection, double controlDistance, bool isMouseControl)
        {
            angle = shootDirection;

            if (isMouseControl)
            {
                if (controlDistance <= 50) speed = 0.040;
                if (controlDistance > 50) speed = 0.045;
                if (controlDistance > 100) speed = 0.050;
                if (controlDistance > 150) speed = 0.055;
                if (controlDistance > 200) speed = 0.060;
                if (controlDistance > 250) speed = 0.065;

                zSpeed = 0;
                if (height > 0) speed *= 0.85;
            }
            else
            {
                double p = power / 15.0 + 7.0;
                speed = p / 200.0;

                if (p >= 16) zSpeed = 50;
                if (p >= 14 && p < 16) zSpeed = 40;
                if (p >= 12 && p < 14) zSpeed = 30;
                if (p >= 10 && p < 12) zSpeed = 20;
                if (p < 10) zSpeed = 10;

                if (zSpeed <= 0) speed -= 0.005;
                if (zSpeed == 5) speed -= 0.0085;
                if (zSpeed == 10) speed -= 0.0110;
                if (zSpeed == 15) speed -= 0.0135;
                if (zSpeed == 20) speed -= 0.016;
                if (zSpeed == 25) speed -= 0.0185;
                if (zSpeed == 30) speed -= 0.0210;
                if (zSpeed == 35) speed -= 0.0235;
                if (zSpeed == 40) speed -= 0.026;
                if (zSpeed == 45) speed -= 0.0285;
                if (zSpeed == 50) speed -= 0.031;
            }


        }

        public void AIQuickShot(double shootDirection)
        {
            angle = shootDirection;
            zSpeed = 0;
            speed = 0.08;
            if (height > 0) speed *= 0.75;
        }

        public void CalculateAICornerKick(double shootDirection)
        {
            angle = shootDirection;
            speed = 0.060 + (F.rand.NextDouble() / 100);
            zSpeed = 50;
        }

        public void CalculateAutopass(int tID, int pID, double receiverPosX, double receiverPosY, double receiverAngle, double receiverSpeed)
        {
            double shortestDist = 1000;
            int iID = -1;
            int jID = -1;
            double[] _autopassCoords = new double[2];

            for (int i = 0; i < 40; i++)
                for (int j = 0; j < 6; j++)
                {
                    double d = SimulateAutopass(i, j, receiverPosX, receiverPosY, receiverAngle, receiverSpeed, out _autopassCoords);
                    if (d < shortestDist)
                    {
                        autopassCoords[0] = _autopassCoords[0];
                        autopassCoords[1] = _autopassCoords[1];
                        shortestDist = d;
                        iID = i;
                        jID = j;
                    }
                }

            double advance = GetAdvance(iID);

            double r;
            r = Math.Cos(F.ToRad * receiverAngle) * receiverSpeed * advance;
            receiverPosX += r;
            r = Math.Sin(F.ToRad * receiverAngle) * receiverSpeed * advance;
            receiverPosY += r;

            angle = F.Angle(coords[0], coords[1], receiverPosX, receiverPosY);
            speed = GetSpeedForAdvance(jID);
            zSpeed = 0;
            if (height > 0) speed *= 0.75;

            room.users[tID, pID].gotoAutopass = true;
        }

        double SimulateAutopass(int i, int j, double receiverPosX, double receiverPosY, double receiverAngle, double receiverSpeed, out double[] _autopassCoords)
        {
            double advance = GetAdvance(i);
            double _speed = GetSpeedForAdvance(j);
            double[] _ball = new double[2];
            _ball[0] = coords[0];
            _ball[1] = coords[1];
            double shortestDist = 1000;
            double prevDist = 1000;
            double[] advancePos = new double[2];

            double r;
            r = Math.Cos(F.ToRad * receiverAngle) * receiverSpeed * advance;
            advancePos[0] = receiverPosX + r;
            r = Math.Sin(F.ToRad * receiverAngle) * receiverSpeed * advance;
            advancePos[1] = receiverPosY + r;

            double _ballAngle = F.Angle(coords[0], coords[1],advancePos[0],advancePos[1]);

            while (true)
            {
                //move player
                r = Math.Cos(F.ToRad * receiverAngle) * receiverSpeed;
                receiverPosX += r;
                r = Math.Sin(F.ToRad * receiverAngle) * receiverSpeed;
                receiverPosY += r;

                //move ball
                r = Math.Cos(F.ToRad * _ballAngle) * _speed;
                _ball[0] += r;
                r = Math.Sin(F.ToRad * _ballAngle) * _speed;
                _ball[1] += r;


                _autopassCoords = new double[2]; //hmm, voiko tämä toimia
                _autopassCoords[0] = _ball[0];
               _autopassCoords[1] = _ball[1];

                _speed -= 0.0005;
                if (_speed < 0) return shortestDist;
                if (_speed < 0.015f) continue;
                if (_speed > 0.030f) continue;

                double d = F.Distance(_ball[0], _ball[1], receiverPosX, receiverPosY);

                if (d < shortestDist)
                    shortestDist = d;

                //distance starts to grow, so lets end simulation
                if (d > prevDist) return shortestDist;
                prevDist = d;
            }

            //while ballspeed=0 tai ball distance->player<0.05
            //alussa distance pienenee, mutta heti kun havaitaan, että distance alkaa suurenemaan, quitataan

        }

        double GetAdvance(int iID)
        {
            return iID * 5.0;
        }

        double GetSpeedForAdvance(int jID)
        {
            double _speed = 0;

            if (jID == 0) _speed = 0.040;
            if (jID == 1) _speed = 0.045;
            if (jID == 2) _speed = 0.050;
            if (jID == 3) _speed = 0.055;
            if (jID == 4) _speed = 0.060;
            if (jID == 5) _speed = 0.065;

            return _speed;
        }

        public void DoThrowinByPower(double power, double shootDirection)
        {
            angle = shootDirection;
            height = 0.27;

            if (power > 9)
            {
                speed = 0.045;
                zSpeed = 30;
                return;
            }
            if (power > 8)
            {
                speed = 0.0433;
                zSpeed = 25;
                return;
            }
            if (power > 7)
            {
                speed = 0.0416;
                zSpeed = 20;
                return;
            }
            if (power > 6)
            {
                speed = 0.04;
                zSpeed = 20;
                return;
            }
            if (power > 5)
            {
                speed = 0.0383;
                zSpeed = 15;
                return;
            }
            if (power > 4)
            {
                speed = 0.0366;
                zSpeed = 15;
                return;
            }
            if (power > 3)
            {
                speed = 0.035;
                zSpeed = 10;
                return;
            }
            if (power > 2)
            {
                speed = 0.0333;
                zSpeed = 10;
                return;
            }
            if (power > 1)
            {
                speed = 0.0316;
                zSpeed = 5;
                return;
            }

            speed = 0.03;
            zSpeed = 5;
        }


    }
}
