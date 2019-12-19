using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServerMono
{
    public struct PositionAndSide
    {
        public Position position;
        public Side side;
    }

    public enum Position
    {
        D,
        M,
        F
    }

    public enum Side
    {
        L,
        LC,
        C,
        RC,
        R
    }

    class PositionHandler
    {
        public static double[] GetWantedPos(Position p, Side s, int tID, int homeSide, double targetPosX, double targetPosY, int teamControlling, double offsideLine)
        {
            double[] res = new double[2];
            bool flip = false;

            if (p == Position.D) res[1] = 1.7;//2.1
            if (p == Position.M) res[1] = 0;
            if (p == Position.F) res[1] = -1.5;//-1.9

            if (s == Side.L) res[0] = 3.0;
            if (p == Position.D && s == Side.LC) res[0] = 2.0;
            if (p == Position.M && s == Side.LC) res[0] = 2.0;
            if (p == Position.F && s == Side.LC) res[0] = 1.5;
            if (s == Side.C) res[0] = 0.0;
            if (p == Position.D && s == Side.RC) res[0] = -2.0;
            if (p == Position.M && s == Side.RC) res[0] = -2.0;
            if (p == Position.F && s == Side.RC) res[0] = -1.5;
            if (s == Side.R) res[0] = -3.0;

            //if any team is controlling ball, teams will move more attacking/defending positions
            if (teamControlling > -1)
                if (teamControlling == tID) res[1] -= 1; else res[1] += 1;
                //jos tarvii saada paitsioita, korvaa yllä oleva tällä
                //if (teamControlling == tID) res[1] -= 5; else res[1] += 1;

            //res[0] += F.rand.NextDouble() - 0.5;
            //res[1] += F.rand.NextDouble() - 0.5;

            if (tID == 0 && homeSide == 1) flip = false;
            if (tID == 1 && homeSide == 1) flip = true;
            if (tID == 0 && homeSide == 2) flip = true;
            if (tID == 1 && homeSide == 2) flip = false;

            if (flip)
            {
                res[0] = 0 - res[0];
                res[1] = 0 - res[1];
            }

            res[0] += targetPosX * 0.3;
            res[1] += targetPosY * 0.5;

            //if ownteam controls, bot checks offside line
            if (teamControlling == tID)
            {
                if (!flip)
                    if (res[1] < offsideLine) res[1] = offsideLine;

                if (flip)
                    if (res[1] > offsideLine) res[1] = offsideLine;
            }

            res[0] += F.rand.NextDouble() - 0.5;
            res[1] += F.rand.NextDouble() - 0.5;

            if (res[0] > Field.THROWIN_P - 0.02) res[0] = Field.THROWIN_P - 0.02;
            if (res[0] < Field.THROWIN_N + 0.02) res[0] = Field.THROWIN_N + 0.02;
            if (res[1] > Field.GOALLINE_P - 0.02) res[1] = Field.GOALLINE_P - 0.02;
            if (res[1] < Field.GOALLINE_N + 0.02) res[1] = Field.GOALLINE_N + 0.02;

            return res;
        }

        static double CheckOffsideLine(double resY, int tID, int homeSide)
        {
            double res=0;
            byte upDown = 0;
            double offsideLine = 0;
            byte oppTID;
            if (tID == 0) oppTID = 1; else oppTID = 0;

            if (tID == 0 && homeSide == 1) upDown = 2;
            if (tID == 0 && homeSide == 2) upDown = 1;
            if (tID == 1 && homeSide == 1) upDown = 1;
            if (tID == 1 && homeSide == 2) upDown = 2;

            //offsideline by lowest opposition
            /*for (int i = 0; i < maxPlayers; i++)
            {
                //if (!IsPlayerOnline(oppTID, i) && !botsEnabled) continue;
                if (upDown == 1)
                {
                    if (users[oppTID, i].coords[1] > freekickCoords[1])
                        freekickCoords[1] = users[oppTID, i].coords[1];
                }
                else
                {
                    if (users[oppTID, i].coords[1] < freekickCoords[1])
                        freekickCoords[1] = users[oppTID, i].coords[1];
                }
            }*/

            return res;
        }

        public static double[] GetKickoffPos(Position p, Side s, int tID, int homeSide, int gotoKickoff)
        {
            double[] res = new double[2];
            bool flip = false;

            if (p == Position.D) res[1] = 4.0;
            if (p == Position.M) res[1] = 2.0;
            if (p == Position.F) res[1] = 1.0;

            if (s == Side.L) res[0] = 3.0;
            if (s == Side.LC) res[0] = 1.0;
            if (s == Side.RC) res[0] = -1.0;
            if (s == Side.R) res[0] = -3.0;

            res[0] += F.rand.NextDouble() / 5.0 - 0.1;
            res[1] += F.rand.NextDouble() / 5.0 - 0.1;

            //x += Random.Range(-0.2f, 0.2f);
            //y += Random.Range(-0.2f, 0.2f);

            //restrick that attacker wont start on opponents side (only happens, when 3 attackers)
            if (p == Position.F && res[1] < 0.05) res[1] = 0.05;

            if (gotoKickoff == 1)
            {
                res[0] = 0.05;
                res[1] = 0.05;
            }

            if (gotoKickoff == 2)
            {
                res[0] = -0.2;
                res[1] = 0.05;
            }

            //restrict that player wont go to kickoff circle
            /*if (gotoKickoff==-1)
            {
                if (F.Distance(res.ToArray(), new double[2]) < 1.3)
                {
                    a = F.Angle(res.ToArray(), new double[2]);
                    r = Math.Cos(F.ToRad * a) * 1.3;
                    res[0] += r;
                    r = Math.Sin(F.ToRad * a) * 1.3;
                    res[1] += r;
                }
            }*/

            if (tID == 0 && homeSide == 1) flip = false;
            if (tID == 1 && homeSide == 1) flip = true;
            if (tID == 0 && homeSide == 2) flip = true;
            if (tID == 1 && homeSide == 2) flip = false;

            if (flip)
            {
                res[0] = 0 - res[0];
                res[1] = 0 - res[1];
            }

            return res;
        }

        public static double[] GetGoalkickPos(Position p, Side s, int tID, int homeSide)
        {
            double[] res = new double[2];
            bool flip = false;

            if (p == Position.D) res[1] = 3;
            if (p == Position.M) res[1] = 1;
            if (p == Position.F) res[1] = -1;

            if (s == Side.L) res[0] = 3;
            if (s == Side.LC) res[0] = 2;
            if (s == Side.C) res[0] = 0;
            if (s == Side.RC) res[0] = -2;
            if (s == Side.R) res[0] = -3;

            res[0] += F.rand.NextDouble() - 0.5;
            res[1] += F.rand.NextDouble() - 0.5;

            if (tID == 0 && homeSide == 1) flip = false;
            if (tID == 1 && homeSide == 1) flip = true;
            if (tID == 0 && homeSide == 2) flip = true;
            if (tID == 1 && homeSide == 2) flip = false;

            if (flip)
            {
                res[0] = 0 - res[0];
                res[1] = 0 - res[1];
            }

            return res;
        }

        public static double[] GetThrowinPos(Position p, Side s, int tID, int homeSide, double targetPosX, double targetPosY)
        {
            double[] res = new double[2];

            if (targetPosY < 7.20f && targetPosY >= 4.32f)
            {
                if ((tID == 0 && homeSide == 1) || (tID == 1 && homeSide == 2))
                {
                    if (p == Position.D) res[1] = targetPosY;
                    if (p == Position.M) res[1] = targetPosY - 1;
                    if (p == Position.F) res[1] = targetPosY - 2;
                }
                else
                {
                    if (p == Position.F) res[1] = targetPosY;
                    if (p == Position.M) res[1] = targetPosY - 1;
                    if (p == Position.D) res[1] = targetPosY - 3;
                }
            }
            if (targetPosY < 4.32f && targetPosY >= -4.32f)
            {
                if ((tID == 0 && homeSide == 1) || (tID == 1 && homeSide == 2))
                {
                    if (p == Position.D) res[1] = targetPosY + 1.5f;
                    if (p == Position.M) res[1] = targetPosY;
                    if (p == Position.F) res[1] = targetPosY - 1;
                }
                else
                {
                    if (p == Position.F) res[1] = targetPosY + 1;
                    if (p == Position.M) res[1] = targetPosY;
                    if (p == Position.D) res[1] = targetPosY - 1.5f;
                }
            }
            if (targetPosY < -4.32f && targetPosY >= -7.20f)
            {
                if ((tID == 1 && homeSide == 1) || (tID == 0 && homeSide == 2))
                {
                    if (p == Position.D) res[1] = targetPosY;
                    if (p == Position.M) res[1] = targetPosY + 1;
                    if (p == Position.F) res[1] = targetPosY + 2;
                }
                else
                {
                    if (p == Position.F) res[1] = targetPosY;
                    if (p == Position.M) res[1] = targetPosY + 1;
                    if (p == Position.D) res[1] = targetPosY + 3;
                }
            }

            if (targetPosX > 0)
            {
                if ((tID == 0 && homeSide == 1) || (tID == 1 && homeSide == 2))
                {
                    if (s == Side.L) res[0] = Field.THROWIN_P - 0.5f;
                    if (s == Side.LC) res[0] = Field.THROWIN_P - 2.0f;
                    if (s == Side.C) res[0] = Field.THROWIN_P - 3.5f;
                    if (s == Side.RC) res[0] = Field.THROWIN_P - 5.0f;
                    if (s == Side.R) res[0] = Field.THROWIN_P - 6.5f;
                }
                else
                {
                    if (s == Side.L) res[0] = Field.THROWIN_P - 6.5f;
                    if (s == Side.LC) res[0] = Field.THROWIN_P - 5.0f;
                    if (s == Side.C) res[0] = Field.THROWIN_P - 3.5f;
                    if (s == Side.RC) res[0] = Field.THROWIN_P - 2.0f;
                    if (s == Side.R) res[0] = Field.THROWIN_P - 0.5f;
                }
            }
            else
            {
                if ((tID == 0 && homeSide == 1) || (tID == 1 && homeSide == 2))
                {
                    if (s == Side.L) res[0] = Field.THROWIN_N + 6.5f;
                    if (s == Side.LC) res[0] = Field.THROWIN_N + 5.0f;
                    if (s == Side.C) res[0] = Field.THROWIN_N + 3.5f;
                    if (s == Side.RC) res[0] = Field.THROWIN_N + 2.0f;
                    if (s == Side.R) res[0] = Field.THROWIN_N + 0.5f;
                }
                else
                {
                    if (s == Side.L) res[0] = Field.THROWIN_N + 0.5f;
                    if (s == Side.LC) res[0] = Field.THROWIN_N + 2.0f;
                    if (s == Side.C) res[0] = Field.THROWIN_N + 3.5f;
                    if (s == Side.RC) res[0] = Field.THROWIN_N + 5.0f;
                    if (s == Side.R) res[0] = Field.THROWIN_N + 6.5f;
                }
            }


            res[0] += F.rand.NextDouble() - 0.5;
            res[1] += F.rand.NextDouble() - 0.5;

            if (res[0] > Field.THROWIN_P - 0.02f) res[0] = Field.THROWIN_P - 0.02f;
            if (res[0] < Field.THROWIN_N + 0.02f) res[0] = Field.THROWIN_N + 0.02f;
            if (res[1] > Field.GOALLINE_P - 0.02f) res[1] = Field.GOALLINE_P - 0.02f;
            if (res[1] < Field.GOALLINE_N + 0.02f) res[1] = Field.GOALLINE_N + 0.02f;

            return res;
        }

        public static double[] GetCornerPos(double targetPosY, ref byte cornerPlrsSetted, bool isAttackingTeam)
        {
            double[] res = new double[2];
            bool disableRandomMove = false;
            double cornerTargerY;

            if (targetPosY > 0)
                cornerTargerY = Field.GOALKICK_YP - 0.5;
            else
                cornerTargerY = Field.GOALKICK_YN + 0.5;

            if (isAttackingTeam)
            {
                if (cornerPlrsSetted == 0)
                {
                    disableRandomMove = true;
                    res[0] = -1;
                    res[1] = 0;
                }
                if (cornerPlrsSetted == 1)
                {
                    disableRandomMove = true;
                    res[0] = 1;
                    res[1] = 0;
                }
                if (cornerPlrsSetted > 1) res[1] = cornerTargerY;
            }
            else
            {
                if (cornerPlrsSetted == 0)
                {
                    disableRandomMove = true;
                    res[1] = 0;
                }
                if (cornerPlrsSetted > 0) res[1] = cornerTargerY;
            }

            cornerPlrsSetted++;

            if (!disableRandomMove)
            {
                res[0] += (F.rand.NextDouble() - 0.5) * 2.5;
                res[1] += (F.rand.NextDouble() - 0.5) * 2.5;
            }

            if (res[0] > Field.THROWIN_P - 0.02f) res[0] = Field.THROWIN_P - 0.02f;
            if (res[0] < Field.THROWIN_N + 0.02f) res[0] = Field.THROWIN_N + 0.02f;
            if (res[1] > Field.GOALLINE_P - 0.02f) res[1] = Field.GOALLINE_P - 0.02f;
            if (res[1] < Field.GOALLINE_N + 0.02f) res[1] = Field.GOALLINE_N + 0.02f;

            return res;
        }

        public static double[] KickoffPositionToCoords(Position p, Side s)
        {
            double[] res = new double[2];

            if (p == Position.D) res[1] = 4.0;
            if (p == Position.M) res[1] = 2.0;
            if (p == Position.F) res[1] = 1.0;

            if (s == Side.L) res[0] = 3.0;
            if (s == Side.LC) res[0] = 1.0;
            if (s == Side.RC) res[0] = -1.0;
            if (s == Side.R) res[0] = -3.0;

            return res;
        }

        public static PositionAndSide GetDynamicPosition(int PIDCounter, int maxPlayers)
        {
            PositionAndSide p;
            p.position = Position.D;
            p.side = Side.C;

            #region 5 players
            if (maxPlayers == 5)
            {
                if (PIDCounter == 0)
                {
                    p.position = Position.D;
                    p.side = Side.C;
                }
                if (PIDCounter == 1)
                {
                    p.position = Position.D;
                    p.side = Side.LC;
                }
                if (PIDCounter == 2)
                {
                    p.position = Position.D;
                    p.side = Side.RC;
                }
                if (PIDCounter == 3)
                {
                    p.position = Position.F;
                    p.side = Side.LC;
                }
                if (PIDCounter == 4)
                {
                    p.position = Position.F;
                    p.side = Side.RC;
                }
                return p;
            }
            #endregion

            #region 6 players
            if (maxPlayers == 6)
            {
                if (PIDCounter == 0)
                {
                    p.position = Position.D;
                    p.side = Side.C;
                }
                if (PIDCounter == 1)
                {
                    p.position = Position.D;
                    p.side = Side.LC;
                }
                if (PIDCounter == 2)
                {
                    p.position = Position.D;
                    p.side = Side.RC;
                }
                if (PIDCounter == 3)
                {
                    p.position = Position.F;
                    p.side = Side.LC;
                }
                if (PIDCounter == 4)
                {
                    p.position = Position.M;
                    p.side = Side.C;
                }
                if (PIDCounter == 5)
                {
                    p.position = Position.F;
                    p.side = Side.RC;
                }
                return p;
            }
            #endregion



            return p;
        }

        public static double[] GetDynamicCoords(PositionAndSide p, int tID, int homeSide)
        {
            double[] res = new double[2];
            bool flip = false;

            if (p.position == Position.D) res[1] = Field.GOALKICK_YP;
            if (p.position == Position.M) res[1] = 0;
            if (p.position == Position.F) res[1] = Field.GOALKICK_YN;

            if (p.side == Side.L) res[0] = Field.THROWIN_P;
            if (p.side == Side.LC) res[0] = Field.THROWIN_P / 2.0;
            if (p.side == Side.C) res[0] = 0.0;
            if (p.side == Side.RC) res[0] = Field.THROWIN_N / 2.0;
            if (p.side == Side.R) res[0] = Field.THROWIN_N;

            if (tID == 0 && homeSide == 1) flip = false;
            if (tID == 1 && homeSide == 1) flip = true;
            if (tID == 0 && homeSide == 2) flip = true;
            if (tID == 1 && homeSide == 2) flip = false;

            if (flip)
            {
                res[0] = 0 - res[0];
                res[1] = 0 - res[1];
            }

            return res;
        }

    }
}
