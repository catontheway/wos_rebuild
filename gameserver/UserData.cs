using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Windows;

namespace GameServerMono
{
    class UserData
    {
        public NetConnection connection;
        public int pID = 0;     //database id
        public int tID = 0;     //database team id
        public string username = "";
        public int uniquePID;  //with this, we can idenfy user, as we cannot user username, incase user is Guest
        public PositionAndSide pos;
        public PositionAndSide AIDynamicpos;
        public bool dynamicPosFound;
        public bool AIDoPress;
        public int AIPressDelay;
        public double AIBallInPossesTime;
        public string nation;
        public bool isVip;
        public byte admas;
        public byte body;
        public byte skin;
        public byte hair;
        public byte number;
        public byte[] shoe = new byte[3];
        public bool ready;
        public ControlMethod controlMethod;       
        public byte keyboardButton; //WASD
        public double timeout;
        //public bool isComputer;
        //public bool isJoystick;
        public double[] clickCoords = new double[2];
        public double[] controlDir = new double[2];
        public double controlDistance;
        public double distToBall;
        public bool[] buttons = new bool[3]; //0=shoot, 1=auto pass&direct pass/tackle, 2=cross
        public double[] buttonPowers = new double[3];
        //public bool slideTackle = false;
        public int slideDelay = 0;
        public int fallDelay = 0;
        public int playedMins = 0;  //for experience
        public bool automoveFromFreekickArea = false;
        public double[] _oldCoords = new double[2];
        public double[] coords = new double[2];
        public double[] kickoffCoords = new double[2];
        public double angle;
        public double direction;
        public double speed;
        public int touchDelay;
        public bool offside;
        public bool gotoKickoff;
        public bool slowMoving = true;
        public int[] playedPos = new int[4];  //1=up, 2=down, 3=left, 4=right
        public double[] wantedPos = new double[2];  //for AI
        public bool gotoAutopass;
        public bool udpTrafficEnabled;
        public byte ownGoalCount;  //for banning

        public double maxAcceleration = 0.0012;

        public UserData(RoomType roomType)
        {
            coords[0] = Field.BENCHX;
            coords[1] = 0;

            //randomize bots skin&hair
            skin = (byte)F.rand.Next(0, 2);
            if (skin == 0)
                hair = (byte)F.rand.Next(0, 4);
            else
                hair = (byte)F.rand.Next(0, 2);

            //in public rooms, bots are random, but in challenges, all bots have normal sized bodies
            /*if (roomType == RoomType.Public)
                body = (byte)F.rand.Next(0, 3);
            else
                body = 1;*/

            //all bots are normal body
            body = 1;

        }

        public UserData(
            NetConnection conn, int tID, int pID, string nation, string username, bool isVip,
           byte admas, byte body, byte skin, byte hair, byte number, /*byte[] shoe,*/ int uniquePID)
        {
            this.timeout = NetTime.Now + 30;
            this.connection = conn;
            this.pID = pID;
            this.tID = tID;
            this.nation = nation;
            this.username = username;
            this.isVip = isVip;
            this.admas = admas;
            this.body = body;
            this.skin = skin;
            this.hair = hair;
            this.number = number;
            //this.shoe = shoe.ToArray();  moista poistaa toArray, jos otat tämän käyttöön
            this.uniquePID = uniquePID;

            coords[0] = -6;
            coords[1] = 0;
        }

        public void SetButtons(byte _buttons)
        {
            for (int i = 0; i < 3; i++)
                buttons[i] = false;

            if (_buttons == 1) buttons[0] = true;
            if (_buttons == 10) buttons[1] = true;
            if (_buttons == 100) buttons[2] = true;
            if (_buttons == 11)
            {
                buttons[0] = true;
                buttons[1] = true;
            }
            if (_buttons == 101)
            {
                buttons[0] = true;
                buttons[2] = true;
            }
            if (_buttons == 110)
            {
                buttons[1] = true;
                buttons[2] = true;
            }
            if (_buttons == 111)
            {
                buttons[0] = true;
                buttons[1] = true;
                buttons[2] = true;
            }
        }

        public void Move()
        {
            double r;
            r = Math.Cos(F.ToRad * direction) * speed;
            coords[0] += r;
            r = Math.Sin(F.ToRad * direction) * speed;
            coords[1] += r;
        }

        public double GetMaxSpeed()
        {
            double res = 0.02;

            if (IsHumanPlayer() && !isVip) body = 1;

            /*if (body == 0) res = 0.02436;  //0.021
            if (body == 1) res = 0.0232;  //0.020
            if (body == 2) res = 0.02262;*/
            //0.0195

            if (body == 0) res = 0.021;  //0.021
            if (body == 1) res = 0.020;  //0.020
            if (body == 2) res = 0.0195;           //0.0195

            return res;
        }

        public double GetAIMaxSpeed(RoomType roomType)
        {
            if (roomType == RoomType.Public)
                return 0.018;
            else
                return 0.020;
        }

        public double GetAcceleration()
        {
            if (controlMethod == ControlMethod.Mouse)
            {
                double res = 0;
                //double dist = F.Distance(coords.ToArray(),clickCoords.ToArray());

                if (controlDistance <= 19 && controlDistance > 14)
                    res = 0.000696;
                if (controlDistance <= 24 && controlDistance > 19)
                    res = 0.000928;
                if (controlDistance > 24)
                    res = 0.001392;

                return res;
            }

            if (controlMethod == ControlMethod.Joystick || controlMethod == ControlMethod.Mobile)
            {
                if (controlDir[0] == 0 && controlDir[1] == 0) return 0;
                return maxAcceleration;
            }

            if (controlMethod == ControlMethod.MouseKeyboard)
            {
                if (keyboardButton == 0) return 0;
                return maxAcceleration;
            }

            return 0;

        }

        public double GetSlideTackleSpeed()
        {
            double res = 0.0174;

            return res;
        }

        public double GetSlideTackleArea()
        {
            double res = 0.13;//13

            if (IsHumanPlayer() && !isVip) body = 1;

            if (body == 0) res = 0.10;//10
            if (body == 1) res = 0.13;//13
            if (body == 2) res = 0.16;//16

            return res;
        }

        public double GetVolleyHeight()
        {
            double res = 0.24;

            if (IsHumanPlayer() && !isVip) body = 1;

            if (body == 0) res = 0.21;
            if (body == 1) res = 0.24;
            if (body == 2) res = 0.27;

            return res;
        }

        bool IsHumanPlayer()
        {
            if (pID > 0)
                return true;
            else
                return false;
        }

        public bool IsMouseAiming()
        {
            if (controlMethod == ControlMethod.Mouse || controlMethod == ControlMethod.MouseKeyboard)
                return true;
            else
                return false;
        }

        public double GetKeyboardAngle()
        {
            if (keyboardButton == 1) return 225;
            if (keyboardButton == 2) return 270;
            if (keyboardButton == 3) return 315;

            if (keyboardButton == 4) return 180;
            if (keyboardButton == 6) return 0;

            if (keyboardButton == 7) return 135;
            if (keyboardButton == 8) return 90;
            if (keyboardButton == 9) return 45;

            return 0;
        }

    }
}
