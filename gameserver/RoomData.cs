using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Diagnostics;

namespace GameServerMono
{
    enum RoomState
    {
        ReadyScreen,
        Running,
        //StatsScreen
    }

    enum RoomType
    {
        Public,
        Challenge
    }

    enum ControlMethod
    {
        Mobile,
        Joystick,
        Mouse,
        MouseKeyboard
    }

    class RoomData
    {
        NetServer server;
        NetClient clientToMS;
        public UserData[,] users;
        public Keeper[] keepers = new Keeper[2];
        public TeamData[] teams = new TeamData[2];
        public int uniqueID;
        public RoomType roomType;
        public RoomState roomState;
        public bool officiallyStarted;
        public int fixtureID;
        public Ball ball;
        public byte maxPlayers;
        public bool statsWindowVisible;
        public byte time;  //ingame played minutes
        public byte homeSide;   //1=home side is upper, 2=home side is lower
        public byte period;
        public bool timerEnabled = false;
        public int timeTicker = 0;
        public int kickoff = -1;    //0-1      which team takes kickoff, 0=home, 1=away
        public int kickoffAtBegin = -1; //0-1  this is used to save, who had kickoff at begin of game, so correct team will start second period
        public byte autoMoving = 0;     //1=players runs to kickoff positions, 2=players runs to bench
        public byte ballInNetDelayer = 0;
        public int cancelFreekickDelayer = 0;
        //public bool flagged = false;
        byte keeperGoalkickScript;
        byte keeperGoalkickScriptDelay;
        public double[] keeperDistanceCoords = new double[2];   //coordinates, where keeper needs to run
        public bool botsEnabled;
        int throwInDelay;
        int cornerDelay;
        bool cornerInProgress;
        int freekickDelay;
        int dynamicPosDelayer;
        bool removeBlockAreaMSGSent;
        public int timeout;
        public int autostartTimeout; //for league matches
        bool offsideFlagging = false;

        public int throwInTID = -1;  //0-1
        public int goalkickTID = -1;//0-1
        public int cornerTID = -1;//0-1
        public int freekickTID = -1;//0-1
        public bool isPenalty;
        public int fouledTID = -1;
        public int foulDelay = 0;
        public int throwInTakerPID = -1;//0-1
        public int cornerTakerPID = -1;//0-1
        public int freekickTakerPID = -1;//0-1
        public int[] lastControllersTID = new int[2];     //0-1
        public int[] lastControllersPID = new int[2];     //0-1
        int currentControllerTID;
        int currentControllerPID;
        public int disabledPID = -1;
        public int disabledTID = -1;
        public int keeperDistanceShot;
        public byte keeperCornerWaitDelay = 0;   //keeper wont move during corner
        public byte keeperReaction = 0;
        byte udpSkip;
        public double[] throwInCoords = new double[2];
        public double[] goalkickCoords = new double[2];
        public double[] cornerCoords = new double[2];
        public double[] freekickCoords = new double[2];
        bool[] potentialReceiver;

        List<ChallengePlayerData> challengePlayerData2 = new List<ChallengePlayerData>();
        List<GoalData> goalData = new List<GoalData>();
        public List<SpectatorData> spectatorData2 = new List<SpectatorData>();

        int slideDuration = 70;

        public RoomData(bool officiallyStarted, int fixtureID, NetServer server, NetClient clientToMS, RoomType roomType, int uniqueID, TeamData teams0, TeamData teams1, bool botsEnabled, byte maxPlayers)
        {
            this.server = server;
            this.clientToMS = clientToMS;
            this.roomType = roomType;
            this.roomState = RoomState.ReadyScreen;
            this.officiallyStarted = officiallyStarted;
            this.fixtureID = fixtureID;
            if (this.officiallyStarted) autostartTimeout = 300;
            this.botsEnabled = botsEnabled;
            this.uniqueID = uniqueID;
            this.maxPlayers = maxPlayers;
            potentialReceiver = new bool[maxPlayers];

            users = new UserData[2, maxPlayers];

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j] = new UserData(this.roomType);

            this.teams[0] = teams0;
            this.teams[1] = teams1;

            for (int i = 0; i < 2; i++)
                keepers[i] = new Keeper();
        }

        public void GameLoop(int roomID)
        {
            if (roomState == RoomState.ReadyScreen) return;

            CountAngles();

            CountPlayersDistanceToBall();

            CountTime();


            AutoMovePlayers();

            BallControl();



            PowerBar(1);
            BallKickingA();
            BallKickingB();  //in mobile: autopass&direct pass, in mouse: not used
            BallKickingMobileVolley();
            BallKickingC();  //in mouse: RMB. in mobile: crossing
            AIBallKicking();
            AIBallPassing();

            GKAI();
            ball.BallCalculations();


            WhistlePossibleOffside();
            //WhistleFreekick();



            CalculateAIWantedPos();

            SlideTackle();

            MovePlayers();
            AIMovePlayers();
            PlayerCollision();


            Lines();
            Delayers();
            CancelFreekickDelayer();

            ThrowIn();
            Corner();
            Offside();

            PowerBar(2);
            BroadcastGamedata();




        }

        void NextPeriod()
        {
            statsWindowVisible = false;

            //halftime gone, lets begin second period
            if (period == 1)
            {
                roomState = RoomState.Running;
                period = 2;
                time = 45;
                timeTicker = 0;
                for (int i = 0; i < 2; i++)
                    ball.coords[i] = 0;
                ball.speed = 0;
                ball.zSpeed = 0;
                ball.height = 0;
                if (homeSide == 1) homeSide = 2; else homeSide = 1;
                if (kickoffAtBegin == 0) kickoff = 1; else kickoff = 0;
                SetKickOffCoords();
                SetPlrCoordsToBench();
                autoMoving = 1;
                BroadcastCloseStatsWindow();
            }
            //fulltime gone, lets switch to ready screen
            else
            {
                roomState = RoomState.ReadyScreen;
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                        users[i, j].ready = false;
                ball.speed = 0;
                ball.zSpeed = 0;
                ball.height = 0;
                BroadcastCloseStatsWindow();
            }

        }

        void CountTime()
        {
            if (!timerEnabled) return;

            CountPreferredPosition();
            CountPossession();

            #region cancel challenge, if another/both teams doesnt have players
            bool cancelChallenge = CancelChallengeIfNoPlayers();
            if (cancelChallenge)
            {
                period = 2;
                time = 91;

                int[] _plrCounts = new int[2];

                for (int i = 0; i < 2; i++)
                    _plrCounts[i] = CountPlayersInTeam(i);

                int _goalDiff = 0;

                if (_plrCounts[0] > _plrCounts[1])
                {
                    //if quitted team were leading, lets set them losing
                    if (teams[1].score > teams[0].score)
                    {
                        teams[0].score = 3;
                        teams[1].score = 0;
                    }

                    _goalDiff = teams[0].score - teams[1].score;
                    if (_goalDiff < 3)
                    {
                        teams[0].score = 3;
                        teams[1].score = 0;
                    }
                }
                if (_plrCounts[1] > _plrCounts[0])
                {
                    //if quitted team were leading, lets set them losing
                    if (teams[0].score > teams[1].score)
                    {
                        teams[1].score = 3;
                        teams[0].score = 0;
                    }

                    _goalDiff = teams[1].score - teams[0].score;
                    if (_goalDiff < 3)
                    {
                        teams[1].score = 3;
                        teams[0].score = 0;
                    }
                }
            }
            #endregion

            timeTicker++;
            if (timeTicker > 333)//333
            {
                timeTicker = 0;
                time++;
                CountPlayedMinutesForPlayers();
                if (period == 1 && time > 45) time = 46;
                if (time > 90) time = 91;
            }

            //lets not do finalwhistle, if ball isnt near halfline
            if (!cancelChallenge)
                if (!IsBallNearMidfield()) return;

            //halftime//finalwhistle
            if ((period == 1 && time > 45) || (period == 2 && time > 90))
            {
                ResetOwnGoalCounts();
                ResetSlideTacklesAndFallDelays();
                ResetOffside();
                autoMoving = 2;
                timerEnabled = false;
                statsWindowVisible = true;
                SoundBroadcast(3, timerEnabled);
                for (int i = 0; i < 2; i++)
                {
                    lastControllersPID[i] = -1;
                    lastControllersTID[i] = -1;
                }
                ResetAutopassReceivers();

                if (cancelChallenge)
                {
                    if (roomType == RoomType.Challenge) SendCancelledChallengeToMasterserver();
                }
                else
                {
                    if (period == 2 && roomType == RoomType.Challenge) SendFinalScoreAndStatsToMasterserver();
                }
                //SendExperienceToMasterserver();

                BroadcastHafttimeStatsToClients();
            }
        }

        bool CancelChallengeIfNoPlayers()
        {
            if (roomType == RoomType.Public) return false;

            if (CountPlayersInTeam(0) == 0 || CountPlayersInTeam(1) == 0)
                return true;
            else
                return false;
        }

        void ResetOwnGoalCounts()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j].ownGoalCount = 0;
        }

        void SendCancelledChallengeToMasterserver()
        {
            officiallyStarted = false;

            NetOutgoingMessage outmsg = clientToMS.CreateMessage();
            outmsg.Write((byte)92);

            outmsg.Write(fixtureID);
            fixtureID = 0;

            #region basic data
            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(teams[i].tID);
                outmsg.Write(teams[i].score);
            }
            #endregion

            #region player data

            int plrCount = 0;

            //player count, if player have played in match (we dont want to include those players, which have just visited at ready screen but left, before match have begin)
            lock (challengePlayerData2)
            {
                for (int i = 0; i < challengePlayerData2.Count; i++)
                    if (challengePlayerData2[i].timePlayed > 0) plrCount++;

                outmsg.Write(plrCount);

                for (int i = 0; i < challengePlayerData2.Count; i++)
                {
                    if (challengePlayerData2[i].timePlayed == 0) continue;

                    outmsg.Write(challengePlayerData2[i].tID);
                    outmsg.Write(challengePlayerData2[i].pID);
                }
            }
            #endregion

            clientToMS.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        void SendFinalScoreAndStatsToMasterserver()
        {
            officiallyStarted = false;

            double[] _pos = new double[2];
            _pos[0] = 50;
            _pos[1] = 50;

            if (teams[0].possession > 0 && teams[1].possession > 0)
            {
                int totalPos = teams[0].possession + teams[1].possession;

                _pos[0] = 100.0 / totalPos * teams[0].possession;
                _pos[1] = 100 - _pos[0];
            }

            NetOutgoingMessage outmsg = clientToMS.CreateMessage();
            outmsg.Write((byte)48);

            outmsg.Write(fixtureID);
            fixtureID = 0;

            #region basic data
            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(teams[i].tID);
                outmsg.Write(teams[i].score);
                outmsg.Write(teams[i].goalKicks);
                outmsg.Write(teams[i].corners);
                outmsg.Write(teams[i].throwIns);
                outmsg.Write(teams[i].offsides);
                outmsg.Write(teams[i].shotsTotal);
                outmsg.Write(teams[i].shotsOnGoal);
                outmsg.Write((byte)Math.Round(_pos[i]));
            }
            #endregion

            #region player data

            int plrCount = 0;

            //player count, if player have played in match (we dont want to include those players, which have just visited at ready screen but left, before match have begin)
            lock (challengePlayerData2)
            {
                for (int i = 0; i < challengePlayerData2.Count; i++)
                    if (challengePlayerData2[i].timePlayed > 0) plrCount++;

                outmsg.Write(plrCount);

                for (int i = 0; i < challengePlayerData2.Count; i++)
                {
                    if (challengePlayerData2[i].timePlayed == 0) continue;

                    outmsg.Write(challengePlayerData2[i].tID);
                    outmsg.Write(challengePlayerData2[i].pID);
                    outmsg.Write(challengePlayerData2[i].timePlayed);
                    outmsg.Write(challengePlayerData2[i].shotsTotal);
                    outmsg.Write(challengePlayerData2[i].shotsOnTarget);
                    outmsg.Write(challengePlayerData2[i].offsides);
                    outmsg.Write(challengePlayerData2[i].posUP);
                    outmsg.Write(challengePlayerData2[i].posDown);
                    outmsg.Write(challengePlayerData2[i].posLeft);
                    outmsg.Write(challengePlayerData2[i].posRight);
                    outmsg.Write(challengePlayerData2[i].teamGoals);
                }
            }
            #endregion

            #region goal data
            lock (goalData)
            {
                outmsg.Write(goalData.Count);

                for (int i = 0; i < goalData.Count; i++)
                {
                    outmsg.Write(goalData[i].scorer);
                    outmsg.Write(goalData[i].assister);
                    outmsg.Write(goalData[i].goalTime);
                    outmsg.Write(goalData[i].teamScored);
                    outmsg.Write(goalData[i].bothTeamsHave5Players);
                }
            }
            #endregion

            clientToMS.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        void CountPreferredPosition()
        {

            bool isLeft = false;
            bool isUp = false;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (users[i, j].pID == 0) continue;

                    //home attacks up->down, away down->up
                    if (homeSide == 1)
                    {
                        if (i == 0) //home teams player
                        {
                            if (users[i, j].coords[0] > ball.coords[0]) isLeft = true; else isLeft = false;
                            if (users[i, j].coords[1] > ball.coords[1]) isUp = false; else isUp = true;
                        }
                        if (i == 1) //away teams player
                        {
                            if (users[i, j].coords[0] > ball.coords[0]) isLeft = false; else isLeft = true;
                            if (users[i, j].coords[1] > ball.coords[1]) isUp = true; else isUp = false;
                        }
                    }

                    //home attacks down->up, away up->down
                    if (homeSide == 2)
                    {
                        if (i == 0) //home teams player
                        {
                            if (users[i, j].coords[0] > ball.coords[0]) isLeft = false; else isLeft = true;
                            if (users[i, j].coords[1] > ball.coords[1]) isUp = true; else isUp = false;
                        }
                        if (i == 1) //away teams player
                        {
                            if (users[i, j].coords[0] > ball.coords[0]) isLeft = true; else isLeft = false;
                            if (users[i, j].coords[1] > ball.coords[1]) isUp = false; else isUp = true;
                        }
                    }

                    if (isUp)
                        users[i, j].playedPos[0]++;
                    else
                        users[i, j].playedPos[1]++;

                    if (isLeft)
                        users[i, j].playedPos[2]++;
                    else
                        users[i, j].playedPos[3]++;

                    lock (challengePlayerData2)
                    {
                        for (int k = 0; k < challengePlayerData2.Count; k++)
                            if (challengePlayerData2[k].pID == users[i, j].pID)
                            {
                                if (isUp)
                                    challengePlayerData2[k].posUP++;
                                else
                                    challengePlayerData2[k].posDown++;

                                if (isLeft)
                                    challengePlayerData2[k].posLeft++;
                                else
                                    challengePlayerData2[k].posRight++;
                            }
                    }


                }
        }

        void CountPossession()
        {
            if (lastControllersPID[0] == -1) return;
            if (lastControllersTID[0] == -1) return;

            teams[lastControllersTID[0]].possession++;
        }

        void CountPlayedMinutesForPlayers()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].pID > 0)
                    {
                        users[i, j].playedMins++;

                        lock (challengePlayerData2)
                        {
                            for (int k = 0; k < challengePlayerData2.Count; k++)
                                if (challengePlayerData2[k].pID == users[i, j].pID)
                                    challengePlayerData2[k].timePlayed++;
                        }

                    }
        }

        void AddOffsideStat(int pID)
        {
            if (roomType == RoomType.Public) return;
            if (pID == 0) return;

            lock (challengePlayerData2)
            {
                for (int i = 0; i < challengePlayerData2.Count; i++)
                    if (challengePlayerData2[i].pID == pID)
                    {
                        challengePlayerData2[i].offsides++;
                        break;
                    }
            }
        }

        public void AddShotStat(bool onTarget)
        {
            int pID = lastControllersPID[0];
            int tID = lastControllersTID[0];

            if (pID == -1 || tID == -1) return; //just to be safe, that server wont crash

            teams[tID].shotsTotal++;
            if (onTarget) teams[tID].shotsOnGoal++;

            if (roomType == RoomType.Public) return;

            lock (challengePlayerData2)
            {
                for (int i = 0; i < challengePlayerData2.Count; i++)
                    if (challengePlayerData2[i].pID == users[tID, pID].pID)
                    {
                        challengePlayerData2[i].shotsTotal++;
                        if (onTarget) challengePlayerData2[i].shotsOnTarget++;
                        break;
                    }
            }

        }

        public void AddGoalStats(byte tIDScored, bool ownGoal)
        {
            if (roomType != RoomType.Challenge) return;

            int scorer = 0;
            int assister = 0;
            bool bothTeamsHave5Players = true;

            int pID1 = lastControllersPID[0];
            int pID2 = lastControllersPID[1];
            int tID1 = lastControllersTID[0];
            int tID2 = lastControllersTID[1];

            int bannedPID = 0;
            byte[] plrCount = new byte[2];

            //this is for banning purposes
            if (ownGoal)
            {
                if (pID1 > -1 && tID1 > -1)
                {
                    if (IsPlayerOnline(tID1, pID1))
                        users[tID1, pID1].ownGoalCount++;

                    if (users[tID1, pID1].ownGoalCount > 2)
                        bannedPID = users[tID1, pID1].pID;
                }
            }

            #region both teams need to have least 5 players

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].pID > 0)
                        plrCount[i]++;

            if (plrCount[0] < 5 || plrCount[1] < 5) bothTeamsHave5Players = false;

            #endregion

            if (bannedPID > 0)
            {
                NetOutgoingMessage outmsg = clientToMS.CreateMessage();
                outmsg.Write((byte)87);
                outmsg.Write(bannedPID);
                clientToMS.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
            }

            if (pID1 > -1 && tID1 > -1)
                scorer = users[tID1, pID1].pID;

            if (pID2 > -1 && tID2 > -1)
                if (tID1 == tID2 && !ownGoal)
                    assister = users[tID2, pID2].pID;

            lock (goalData)
            {
                goalData.Add(new GoalData(scorer, assister, time, tIDScored, bothTeamsHave5Players));
            }

            //calculate team goals
            if (bothTeamsHave5Players)
                lock (challengePlayerData2)
                {
                    for (int i = 0; i < 2; i++)
                        for (int j = 0; j < maxPlayers; j++)
                            if (users[i, j].pID > 0)
                                for (int k = 0; k < challengePlayerData2.Count; k++)
                                    if (challengePlayerData2[k].pID == users[i, j].pID)
                                    {
                                        if (challengePlayerData2[k].tID == tIDScored)
                                            challengePlayerData2[k].teamGoals++;
                                        else
                                            challengePlayerData2[k].teamGoals--;
                                        break;
                                    }
                }

        }

        public void SendTrainingGoalData(byte tIDScored, bool ownGoal)
        {
            if (roomType != RoomType.Public) return;

            byte[] plrCount = new byte[2];

            int bannedPID = 0;

            //this is for banning purposes
            if (ownGoal)
            {
                int pID1 = lastControllersPID[0];
                int tID1 = lastControllersTID[0];

                if (pID1 > -1 && tID1 > -1)
                {
                    if (IsPlayerOnline(tID1, pID1))
                        users[tID1, pID1].ownGoalCount++;

                    if (users[tID1, pID1].ownGoalCount > 2)
                        bannedPID = users[tID1, pID1].pID;
                }
            }

            //both teams need to have least 5 players
            if (bannedPID == 0)
            {
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                        if (users[i, j].pID > 0)
                            plrCount[i]++;

                if (plrCount[0] < 5 || plrCount[1] < 5) return;
            }

            int scorer = 0;
            int assister = 0;

            if (!ownGoal)
            {
                int pID1 = lastControllersPID[0];
                int pID2 = lastControllersPID[1];
                int tID1 = lastControllersTID[0];
                int tID2 = lastControllersTID[1];

                if (pID1 > -1 && tID1 > -1)
                    scorer = users[tID1, pID1].pID;

                if (pID2 > -1 && tID2 > -1)
                    if (tID1 == tID2)
                        assister = users[tID2, pID2].pID;
            }

            NetOutgoingMessage outmsg = clientToMS.CreateMessage();
            outmsg.Write((byte)54);

            outmsg.Write(maxPlayers);
            outmsg.Write(tIDScored);
            outmsg.Write(scorer);
            outmsg.Write(assister);
            outmsg.Write(bannedPID);

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    outmsg.Write(users[i, j].pID);

            clientToMS.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        bool IsBallNearMidfield()
        {
            if (ball.coords[1] < 1.5 && ball.coords[1] > -1.5)
                return true;
            else
                return false;
        }

        void CountAngles()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (IsPlayerOnline(i, j))
                        users[i, j].angle = F.Angle(0, 0, users[i, j].controlDir[0], users[i, j].controlDir[1]);

        }

        void CountPlayersDistanceToBall()
        {
            for (int i = 0; i < 2; i++)
                keepers[i].distToBall = F.Distance(keepers[i].coords[0], keepers[i].coords[1], ball.coords[0], ball.coords[1]);

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j].distToBall = F.Distance(users[i, j].coords[0], users[i, j].coords[1], ball.coords[0], ball.coords[1]);
        }

        void AutoMovePlayers()
        {
            if (autoMoving == 0) return;

            //if players are going to bench, override kickoff coords
            if (autoMoving == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    keepers[i].kickoffCoords[0] = Field.BENCHX;
                    keepers[i].kickoffCoords[1] = 0;
                    for (int j = 0; j < maxPlayers; j++)
                    {
                        users[i, j].kickoffCoords[0] = Field.BENCHX;
                        users[i, j].kickoffCoords[1] = 0;
                    }
                }

            }

            //this is used to count, if player have reached his kickoff positio. if count=servers[sID].maxPlayers*2 then all players at positions
            byte count = 0;

            //keepers
            for (int i = 0; i < 2; i++)
            {
                if (keepers[i].divingDelay > 0) continue;
                //keeper havent reached his positio yet, so lets move him automatically
                if (F.Distance(keepers[i].coords[0], keepers[i].coords[1], keepers[i].kickoffCoords[0], keepers[i].kickoffCoords[1]) > 0.03)
                {
                    keepers[i].speed = keepers[i].GetMaxSpeed();
                    keepers[i].angle = F.Angle(keepers[i].coords[0], keepers[i].coords[1], keepers[i].kickoffCoords[0], keepers[i].kickoffCoords[1]);
                    keepers[i].Move();
                }
                //keeper have reached his position, so lets stop him
                else
                {
                    keepers[i].coords[0] = keepers[i].kickoffCoords[0];
                    keepers[i].coords[1] = keepers[i].kickoffCoords[1];
                    keepers[i].speed = 0;
                    keepers[i].angle = F.Angle(keepers[i].coords[0], keepers[i].coords[1], 0, 0);
                    count++;
                }
            }


            //players
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (!botsEnabled)
                        if (!IsPlayerOnline(i, j))
                        {
                            count++;
                            continue;
                        }

                    //player havent reached his positio yet, so lets move him automatically
                    if (F.Distance(users[i, j].coords[0], users[i, j].coords[1], users[i, j].kickoffCoords[0], users[i, j].kickoffCoords[1]) > 0.03)
                    {
                        users[i, j].speed = users[i, j].GetMaxSpeed();
                        users[i, j].direction = F.Angle(users[i, j].coords[0], users[i, j].coords[1], users[i, j].kickoffCoords[0], users[i, j].kickoffCoords[1]);
                        users[i, j].Move();
                    }
                    //player have reached his position, so lets stop him
                    else
                    {
                        users[i, j].coords[0] = users[i, j].kickoffCoords[0];
                        users[i, j].coords[1] = users[i, j].kickoffCoords[1];
                        users[i, j].speed = 0;
                        users[i, j].direction = F.Angle(users[i, j].coords[0], users[i, j].coords[1], 0, 0);
                        count++;
                    }
                }

            //*****************

            if (ballInNetDelayer > 0) return;

            //lets check, if all players are at their positions
            if (count == (maxPlayers * 2) + 2)
            {
                //players are on kickoff positions. let the game begin
                if (autoMoving == 1)
                {
                    autoMoving = 0;
                    timerEnabled = true;
                    SoundBroadcast(10, timerEnabled);
                }

                //players are on bench
                if (autoMoving == 2)
                {
                    ball.coords[0] = 0;
                    ball.coords[1] = 0;
                    autoMoving = 0;
                    NextPeriod();
                }
            }
        }

        void CalculateAIWantedPos()
        {
            if (autoMoving > 0) return;
            if (!botsEnabled) return;

            CalculateDynamicPositions();
            CalculatePressingPlayer();
            AIStartSlide();

            int botToFreekickPID = -1;
            int botToFreekickTID = -1;

            if (throwInTID > -1 && ball.speed == 0 && throwInTakerPID == -1)
            {
                botToFreekickPID = GetNearestBot(throwInTID, throwInCoords[0], throwInCoords[1]);
                botToFreekickTID = throwInTID;
            }
            if (cornerTID > -1 && ball.speed == 0 && cornerTakerPID == -1)
            {
                botToFreekickPID = GetNearestBot(cornerTID, cornerCoords[0], cornerCoords[1]);
                botToFreekickTID = cornerTID;
            }
            if (freekickTID > -1 && ball.speed == 0 && freekickTakerPID == -1)
            {
                botToFreekickPID = GetNearestBot(freekickTID, freekickCoords[0], freekickCoords[1]);
                botToFreekickTID = freekickTID;
            }

            byte cornerPlrsSetted;

            for (int i = 0; i < 2; i++)
            {
                cornerPlrsSetted = 0;

                for (int j = 0; j < maxPlayers; j++)
                {
                    if (IsPlayerOnline(i, j)) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].automoveFromFreekickArea) continue;
                    if (users[i, j].touchDelay > 0) continue;

                    //check, if bot is ordered to throwin/corner/freekick
                    if (i == botToFreekickTID && j == botToFreekickPID)
                    {
                        users[i, j].wantedPos[0] = ball.coords[0];
                        users[i, j].wantedPos[1] = ball.coords[1];
                        users[i, j].direction = F.Angle(users[i, j].coords[0], users[i, j].coords[1], ball.coords[0], ball.coords[1]);
                        continue;
                    }

                    //AI go to autopass
                    if (users[i, j].gotoAutopass)
                    {
                        users[i, j].wantedPos[0] = ball.autopassCoords[0];
                        users[i, j].wantedPos[1] = ball.autopassCoords[1];

                        //lets stop "goto autopass" if player is near that position
                        if (F.Distance(users[i, j].coords[0], users[i, j].coords[1], users[i, j].wantedPos[0], users[i, j].wantedPos[1]) < 0.05)
                        {
                            users[i, j].gotoAutopass = false;
                            users[i, j].AIDoPress = true;
                        }

                        continue;
                    }

                    //AI ordered to press
                    if (users[i, j].AIDoPress)
                    {
                        users[i, j].AIPressDelay--;

                        if (users[i, j].AIPressDelay < 0)
                        {
                            users[i, j].wantedPos[0] = ball.coords[0];
                            users[i, j].wantedPos[1] = ball.coords[1];
                            int _reaction = 10;
                            if (roomType == RoomType.Challenge) _reaction = 5;
                            users[i, j].AIPressDelay = _reaction;
                        }
                        continue;
                    }

                    if (goalkickTID > -1 || keepers[0].ballInPossesDelay > 0 || keepers[1].ballInPossesDelay > 0)
                    {
                        users[i, j].wantedPos = PositionHandler.GetGoalkickPos(users[i, j].AIDynamicpos.position, users[i, j].AIDynamicpos.side, i, homeSide);
                        continue;
                    }

                    if (throwInTID > -1)
                    {
                        users[i, j].wantedPos = PositionHandler.GetThrowinPos(users[i, j].AIDynamicpos.position, users[i, j].AIDynamicpos.side, i, homeSide, throwInCoords[0], throwInCoords[1]);
                        continue;
                    }

                    if (cornerTID > -1 || cornerInProgress)
                    {
                        bool isAttackingTeam = false;
                        if (i == cornerTID) isAttackingTeam = true;

                        users[i, j].wantedPos = PositionHandler.GetCornerPos(cornerCoords[1], ref cornerPlrsSetted, isAttackingTeam);
                        continue;
                    }


                    //check, if AI is controlling ball and near enough ball (is dribbling)
                    if (currentControllerTID == i && currentControllerPID == j)
                    {
                        if (users[i, j].distToBall < 0.1)
                        {
                            int oppTID;
                            if (i == 0) oppTID = 1; else oppTID = 0;
                            double angleToOpponent;
                            double goalY = 0;
                            double wantedDir, f, a;
                            bool turningDirectionDecided = false;
                            double turningAmount = 0;

                            //check direction of goal                            
                            if (i == 0 && homeSide == 1) goalY = Field.GOALLINE_N;
                            if (i == 1 && homeSide == 1) goalY = Field.GOALLINE_P;
                            if (i == 0 && homeSide == 2) goalY = Field.GOALLINE_P;
                            if (i == 1 && homeSide == 2) goalY = Field.GOALLINE_N;

                            //default angle->goal
                            wantedDir = F.Angle(users[i, j].coords[0], users[i, j].coords[1], 0, goalY);

                            //lets check, if opponent is ahead
                            for (int k = 0; k < maxPlayers; k++)
                            {
                                //skip if opponent far away
                                if (F.Distance(users[i, j].coords[0], users[i, j].coords[1], users[oppTID, k].coords[0], users[oppTID, k].coords[1]) > 1.0) continue;
                                angleToOpponent = F.Angle(users[i, j].coords[0], users[i, j].coords[1], users[oppTID, k].coords[0], users[oppTID, k].coords[1]);

                                //check, if opponent is ahead
                                f = 180.0 - wantedDir;
                                a = angleToOpponent + f;
                                if (a >= 360) a -= 360;
                                if (a < 0) a += 360;
                                if (a < 135 || a > 225) continue;

                                //turn away from opponent
                                if (!turningDirectionDecided)
                                {
                                    if (a > 180) turningAmount = -90; else turningAmount = 90;
                                    turningDirectionDecided = true;
                                }

                                wantedDir = angleToOpponent + turningAmount;
                                if (wantedDir >= 360) wantedDir -= 360;
                                if (wantedDir < 0) wantedDir += 360;
                            }

                            double r;
                            r = Math.Cos(F.ToRad * wantedDir) * 1.0;
                            users[i, j].wantedPos[0] = users[i, j].coords[0] + r;
                            r = Math.Sin(F.ToRad * wantedDir) * 1.0;
                            users[i, j].wantedPos[1] = users[i, j].coords[1] + r;

                            //users[i, j].wantedPos = GetOpponentsNetPos(i, homeSide);
                            continue;
                        }
                    }

                    double offsideLine = 0;
                    if (currentControllerTID == i && lastControllersPID[i] != j)
                        offsideLine = CalculateOffsideLine(i, j);
                    users[i, j].wantedPos = PositionHandler.GetWantedPos(users[i, j].AIDynamicpos.position, users[i, j].AIDynamicpos.side, i, homeSide, ball.coords[0], ball.coords[1], currentControllerTID, offsideLine);
                }
            }
        }

        double CalculateOffsideLine(int tID, int pID)
        {
            byte upDown = 0;
            byte oppTID;

            //if pID is -1, keeper have kicked ball!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

            double offsideLine;
            if (tID == 0) oppTID = 1; else oppTID = 0;

            if (tID == 0 && homeSide == 1) upDown = 2;
            if (tID == 0 && homeSide == 2) upDown = 1;
            if (tID == 1 && homeSide == 1) upDown = 1;
            if (tID == 1 && homeSide == 2) upDown = 2;

            if (upDown == 1) offsideLine = 9999; else offsideLine = -9999;

            //lets check, that player wont go upper than ball
            if (upDown == 1)
            {
                if (users[tID, pID].coords[1] > ball.coords[1]) offsideLine = ball.coords[1];
                if (offsideLine < 0) offsideLine = 0;
            }
            else
            {
                if (users[tID, pID].coords[1] < ball.coords[1]) offsideLine = ball.coords[1];
                if (offsideLine > 0) offsideLine = 0;
            }

            //offsideline by lowest opposition
            for (int i = 0; i < maxPlayers; i++)
            {
                if (!IsPlayerOnline(oppTID, i) && !botsEnabled) continue;
                if (upDown == 1)
                {
                    if (users[oppTID, i].coords[1] > offsideLine)
                        offsideLine = users[oppTID, i].coords[1];
                }
                else
                {
                    if (users[oppTID, i].coords[1] < offsideLine)
                        offsideLine = users[oppTID, i].coords[1];
                }
            }

            return offsideLine;
        }

        void ResetPressing()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j].AIDoPress = false;
        }

        void ResetAutopassReceivers()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j].gotoAutopass = false;
        }

        double[] GetOpponentsNetPos(int tID)
        {
            double[] res = new double[2];

            if (tID == 0 && homeSide == 1) res[1] = Field.GOALLINE_N;
            if (tID == 1 && homeSide == 1) res[1] = Field.GOALLINE_P;
            if (tID == 0 && homeSide == 2) res[1] = Field.GOALLINE_P;
            if (tID == 1 && homeSide == 2) res[1] = Field.GOALLINE_N;

            return res;
        }

        double[] GetOwnTeamNetPos(int tID)
        {
            double[] res = new double[2];

            if (tID == 0 && homeSide == 1) res[1] = Field.GOALLINE_P;
            if (tID == 1 && homeSide == 1) res[1] = Field.GOALLINE_N;
            if (tID == 0 && homeSide == 2) res[1] = Field.GOALLINE_N;
            if (tID == 1 && homeSide == 2) res[1] = Field.GOALLINE_P;

            return res;
        }

        void MovePlayers()
        {
            if (autoMoving > 0) return;

            double maxSpeed;
            double acceleration;
            double turning = 8;
            double f, a, d;
            double _angle;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (!IsPlayerOnline(i, j)) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;

                    users[i, j]._oldCoords[0] = users[i, j].coords[0];
                    users[i, j]._oldCoords[1] = users[i, j].coords[1];

                    if (users[i, j].automoveFromFreekickArea)
                    {
                        //goalkicks, corners and throwins, player can move to 0,0
                        if (freekickTID == -1)
                            users[i, j].angle = F.Angle(users[i, j].coords[0], users[i, j].coords[1], 0, 0);
                        //with freekicks, players moves toward own goal
                        else
                        {
                            double[] _coords = GetOwnTeamNetPos(i);
                            users[i, j].angle = F.Angle(users[i, j].coords[0], users[i, j].coords[1], _coords[0], _coords[1]);
                        }

                        acceleration = users[i, j].maxAcceleration;
                    }
                    else
                        acceleration = users[i, j].GetAcceleration();

                    if (!users[i, j].automoveFromFreekickArea)
                    {
                        if (users[i, j].controlMethod != ControlMethod.MouseKeyboard)
                        {
                            if (users[i, j].buttons[0]) continue;
                            if (users[i, j].controlMethod == ControlMethod.Mouse)
                                if (users[i, j].buttons[1]) continue;
                            if (users[i, j].buttons[2]) continue;
                        }
                    }

                    if (users[i, j].controlMethod == ControlMethod.MouseKeyboard)
                    {
                        if (users[i, j].automoveFromFreekickArea)
                            _angle = users[i, j].angle;
                        else
                            _angle = users[i, j].GetKeyboardAngle();
                    }
                    else
                        _angle = users[i, j].angle;


                    //************* SPEED CALCULATIONS ************************

                    maxSpeed = users[i, j].GetMaxSpeed();

                    if (acceleration == 0) users[i, j].speed -= 0.00058;

                    //************* TURNING CALCULATIONS **********************

                    if (acceleration > 0)
                    {
                        f = 180 - users[i, j].direction;
                        a = _angle + f;

                        if (a >= 360) a -= 360;
                        if (a < 0) a += 360;

                        if (a < 67 || a > 293)
                        { //180 turning
                            if (users[i, j].speed > 0)
                            {
                                users[i, j].speed -= 0.00174;
                                acceleration = 0;
                            }
                            else
                            {
                                users[i, j].direction = _angle;// users[i, j].angle;
                            }
                        }
                        else
                        {
                            if (a >= 180 - turning && a <= 180 + turning)
                                users[i, j].direction = _angle;
                            else if (a < 180 - turning)
                                users[i, j].direction -= turning;
                            else
                                users[i, j].direction += turning;

                            if (users[i, j].direction > 360) users[i, j].direction -= 360;
                            if (users[i, j].direction < 0) users[i, j].direction += 360;
                        }

                        users[i, j].speed += acceleration;
                    }

                    //slow moving
                    if (users[i, j].slowMoving && !users[i, j].automoveFromFreekickArea)
                    {
                        d = users[i, j].controlDistance;

                        if (users[i, j].controlMethod == ControlMethod.Mouse)
                        {
                            if (d < 10 && users[i, j].speed > 0.004) users[i, j].speed = 0.004;
                            if (d < 18 && d >= 10 && users[i, j].speed > 0.006) users[i, j].speed = 0.006;
                            if (d < 26 && d >= 18 && users[i, j].speed > 0.008) users[i, j].speed = 0.008;
                            if (d < 34 && d >= 26 && users[i, j].speed > 0.010) users[i, j].speed = 0.010;
                            if (d < 42 && d >= 34 && users[i, j].speed > 0.012) users[i, j].speed = 0.012;
                            if (d < 50 && d >= 42 && users[i, j].speed > 0.014) users[i, j].speed = 0.014;
                            if (d < 58 && d >= 50 && users[i, j].speed > 0.016) users[i, j].speed = 0.016;
                        }
                        else if (users[i, j].controlMethod == ControlMethod.Joystick || users[i, j].controlMethod == ControlMethod.Mobile)
                        {
                            if (d < 10 && users[i, j].speed > 0.004) users[i, j].speed = 0.004;
                            if (d < 15 && d >= 10 && users[i, j].speed > 0.006) users[i, j].speed = 0.006;
                            if (d < 20 && d >= 15 && users[i, j].speed > 0.008) users[i, j].speed = 0.008;
                            if (d < 25 && d >= 20 && users[i, j].speed > 0.010) users[i, j].speed = 0.010;
                            if (d < 30 && d >= 25 && users[i, j].speed > 0.012) users[i, j].speed = 0.012;
                            if (d < 35 && d >= 30 && users[i, j].speed > 0.014) users[i, j].speed = 0.014;
                            if (d < 40 && d >= 35 && users[i, j].speed > 0.016) users[i, j].speed = 0.016;
                            if (d < 45 && d >= 40 && users[i, j].speed > 0.018) users[i, j].speed = 0.018;
                        }
                    }


                    if (users[i, j].speed > maxSpeed) users[i, j].speed = maxSpeed;
                    if (users[i, j].speed < 0) users[i, j].speed = 0;

                }


            //*********************************************************
            //*********************************************************
            //*********************************************************

            //move player
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (!IsPlayerOnline(i, j)) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;

                    if (users[i, j].automoveFromFreekickArea)
                        users[i, j].automoveFromFreekickArea = CheckIfStillInFreekickArea(i, j);

                    //freekick is going on, so lets block player from area, if he tries to get in
                    BlockEnterToFreekickArea(i, j);
                    users[i, j].Move();
                }

            //blockarea, that player cant run off from field
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (!IsPlayerOnline(i, j)) continue;
                    if (users[i, j].coords[0] > Field.THROWIN_P + 2.0) users[i, j].coords[0] = Field.THROWIN_P + 2.0;
                    if (users[i, j].coords[0] < Field.THROWIN_N - 2.0) users[i, j].coords[0] = Field.THROWIN_N - 2.0;
                    if (users[i, j].coords[1] > Field.GOALLINE_P + 2.0) users[i, j].coords[1] = Field.GOALLINE_P + 2.0;
                    if (users[i, j].coords[1] < Field.GOALLINE_N - 2.0) users[i, j].coords[1] = Field.GOALLINE_N - 2.0;
                }

        }

        void AIMovePlayers()
        {
            if (autoMoving > 0) return;
            if (!botsEnabled) return;

            double maxSpeed;
            double acceleration;
            double _angle;
            double f, a;
            double turning = 7.0;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (IsPlayerOnline(i, j)) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;

                    users[i, j]._oldCoords[0] = users[i, j].coords[0];
                    users[i, j]._oldCoords[1] = users[i, j].coords[1];

                    if (users[i, j].automoveFromFreekickArea)
                    {
                        //goalkicks, corners and throwins, player can move to 0,0
                        if (freekickTID == -1)
                            _angle = F.Angle(users[i, j].coords[0], users[i, j].coords[1], 0, 0);
                        //with freekicks, players moves toward own goal
                        else
                        {
                            double[] _coords = GetOwnTeamNetPos(i);
                            _angle = F.Angle(users[i, j].coords[0], users[i, j].coords[1], _coords[0], _coords[1]);
                        }
                    }
                    else
                        _angle = F.Angle(users[i, j].coords[0], users[i, j].coords[1], users[i, j].wantedPos[0], users[i, j].wantedPos[1]);

                    maxSpeed = users[i, j].GetAIMaxSpeed(roomType);
                    acceleration = users[i, j].maxAcceleration;

                    //************* TURNING CALCULATIONS **********************

                    //turning start
                    f = 180.0 - users[i, j].direction;
                    a = _angle + f;

                    if (a >= 360) a -= 360.0;
                    if (a < 0) a += 360.0;

                    //*******
                    if (a < 67 || a > 293)
                    { //180 turning
                        if (users[i, j].speed > 0)
                        {
                            users[i, j].speed -= 0.00174;
                            acceleration = 0;
                        }
                        else
                        {
                            users[i, j].direction = _angle;
                        }
                    }
                    else
                    {
                        if (a >= 180.0 - turning && a <= 180.0 + turning)
                            users[i, j].direction = _angle;
                        else if (a < 180 - turning)
                            users[i, j].direction -= turning;
                        else
                            users[i, j].direction += turning;

                        if (users[i, j].direction > 360) users[i, j].direction -= 360.0;
                        if (users[i, j].direction < 0) users[i, j].direction += 360.0;
                    }
                    //turning end

                    users[i, j].speed += acceleration;

                    if (users[i, j].speed > maxSpeed) users[i, j].speed = maxSpeed;
                    if (users[i, j].speed < 0) users[i, j].speed = 0;

                }

            //move player
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (IsPlayerOnline(i, j)) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;

                    if (users[i, j].automoveFromFreekickArea)
                        users[i, j].automoveFromFreekickArea = CheckIfStillInFreekickArea(i, j);

                    //freekick is going on, so lets block player from area, if he tries to get in
                    BlockEnterToFreekickArea(i, j);
                    users[i, j].Move();
                }

        }

        void PlayerCollision()
        {
            if (autoMoving > 0 || throwInTID > -1 || freekickTID > -1) return;

            double area = 0.13;  //0.19 on liian iso.AI ei pysty riistämään 

            #region player collisions (between own team mates)
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (users[i, j].slideDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].automoveFromFreekickArea) continue;
                    if (!botsEnabled)
                        if (!IsPlayerOnline(i, j)) continue;

                    for (int k = 0; k < maxPlayers; k++)
                    {
                        if (users[i, k].slideDelay > 0) continue;
                        if (users[i, k].fallDelay > 0) continue;
                        if (users[i, k].automoveFromFreekickArea) continue;
                        if (!botsEnabled)
                            if (!IsPlayerOnline(i, k)) continue;
                        if (j == k) continue;

                        double d = F.Distance(users[i, j].coords[0], users[i, j].coords[1], users[i, k].coords[0], users[i, k].coords[1]);

                        //collision occurs
                        if (d < area)
                        {
                            double _amountToMove = area - d;  //(0.13-0.11)  _amountToMove=0.02
                            double[] _bodyPowers = GetBodyDifferences(users[i, j].body, users[i, k].body);

                            double a = F.Angle(users[i, k]._oldCoords[0], users[i, k]._oldCoords[1], users[i, j]._oldCoords[0], users[i, j]._oldCoords[1]);
                            double r = Math.Cos(F.ToRad * a) * _amountToMove * _bodyPowers[0];
                            users[i, j].coords[0] = users[i, j].coords[0] + r;
                            r = Math.Sin(F.ToRad * a) * _amountToMove * _bodyPowers[0];
                            users[i, j].coords[1] = users[i, j].coords[1] + r;

                            a = F.Angle(users[i, j]._oldCoords[0], users[i, j]._oldCoords[1], users[i, k]._oldCoords[0], users[i, k]._oldCoords[1]);
                            r = Math.Cos(F.ToRad * a) * _amountToMove * _bodyPowers[1];
                            users[i, k].coords[0] = users[i, k].coords[0] + r;
                            r = Math.Sin(F.ToRad * a) * _amountToMove * _bodyPowers[1];
                            users[i, k].coords[1] = users[i, k].coords[1] + r;

                        }

                    }
                }
            #endregion)

            int tID;

            #region player collisions (between enemy players)
            for (int i = 0; i < 2; i++)
            {
                if (i == 0) tID = 1; else tID = 0;

                for (int j = 0; j < maxPlayers; j++)
                {
                    if (users[i, j].slideDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].automoveFromFreekickArea) continue;
                    if (!botsEnabled)
                        if (!IsPlayerOnline(i, j)) continue;

                    for (int k = 0; k < maxPlayers; k++)
                    {
                        if (users[tID, k].slideDelay > 0) continue;
                        if (users[tID, k].fallDelay > 0) continue;
                        if (users[tID, k].automoveFromFreekickArea) continue;
                        if (!botsEnabled)
                            if (!IsPlayerOnline(tID, k)) continue;

                        double d = F.Distance(users[i, j].coords[0], users[i, j].coords[1], users[tID, k].coords[0], users[tID, k].coords[1]);

                        //collision occurs
                        if (d < area)
                        {

                            double _amountToMove = area - d;  //(0.13-0.11)  _amountToMove=0.02
                            double[] _bodyPowers = GetBodyDifferences(users[i, j].body, users[tID, k].body);

                            double a = F.Angle(users[tID, k]._oldCoords[0], users[tID, k]._oldCoords[1], users[i, j]._oldCoords[0], users[i, j]._oldCoords[1]);
                            double r = Math.Cos(F.ToRad * a) * _amountToMove * _bodyPowers[0];
                            users[i, j].coords[0] = users[i, j].coords[0] + r;
                            r = Math.Sin(F.ToRad * a) * _amountToMove * _bodyPowers[0];
                            users[i, j].coords[1] = users[i, j].coords[1] + r;

                            a = F.Angle(users[i, j]._oldCoords[0], users[i, j]._oldCoords[1], users[tID, k]._oldCoords[0], users[tID, k]._oldCoords[1]);
                            r = Math.Cos(F.ToRad * a) * _amountToMove * _bodyPowers[1];
                            users[tID, k].coords[0] = users[tID, k].coords[0] + r;
                            r = Math.Sin(F.ToRad * a) * _amountToMove * _bodyPowers[1];
                            users[tID, k].coords[1] = users[tID, k].coords[1] + r;
                        }

                    }
                }
            }
            #endregion


        }

        double[] GetBodyDifferences(byte body0, byte body1)
        {
            double[] res = new double[2];

            if (body0 == body1)
            {
                res[0] = 0.5;
                res[1] = 0.5;
            }

            if ((body0 == 2 && body1 == 1) || (body0 == 1 && body1 == 0))
            {
                res[0] = 0.35;
                res[1] = 0.65;
            }

            if ((body0 == 1 && body1 == 2) || (body0 == 0 && body1 == 1))
            {
                res[0] = 0.65;
                res[1] = 0.35;
            }

            if (body0 == 2 && body1 == 0)
            {
                res[0] = 0.20;
                res[1] = 0.80;
            }

            if (body0 == 0 && body1 == 2)
            {
                res[0] = 0.80;
                res[1] = 0.20;
            }

            return res;
        }

        bool CheckIfStillInFreekickArea(int tID, int pID)
        {
            //corner
            if (cornerTID > -1)
                if (F.Distance(users[tID, pID].coords[0], users[tID, pID].coords[1], cornerCoords[0], cornerCoords[1]) >= 1.25)
                    return false;

            //freekick
            if (freekickTID > -1)
                if (F.Distance(users[tID, pID].coords[0], users[tID, pID].coords[1], freekickCoords[0], freekickCoords[1]) >= 1.25)
                    return false;

            //goalkick
            if (goalkickTID > -1)
            {
                if ((homeSide == 1 && goalkickTID == 0) || (homeSide == 2 && goalkickTID == 1))
                    if (users[tID, pID].coords[1] <= Field.KEEPER16_AREA_YP) return false;
                if ((homeSide == 1 && goalkickTID == 1) || (homeSide == 2 && goalkickTID == 0))
                    if (users[tID, pID].coords[1] >= Field.KEEPER16_AREA_YN) return false;
            }

            //keeper ball in possession
            if (keepers[0].ballInPossesDelay > 0)
                if (users[tID, pID].coords[1] <= Field.KEEPER16_AREA_YP) return false;
            if (keepers[1].ballInPossesDelay > 0)
                if (users[tID, pID].coords[1] >= Field.KEEPER16_AREA_YN) return false;

            if (keepers[0].divePossesDelay > 0)
                if (users[tID, pID].coords[1] <= Field.KEEPER16_AREA_YP) return false;
            if (keepers[1].divePossesDelay > 0)
                if (users[tID, pID].coords[1] >= Field.KEEPER16_AREA_YN) return false;

            return true;
        }

        void BlockEnterToFreekickArea(int tID, int pID)
        {
            if (users[tID, pID].automoveFromFreekickArea) return;

            double a, r;

            //corner
            if (cornerTID > -1)
            {
                if (cornerTID == tID) return;
                if (F.Distance(users[tID, pID].coords[0], users[tID, pID].coords[1], cornerCoords[0], cornerCoords[1]) < 1.25)
                {
                    a = F.Angle(cornerCoords[0], cornerCoords[1], users[tID, pID].coords[0], users[tID, pID].coords[1]);
                    for (int i = 0; i < 2; i++)
                        users[tID, pID].coords[i] = cornerCoords[i];

                    r = Math.Cos(F.ToRad * a) * 1.25;
                    users[tID, pID].coords[0] = users[tID, pID].coords[0] + r;
                    r = Math.Sin(F.ToRad * a) * 1.25;
                    users[tID, pID].coords[1] = users[tID, pID].coords[1] + r;
                }
            }

            //goalkick
            if (goalkickTID > -1)
            {
                if ((homeSide == 1 && goalkickTID == 0) || (homeSide == 2 && goalkickTID == 1))
                    if (users[tID, pID].coords[1] > Field.KEEPER16_AREA_YP)
                        users[tID, pID].coords[1] = Field.KEEPER16_AREA_YP;

                if ((homeSide == 1 && goalkickTID == 1) || (homeSide == 2 && goalkickTID == 0))
                    if (users[tID, pID].coords[1] < Field.KEEPER16_AREA_YN)
                        users[tID, pID].coords[1] = Field.KEEPER16_AREA_YN;
            }

            //keeper ball in possession
            if (keepers[0].ballInPossesDelay > 0)
                if (users[tID, pID].coords[1] > Field.KEEPER16_AREA_YP)
                    users[tID, pID].coords[1] = Field.KEEPER16_AREA_YP;
            if (keepers[1].ballInPossesDelay > 0)
                if (users[tID, pID].coords[1] < Field.KEEPER16_AREA_YN)
                    users[tID, pID].coords[1] = Field.KEEPER16_AREA_YN;
            if (keepers[0].divePossesDelay > 0)
                if (users[tID, pID].coords[1] > Field.KEEPER16_AREA_YP)
                    users[tID, pID].coords[1] = Field.KEEPER16_AREA_YP;
            if (keepers[1].divePossesDelay > 0)
                if (users[tID, pID].coords[1] < Field.KEEPER16_AREA_YN)
                    users[tID, pID].coords[1] = Field.KEEPER16_AREA_YN;

            //freekick
            if (freekickTID > -1)
            {
                if (freekickTID == tID) return;

                if (F.Distance(users[tID, pID].coords[0], users[tID, pID].coords[1], freekickCoords[0], freekickCoords[1]) < 1.25)
                {
                    a = F.Angle(freekickCoords[0], freekickCoords[1], users[tID, pID].coords[0], users[tID, pID].coords[1]);
                    for (int i = 0; i < 2; i++)
                        users[tID, pID].coords[i] = freekickCoords[i];

                    r = Math.Cos(F.ToRad * a) * 1.25;
                    users[tID, pID].coords[0] = users[tID, pID].coords[0] + r;
                    r = Math.Sin(F.ToRad * a) * 1.25;
                    users[tID, pID].coords[1] = users[tID, pID].coords[1] + r;
                }
            }

        }

        void BallControl()
        {
            currentControllerTID = -1;
            currentControllerPID = -1;
            if (ball.height > 0.15) return;
            if (autoMoving > 0) return;
            if (IsThrowInCornerFreekick()) return;
            if (keepers[0].ballInPossesDelay > 0) return;
            if (keepers[1].ballInPossesDelay > 0) return;

            int pID = -1;
            int tID = -1;
            int oppTID;
            double biggest = 100;
            int[] numberOfControllers = new int[2];

            //lets count, which player is nearest to the ball
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (!botsEnabled)
                        if (!IsPlayerOnline(i, j)) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].touchDelay > 0) continue;
                    if (disabledTID == i && disabledPID == j) continue;

                    //distance needs to be 0.15 or less, so player can control ball (old value was 0.1)
                    if (users[i, j].distToBall < 0.1f)
                    {
                        numberOfControllers[i]++;
                        if (users[i, j].distToBall < biggest)
                        {
                            biggest = users[i, j].distToBall;
                            tID = i;
                            pID = j;
                        }
                    }

                }

            //*********************
            //nobody controls ball
            if (pID == -1)
            {
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                        users[i, j].AIBallInPossesTime = 0;
                return;
            }

            //*********************

            //only 1 team is controlling
            if ((numberOfControllers[0] >= 1 && numberOfControllers[1] == 0) || (numberOfControllers[1] >= 1 && numberOfControllers[0] == 0))
            {
                for (int i = 0; i < 2; i++)
                    ball.coords[i] = users[tID, pID].coords[i];
                ball.speed = users[tID, pID].speed;
                ball.angle = users[tID, pID].direction;

                //decrease controllers running speed little bit
                if (users[tID, pID].speed > users[tID, pID].GetMaxSpeed() - 0.0058)
                    users[tID, pID].speed = users[tID, pID].GetMaxSpeed() - 0.0058;

                ball.Move(0.0377);

                keeperDistanceShot = -1;

                //opponents cant be in offside anymore, if player controls ball
                //if (tID == 0) oppTID = 1; else oppTID = 0;
                //for (int j = 0; j < maxPlayers; j++)
                //    users[oppTID, j].offside = false;
                ResetOffside();

                users[tID, pID].AIBallInPossesTime++;

                currentControllerTID = tID;
                currentControllerPID = pID;
                cornerInProgress = false;
                UpdateController(tID, pID);
                disabledTID = -1;
                disabledPID = -1;
                ResetAutopassReceivers();
            }
            //more than 1 controller
            {
                for (int i = 0; i < 2; i++)
                {
                    if (i == tID) continue; //intercept only from opponents
                    for (int j = 0; j < maxPlayers; j++)
                    {


                        if (!botsEnabled)
                            if (!IsPlayerOnline(i, j)) continue;
                        if (users[i, j].slideDelay > 0) continue;
                        if (users[i, j].fallDelay > 0) continue;
                        if (users[i, j].touchDelay > 0) continue;
                        if (disabledTID == i && disabledPID == j) continue;

                        //interception occurs
                        if (users[i, j].distToBall < 0.10f)
                        {
                            users[tID, pID].touchDelay = 25;
                            //users[i, j].touchDelay = 25;

                            ball.angle = users[i, j].direction;
                            ball.speed = 0.0348;
                            ball.zSpeed = 0;

                            cornerInProgress = false;
                            UpdateController(i, j);
                            disabledTID = -1;
                            disabledPID = -1;
                            ResetAutopassReceivers();

                            ResetOffside();

                            return;
                        }
                    }
                }
            }
        }

        void Lines()
        {
            if (autoMoving > 0) return;
            if (ballInNetDelayer > 0) return;
            if (IsThrowInCornerFreekick()) return;

            if (CheckIfThrowIn()) return;
            if (CheckIfCorner()) return;
            if (CheckIfGoalkick()) return;
        }

        bool CheckIfThrowIn()
        {
            bool res = false;

            if (ball.coords[0] < (Field.THROWIN_N - Field.BALLRADIUS))
            {
                throwInCoords[0] = Field.THROWIN_N;
                throwInCoords[1] = ball.coords[1];
                res = true;
            }

            if (ball.coords[0] > (Field.THROWIN_P + Field.BALLRADIUS))
            {
                throwInCoords[0] = Field.THROWIN_P;
                throwInCoords[1] = ball.coords[1];
                res = true;
            }

            if (res)
            {
                //lets deny, that throwin position wont be out side of field
                if (throwInCoords[1] > Field.GOALLINE_P - 0.5)
                    throwInCoords[1] = Field.GOALLINE_P - 0.5;
                if (throwInCoords[1] < Field.GOALLINE_N + 0.5)
                    throwInCoords[1] = Field.GOALLINE_N + 0.5;

                if (lastControllersTID[0] == 0)
                    throwInTID = 1;
                else
                    throwInTID = 0;

                teams[throwInTID].throwIns++;

                fouledTID = -1;
                cornerInProgress = false;
                ResetOffside();
                timerEnabled = false;
                BroadcastTimer();
                keeperDistanceShot = -1;
                cancelFreekickDelayer = 500;

                if (keepers[0].divingDelay == 0) keepers[0].speed = 0;
                if (keepers[1].divingDelay == 0) keepers[1].speed = 0;
                ResetAutopassReceivers();
                ResetPressing();
            }

            return res;
        }

        bool CheckIfCorner()
        {
            bool res = false;

            //**************

            //upper field
            if (ball.coords[1] > (Field.GOALLINE_P + Field.BALLRADIUS))
            {
                if (keepers[1].divingDelay == 0) keepers[1].speed = 0;

                if (homeSide == 1 && lastControllersTID[0] == 0) //corner for away
                {
                    cornerTID = 1;
                    cornerCoords[1] = Field.GOALLINE_P - 0.07;
                    res = true;
                }

                if (homeSide == 2 && lastControllersTID[0] == 1) //corner for home
                {
                    cornerTID = 0;
                    cornerCoords[1] = Field.GOALLINE_P - 0.07;
                    res = true;
                }
            }

            //lower field
            if (ball.coords[1] < (Field.GOALLINE_N - Field.BALLRADIUS))
            {
                if (keepers[0].divingDelay == 0) keepers[0].speed = 0;

                if (homeSide == 1 && lastControllersTID[0] == 1)//corner for home
                {
                    cornerTID = 0;
                    cornerCoords[1] = Field.GOALLINE_N + 0.07;
                    res = true;
                }

                if (homeSide == 2 && lastControllersTID[0] == 0) //corner for away
                {
                    cornerTID = 1;
                    cornerCoords[1] = Field.GOALLINE_N + 0.07;
                    res = true;
                }
            }

            //**************

            if (res)
            {
                if (ball.coords[0] > 0)
                    cornerCoords[0] = Field.THROWIN_P - 0.07;
                else
                    cornerCoords[0] = Field.THROWIN_N + 0.07;

                fouledTID = -1;
                ResetOffside();
                timerEnabled = false;
                cornerInProgress = true;

                SetAutomoveFromFreekickArea(2);
                keeperDistanceShot = -1;
                cancelFreekickDelayer = 500;
                teams[cornerTID].corners++;
                if (keepers[0].divingDelay == 0) keepers[0].speed = 0;
                if (keepers[1].divingDelay == 0) keepers[1].speed = 0;
                ResetAutopassReceivers();
                ResetPressing();
            }

            return res;
        }

        bool CheckIfGoalkick()
        {
            bool res = false;

            //**************

            //upper field
            if (ball.coords[1] > (Field.GOALLINE_P + Field.BALLRADIUS))
            {
                if (homeSide == 1 && lastControllersTID[0] == 1) //goalkick for home
                {
                    goalkickTID = 0;
                    goalkickCoords[1] = Field.GOALKICK_YP;
                    res = true;
                }

                if (homeSide == 2 && lastControllersTID[0] == 0) //goalkick for away
                {
                    goalkickTID = 1;
                    goalkickCoords[1] = Field.GOALKICK_YP;
                    res = true;
                }
            }

            //lower field
            if (ball.coords[1] < (Field.GOALLINE_N - Field.BALLRADIUS))
            {
                if (homeSide == 1 && lastControllersTID[0] == 0) //goalkick for away
                {
                    goalkickTID = 1;
                    goalkickCoords[1] = Field.GOALKICK_YN;
                    res = true;
                }

                if (homeSide == 2 && lastControllersTID[0] == 1) //goalkick for home
                {
                    goalkickTID = 0;
                    goalkickCoords[1] = Field.GOALKICK_YN;
                    res = true;
                }
            }

            //**************

            if (res)
            {
                if (ball.coords[0] > 0)
                    goalkickCoords[0] = Field.GOALKICK_XP;
                else
                    goalkickCoords[0] = Field.GOALKICK_XN;

                if (ball.coords[0] < Field.GOALKICK_XP && ball.coords[0] > Field.GOALKICK_XN) AddShotStat(false);

                fouledTID = -1;
                ResetOffside();
                cornerInProgress = false;
                teams[goalkickTID].goalKicks++;
                SetAutomoveFromFreekickArea(3);
                keeperDistanceShot = -1;

                //if (ball.coords[0] < 110 && ball.coords[0] > -110)
                //Proc.addShotToGoal(sID, false);
                if (keepers[0].divingDelay == 0) keepers[0].speed = 0;
                if (keepers[1].divingDelay == 0) keepers[1].speed = 0;
                ResetAutopassReceivers();
                ResetPressing();
            }


            return res;
        }

        public bool IsOpponetsShot()
        {
            //upper field
            if (ball.coords[1] > 0)
            {
                if (homeSide == 1 && lastControllersTID[0] == 1)
                    return true;

                if (homeSide == 2 && lastControllersTID[0] == 0)
                    return true;
            }

            //lower field
            if (ball.coords[1] < 0)
            {
                if (homeSide == 1 && lastControllersTID[0] == 0)
                    return true;

                if (homeSide == 2 && lastControllersTID[0] == 1)
                    return true;
            }

            return false;
        }

        void SetAutomoveFromFreekickArea(int action)
        {
            //corner
            if (action == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (cornerTID == i) continue;//we need to allow corner taking players to enter area
                    for (int j = 0; j < maxPlayers; j++)
                        if (F.Distance(users[i, j].coords[0], users[i, j].coords[1], cornerCoords[0], cornerCoords[1]) < 1.25)
                            users[i, j].automoveFromFreekickArea = true;
                }
            }

            //goalkick or keeper holds ball or penalty
            if (action == 3)
            {
                if ((homeSide == 1 && goalkickTID == 0) || (homeSide == 2 && goalkickTID == 1) || keepers[0].divePossesDelay > 0 || keepers[0].ballInPossesDelay > 0)
                {
                    for (int i = 0; i < 2; i++)
                        for (int j = 0; j < maxPlayers; j++)
                            if (users[i, j].coords[1] > Field.KEEPER16_AREA_YP)
                                users[i, j].automoveFromFreekickArea = true;

                }

                if ((homeSide == 1 && goalkickTID == 1) || (homeSide == 2 && goalkickTID == 0) || keepers[1].divePossesDelay > 0 || keepers[1].ballInPossesDelay > 0)
                {
                    for (int i = 0; i < 2; i++)
                        for (int j = 0; j < maxPlayers; j++)
                            if (users[i, j].coords[1] < Field.KEEPER16_AREA_YN)
                                users[i, j].automoveFromFreekickArea = true;

                }
            }

            //freekick
            if (action == 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (freekickTID == i) continue;   //we need to allow freekick taking players to enter area
                    for (int j = 0; j < maxPlayers; j++)
                        if (F.Distance(users[i, j].coords[0], users[i, j].coords[1], freekickCoords[0], freekickCoords[1]) < 1.25)
                            users[i, j].automoveFromFreekickArea = true;
                }
            }

            sbyte teamAllowedToEnter = -1;

            if (action == 2) teamAllowedToEnter = (sbyte)cornerTID;
            if (action == 4) teamAllowedToEnter = (sbyte)freekickTID;

            BroadcastBlockedArea((byte)action, teamAllowedToEnter);
        }

        bool BallKickingA()
        {
            if (autoMoving > 0) return false;
            if (throwInTID > -1) return false;
            if (goalkickTID > -1) return false;
            if (keepers[0].ballInPossesDelay > 0) return false;
            if (keepers[1].ballInPossesDelay > 0) return false;
            //if (ball.height > Proc.getVolleyHeight(2, true)) return false;  //even biggest body cant reach ball

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (freekickTID > -1 && freekickTID != i) continue;
                    if (cornerTID > -1 && cornerTID != i) continue;
                    if (throwInTID > -1 && throwInTID != i) continue;
                    if (freekickTID > -1 && freekickTakerPID != j) continue;
                    if (cornerTID > -1 && cornerTakerPID != j) continue;
                    if (!IsPlayerOnline(i, j)) continue;
                    if (users[i, j].buttons[0]) continue;
                    if (users[i, j].buttonPowers[0] == 0) continue;

                    if (users[i, j].touchDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (ball.height > users[i, j].GetVolleyHeight()) continue;

                    if (disabledTID == i && disabledPID == j) continue;
                    //if (ball.height > Proc.getVolleyHeight(users[i, j].body, users[i, j].vip)) continue;
                    //if (Proc.isThrowInCornerFreekick(sID))
                    //    if (!Proc.isFreekickTaker(sID, i, j)) continue;

                    if (users[i, j].distToBall < GetKickingArea())
                    {
                        bool extraPowerForCorner = false;

                        if (cornerTID == i)
                        {
                            users[i, j].angle = ForceCornerToField(users[i, j].angle);
                            users[i, j].direction = users[i, j].angle;
                            extraPowerForCorner = true;
                        }

                        if (ball.speed > 0) cornerInProgress = false;  //if ball contains speed, we can be sure, that this isnt corner any more

                        double shootingDirection = 0;
                        bool keybHelper = false;

                        //for keyboard, if kick is very low power (user trying to run with ball faster) lets help by setting shoot direction to, where keyboard is aimed
                        if (users[i, j].controlMethod == ControlMethod.MouseKeyboard && users[i, j].buttonPowers[0] < 32)
                        {
                            shootingDirection = users[i, j].GetKeyboardAngle();
                            keybHelper = true;
                        }

                        if (!keybHelper)
                            if (users[i, j].controlDistance == 0)
                                shootingDirection = users[i, j].direction;
                            //ball.CalculateKickA(users[i, j].buttonPowers[0], users[i, j].direction, users[i, j].controlDistance, users[i, j].IsMouseAiming(), extraPowerForCorner);
                            else
                                shootingDirection = users[i, j].angle;
                        //ball.CalculateKickA(users[i, j].buttonPowers[0], users[i, j].angle, users[i, j].controlDistance, users[i, j].IsMouseAiming(), extraPowerForCorner);

                        ball.CalculateKickA(users[i, j].buttonPowers[0], shootingDirection, users[i, j].controlDistance, users[i, j].IsMouseAiming(), extraPowerForCorner);

                        AddTouchDelay();
                        UpdateController(i, j);
                        disabledPID = -1;
                        disabledTID = -1;
                        AddKeeperReaction();

                        DisableFreekickTaker(i, j);
                        ResetFreekickAndCorner();
                        CheckPlayersInOffside();
                        keeperDistanceShot = -1;
                        BroadcastBlockedAreaRemove(false);
                        CheckIfDistanceShoot();

                    }
                }

            return true;
        }

        void BallKickingB()
        {
            if (autoMoving > 0) return;
            if (throwInTID > -1) return;
            if (goalkickTID > -1) return;
            if (keepers[0].ballInPossesDelay > 0) return;
            if (keepers[1].ballInPossesDelay > 0) return;
            //if (ball.height > Proc.getVolleyHeight(2, true)) return false;  //even biggest body cant reach ball

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (freekickTID > -1 && freekickTID != i) continue;
                    if (cornerTID > -1 && cornerTID != i) continue;
                    if (throwInTID > -1 && throwInTID != i) continue;
                    if (freekickTID > -1 && freekickTakerPID != j) continue;
                    if (cornerTID > -1 && cornerTakerPID != j) continue;
                    if (!IsPlayerOnline(i, j)) continue;
                    if (users[i, j].IsMouseAiming()) continue;
                    if (users[i, j].buttons[1]) continue;
                    if (users[i, j].buttonPowers[1] == 0) continue;

                    if (users[i, j].touchDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (ball.height > users[i, j].GetVolleyHeight()) continue;

                    if (disabledTID == i && disabledPID == j) continue;
                    //if (ball.height > Proc.getVolleyHeight(users[i, j].body, users[i, j].vip)) continue;
                    //if (Proc.isThrowInCornerFreekick(sID))
                    //    if (!Proc.isFreekickTaker(sID, i, j)) continue;

                    if (cornerTID == i)
                    {
                        users[i, j].angle = ForceCornerToField(users[i, j].angle);
                        users[i, j].direction = users[i, j].angle;
                    }

                    double passingDir;
                    int receiverPID = -1;

                    //set passing dir
                    if (users[i, j].controlDistance == 0)
                        passingDir = users[i, j].direction;
                    else
                        passingDir = users[i, j].angle;

                    //reset potential receivers
                    for (int k = 0; k < maxPlayers; k++)
                        potentialReceiver[k] = false;

                    if (users[i, j].distToBall < GetKickingArea())
                    {
                        //autopass
                        if (users[i, j].buttonPowers[1] < 32)
                        {
                            bool b = TryFindPassReceiver(45, i, j, passingDir);
                            if (!b) b = TryFindPassReceiver(135, i, j, passingDir);
                            if (!b) return;  //no pass receiver found, lets cancel autopass

                            double nearestDist = 100000;

                            //find nearest receiver
                            for (int k = 0; k < maxPlayers; k++)
                            {
                                if (!potentialReceiver[k]) continue;
                                double d = F.Distance(users[i, j].coords[0], users[i, j].coords[1], users[i, k].coords[0], users[i, k].coords[1]);
                                if (d < nearestDist)
                                {
                                    nearestDist = d;
                                    receiverPID = k;
                                }
                            }

                            //double receiverAngle = F.Angle(ball.coords.ToArray(), users[i, receiverPID].coords.ToArray());
                            //users[i, receiverPID].AIDoPress = true;
                            if (ball.speed > 0) cornerInProgress = false;  //if ball contains speed, we can be sure, that this isnt corner any more
                            ball.CalculateAutopass(i, receiverPID, users[i, receiverPID].coords[0], users[i, receiverPID].coords[1], users[i, receiverPID].direction, users[i, receiverPID].speed);
                        }
                        //direct shot
                        else
                        {
                            if (ball.speed > 0) cornerInProgress = false;  //if ball contains speed, we can be sure, that this isnt corner any more
                            ball.CalculateKickB(users[i, j].buttonPowers[1], passingDir);
                        }


                        AddTouchDelay();
                        UpdateController(i, j);
                        disabledPID = -1;
                        disabledTID = -1;
                        AddKeeperReaction();

                        DisableFreekickTaker(i, j);
                        ResetFreekickAndCorner();
                        CheckPlayersInOffside();
                        keeperDistanceShot = -1;
                        BroadcastBlockedAreaRemove(false);
                        CheckIfDistanceShoot();

                    }
                }


        }

        void BallKickingMobileVolley()
        {
            if (ball.height < 0.05) return;
            if (autoMoving > 0) return;
            if (throwInTID > -1) return;
            if (goalkickTID > -1) return;
            if (keepers[0].ballInPossesDelay > 0) return;
            if (keepers[1].ballInPossesDelay > 0) return;
            //if (ball.height > Proc.getVolleyHeight(2, true)) return false;  //even biggest body cant reach ball

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (freekickTID > -1 && freekickTID != i) continue;
                    if (cornerTID > -1 && cornerTID != i) continue;
                    if (throwInTID > -1 && throwInTID != i) continue;
                    if (freekickTID > -1 && freekickTakerPID != j) continue;
                    if (cornerTID > -1 && cornerTakerPID != j) continue;
                    if (!IsPlayerOnline(i, j)) continue;
                    //if (users[i, j].IsMouseControl()) continue;
                    if (users[i, j].controlMethod == ControlMethod.Mouse || users[i, j].controlMethod == ControlMethod.MouseKeyboard) continue; //lets allow only mobile
                    if (!users[i, j].buttons[1]) continue;

                    if (users[i, j].touchDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (ball.height > users[i, j].GetVolleyHeight()) continue;

                    if (disabledTID == i && disabledPID == j) continue;
                    //if (ball.height > Proc.getVolleyHeight(users[i, j].body, users[i, j].vip)) continue;
                    //if (Proc.isThrowInCornerFreekick(sID))
                    //    if (!Proc.isFreekickTaker(sID, i, j)) continue;


                    if (users[i, j].distToBall < GetKickingArea()/* * 2*/) //mobile users used to have bigger heading area, but perhaps its unfair solution
                    {
                        if (cornerTID == i)
                        {
                            users[i, j].angle = ForceCornerToField(users[i, j].angle);
                            users[i, j].direction = users[i, j].angle;
                        }

                        if (ball.speed > 0) cornerInProgress = false;  //if ball contains speed, we can be sure, that this isnt corner any more

                        if (users[i, j].controlDistance == 0)
                            ball.CalculateKickC(users[i, j].buttonPowers[2], users[i, j].direction, 300, true);
                        else
                            ball.CalculateKickC(users[i, j].buttonPowers[2], users[i, j].angle, 300, true);

                        AddTouchDelay();
                        UpdateController(i, j);
                        disabledPID = -1;
                        disabledTID = -1;
                        AddKeeperReaction();



                        DisableFreekickTaker(i, j);
                        ResetFreekickAndCorner();
                        CheckPlayersInOffside();
                        keeperDistanceShot = -1;
                        BroadcastBlockedAreaRemove(false);
                        CheckIfDistanceShoot();

                    }

                }


        }

        bool TryFindPassReceiver(double aheadAngle, int tID, int pID, double passingDir)
        {
            double angleToReceiver, f, a;
            bool receiverFound = false;

            for (int j = 0; j < maxPlayers; j++)
            {
                if (j == pID) continue;
                if (F.Distance(users[tID, pID].coords[0], users[tID, pID].coords[1], users[tID, j].coords[0], users[tID, j].coords[1]) > 3) continue;

                angleToReceiver = F.Angle(users[tID, pID].coords[0], users[tID, pID].coords[1], users[tID, j].coords[0], users[tID, j].coords[1]);

                f = 180 - passingDir;
                a = angleToReceiver + f;
                if (a >= 360) a -= 360;
                if (a < 0) a += 360;
                //if (a < 157.5f || a > 202.5f) continue;
                if (a < 180 - (aheadAngle / 2) || a > 180 + (aheadAngle / 2)) continue;

                receiverFound = true;
                potentialReceiver[j] = true;
            }

            return receiverFound;
        }

        bool BallKickingC()
        {
            if (autoMoving > 0) return false;
            if (throwInTID > -1) return false;
            if (goalkickTID > -1) return false;
            if (keepers[0].ballInPossesDelay > 0) return false;
            if (keepers[1].ballInPossesDelay > 0) return false;
            //if (ball.height > Proc.getVolleyHeight(2, true)) return false;  //even biggest body cant reach ball

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (freekickTID > -1 && freekickTID != i) continue;
                    if (cornerTID > -1 && cornerTID != i) continue;
                    if (throwInTID > -1 && throwInTID != i) continue;
                    if (freekickTID > -1 && freekickTakerPID != j) continue;
                    if (cornerTID > -1 && cornerTakerPID != j) continue;
                    if (!IsPlayerOnline(i, j)) continue;

                    if (users[i, j].IsMouseAiming())
                    {
                        if (!users[i, j].buttons[2]) continue;
                    }
                    else
                    {
                        if (users[i, j].buttons[2]) continue;
                        if (users[i, j].buttonPowers[2] == 0) continue;
                    }

                    if (users[i, j].touchDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (ball.height > users[i, j].GetVolleyHeight()) continue;

                    if (disabledTID == i && disabledPID == j) continue;
                    //if (ball.height > Proc.getVolleyHeight(users[i, j].body, users[i, j].vip)) continue;
                    //if (Proc.isThrowInCornerFreekick(sID))
                    //    if (!Proc.isFreekickTaker(sID, i, j)) continue;


                    if (users[i, j].distToBall < GetKickingArea())
                    {
                        if (cornerTID == i)
                        {
                            users[i, j].angle = ForceCornerToField(users[i, j].angle);
                            users[i, j].direction = users[i, j].angle;
                        }

                        if (ball.speed > 0) cornerInProgress = false;  //if ball contains speed, we can be sure, that this isnt corner any more

                        if (users[i, j].controlDistance == 0)
                            ball.CalculateKickC(users[i, j].buttonPowers[2], users[i, j].direction, users[i, j].controlDistance, users[i, j].IsMouseAiming());
                        else
                            ball.CalculateKickC(users[i, j].buttonPowers[2], users[i, j].angle, users[i, j].controlDistance, users[i, j].IsMouseAiming());

                        AddTouchDelay();
                        UpdateController(i, j);
                        disabledPID = -1;
                        disabledTID = -1;
                        AddKeeperReaction();



                        DisableFreekickTaker(i, j);
                        ResetFreekickAndCorner();
                        CheckPlayersInOffside();
                        keeperDistanceShot = -1;
                        BroadcastBlockedAreaRemove(false);
                        CheckIfDistanceShoot();

                    }
                }

            return true;
        }

        void AIBallKicking()
        {
            if (autoMoving > 0) return;
            if (!botsEnabled) return;
            if (throwInTID > -1) return;
            if (freekickTID > -1) return;
            if (cornerTID > -1) return;
            if (goalkickTID > -1) return;
            if (keepers[0].ballInPossesDelay > 0) return;
            if (keepers[1].ballInPossesDelay > 0) return;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (IsPlayerOnline(i, j)) continue;
                    if (users[i, j].touchDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (disabledTID == i && disabledPID == j) continue;
                    if (users[i, j].distToBall > GetKickingArea()) continue;
                    if (ball.height > users[i, j].GetVolleyHeight()) continue;

                    //AI cant shoot "one-timers", it will wait 0.5 seconds, before shoot (except, if keeper is diving)
                    if (keepers[0].divingDelay == 0 && keepers[1].divingDelay == 0)
                        if (users[i, j].AIBallInPossesTime < 25) continue;

                    double goalY = 0;

                    //check direction of goal
                    if (i == 0 && homeSide == 1) goalY = Field.GOALLINE_N;
                    if (i == 1 && homeSide == 1) goalY = Field.GOALLINE_P;
                    if (i == 0 && homeSide == 2) goalY = Field.GOALLINE_P;
                    if (i == 1 && homeSide == 2) goalY = Field.GOALLINE_N;

                    //if close enough goal, shoot
                    if (F.Distance(users[i, j].coords[0], users[i, j].coords[1], 0, goalY) < 3.0)
                    {
                        double shootingX = 0;
                        double a;
                        bool keeperOutOfPosition = false;

                        if (ball.coords[0] > 0) shootingX = Field.SIDENET_P - 0.1; else shootingX = Field.SIDENET_N + 0.1;
                        shootingX += (F.rand.NextDouble() - 0.5) / 2.5;  //value between -0.2 <--> 0.2

                        //if keeper is out of position (have dived) lets shoot to another corner/center of goal
                        if (goalY > 0)
                        {
                            if (keepers[0].divingDelay > 0)
                            {
                                if (keepers[0].coords[0] > 0)
                                {
                                    double d = F.rand.NextDouble() / 2;  //0 <--> 0.5
                                    shootingX = 0 - d;
                                    keeperOutOfPosition = true;
                                }
                                else
                                {
                                    double d = F.rand.NextDouble() / 2;  //0 <--> 0.5
                                    shootingX = 0 + d;
                                    keeperOutOfPosition = true;
                                }
                            }
                        }
                        else
                        {
                            if (keepers[1].divingDelay > 0)
                            {
                                if (keepers[1].coords[0] > 0)
                                {
                                    double d = F.rand.NextDouble() / 2;  //0 <--> 0.5
                                    shootingX = 0 - d;
                                    keeperOutOfPosition = true;
                                }
                                else
                                {
                                    double d = F.rand.NextDouble() / 2;  //0 <--> 0.5
                                    shootingX = 0 + d;
                                    keeperOutOfPosition = true;
                                }
                            }
                        }

                        a = F.Angle(ball.coords[0], ball.coords[1], shootingX, goalY);

                        if (keeperOutOfPosition)
                            ball.AIQuickShot(a);
                        else
                            ball.CalculateKickA(150, a, 0, false, false);

                        AddTouchDelay();
                        UpdateController(i, j);
                        disabledPID = -1;
                        disabledTID = -1;
                        AddKeeperReaction();
                        cornerInProgress = false;

                        DisableFreekickTaker(i, j);
                        ResetFreekickAndCorner();
                        CheckPlayersInOffside();
                        keeperDistanceShot = -1;
                        CheckIfDistanceShoot();
                        BroadcastBlockedAreaRemove(false);

                    }
                }
        }

        void AIBallPassing()
        {
            if (autoMoving > 0) return;
            if (!botsEnabled) return;
            if (throwInTID > -1) return;
            if (freekickTID > -1) return;
            if (cornerTID > -1) return;
            if (goalkickTID > -1) return;
            if (keepers[0].ballInPossesDelay > 0) return;
            if (keepers[1].ballInPossesDelay > 0) return;

            for (int i = 0; i < 2; i++)
            {
                if (currentControllerTID != i) continue;
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (currentControllerPID != j) continue;
                    if (IsPlayerOnline(i, j)) continue;
                    if (users[i, j].touchDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (disabledTID == i && disabledPID == j) continue;
                    if (ball.height > users[i, j].GetVolleyHeight()) continue;

                    int oppTID;
                    if (i == 0) oppTID = 1; else oppTID = 0;

                    //if player is close enough opponents net, lets not pass
                    double[] oppNetPos = GetOpponentsNetPos(i);
                    double distToOppNet = F.Distance(users[i, j].coords[0], users[i, j].coords[1], 0, oppNetPos[1]);
                    if (distToOppNet < 2.4) return;

                    int passRequestPID = IsPassRequest(i, j);

                    //if opponent AREN'T near and nobody human player requests pass, lets quit
                    if (!IsOpponentNear(users[i, j].coords[0], users[i, j].coords[1], oppTID) && passRequestPID == -1) return;

                    //reset potential receivers
                    for (int k = 0; k < maxPlayers; k++)
                        potentialReceiver[k] = false;

                    double passingDir = users[i, j].direction;
                    int receiverPID = -1;

                    bool isShortPassPossibility = false;
                    bool isCrossingPossibility = false;

                    //opponent is near, so lets try to pass someone, who is near.
                    //human player havent request pass
                    if (passRequestPID == -1)
                    {
                        isShortPassPossibility = TryFindPassReceiver(45, i, j, passingDir);
                        if (!isShortPassPossibility)
                            isShortPassPossibility = TryFindPassReceiver(135, i, j, passingDir);


                        //if player is own defending zone, he may do cross
                        double[] ownNetPos = GetOwnTeamNetPos(i);
                        double distToOwnNet = F.Distance(0, users[i, j].coords[1], 0, ownNetPos[1]);

                        //crossing is only possible, if player is own "defending field"
                        if (distToOwnNet < Field.GOALLINE_P/* / 1.5*/)
                            isCrossingPossibility = true;

                        //check, that some cross receiver is enough far away 
                        if (isCrossingPossibility)
                        {
                            bool crossReceiverFound = false;
                            for (int k = 0; k < maxPlayers; k++)
                            {
                                //skip players, which are behind
                                if (ownNetPos[1] > 0)
                                    if (users[i, k].coords[1] > users[i, j].coords[1]) continue;
                                if (ownNetPos[1] < 0)
                                    if (users[i, k].coords[1] < users[i, j].coords[1]) continue;

                                double d = F.Distance(users[i, j].coords[0], users[i, j].coords[1], users[i, k].coords[0], users[i, k].coords[1]);
                                if (d > 3.0)
                                {
                                    crossReceiverFound = true;
                                    break;
                                }
                            }
                            isCrossingPossibility = crossReceiverFound;
                        }


                        //AI must have possession for awhile, before he can perform cross
                        if (users[i, j].AIBallInPossesTime < 25) isCrossingPossibility = false;

                        //no pass or cross receiver found, lets cancel autopass
                        if (!isShortPassPossibility && !isCrossingPossibility) return;

                        double nearestDist = 100000;
                        double farthestDist = 0;

                        //AI can also do shortpass and cross, so lets randomly choose one
                        if (isShortPassPossibility && isCrossingPossibility)
                        {
                            int r = F.rand.Next(0, 2);
                            if (r == 0)
                                isShortPassPossibility = false;
                            else
                                isCrossingPossibility = false;
                        }

                        //lets do shortpass. find nearest receiver
                        if (isShortPassPossibility && !isCrossingPossibility)
                            for (int k = 0; k < maxPlayers; k++)
                            {
                                if (!potentialReceiver[k]) continue;
                                double d = F.Distance(users[i, j].coords[0], users[i, j].coords[1], users[i, k].coords[0], users[i, k].coords[1]);
                                if (d < nearestDist)
                                {
                                    nearestDist = d;
                                    receiverPID = k;
                                }
                            }

                        //lets do cross. find farthest receiver (also receiver needs to be least 3.0 distance away)
                        if (!isShortPassPossibility && isCrossingPossibility)
                            for (int k = 0; k < maxPlayers; k++)
                            {
                                //deny crossing back
                                if (ownNetPos[1] > 0)
                                    if (users[i, k].coords[1] > users[i, j].coords[1]) continue;
                                if (ownNetPos[1] < 0)
                                    if (users[i, k].coords[1] < users[i, j].coords[1]) continue;

                                double d = F.Distance(users[i, j].coords[0], users[i, j].coords[1], users[i, k].coords[0], users[i, k].coords[1]);
                                if (d > farthestDist)
                                {
                                    farthestDist = d;
                                    receiverPID = k;
                                }
                            }
                    }

                    //human player requests pass
                    if (passRequestPID > -1)
                    {
                        receiverPID = passRequestPID;
                        isShortPassPossibility = true;
                        isCrossingPossibility = false;  //temporary AI wont cross to player
                    }



                    if (isShortPassPossibility)
                        ball.CalculateAutopass(i, receiverPID, users[i, receiverPID].coords[0], users[i, receiverPID].coords[1], users[i, receiverPID].direction, users[i, receiverPID].speed);

                    if (isCrossingPossibility)
                    {
                        double dir = F.Angle(users[i, j].coords[0], users[i, j].coords[1], users[i, receiverPID].coords[0], users[i, receiverPID].coords[1]);

                        int r = F.rand.Next(0, 2);
                        if (r == 0)
                            ball.CalculateKickA(150, dir, 0, false, false);
                        else
                            ball.CalculateKickC(150, dir, 0, false);
                    }

                    //********************

                    AddTouchDelay();
                    UpdateController(i, j);
                    disabledPID = -1;
                    disabledTID = -1;
                    AddKeeperReaction();

                    DisableFreekickTaker(i, j);
                    ResetFreekickAndCorner();
                    CheckPlayersInOffside();
                    keeperDistanceShot = -1;
                    CheckIfDistanceShoot();

                }
            }
        }

        int IsPassRequest(int tID, int pID)
        {
            for (int j = 0; j < maxPlayers; j++)
            {
                if (j == pID) continue;
                if (users[tID, j].slideDelay > 0) continue;
                if (users[tID, j].fallDelay > 0) continue;

                if (users[tID, j].IsMouseAiming())
                {
                    if (users[tID, j].buttons[2]) return j;
                }
                else
                {
                    if (users[tID, j].buttons[1]) return j;
                }
            }

            return -1;
        }

        bool IsOpponentNear(double posX, double posY, int opponentTID)
        {
            for (int j = 0; j < maxPlayers; j++)
            {
                if (users[opponentTID, j].fallDelay > 0) continue;
                if (users[opponentTID, j].slideDelay > 0) continue;
                if (F.Distance(posX, posY, users[opponentTID, j].coords[0], users[opponentTID, j].coords[1]) < 0.5) return true;
            }

            return false;
        }

        void GKAI()
        {
            StopKeeperMoving();

            GKAIGoalkick();
            GKAIGoToDistanceShot();
            GKAIDecideIfToStartDive();

            GKAIDiving();
            GKAIPositioning();  //also runs to ball, if its near enough
            GKAIKeeperPickUpBall();
        }

        void WhistlePossibleOffside()
        {
            if (ballInNetDelayer > 0) return;
            if (IsThrowInCornerFreekick()) return;
            if (autoMoving > 0) return;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (!IsPlayerOnline(i, j) && !botsEnabled) continue;
                    if (!users[i, j].offside) continue;
                    if (users[i, j].distToBall > 0.20) continue;

                    freekickCoords[0] = users[i, j].coords[0];
                    freekickCoords[1] = users[i, j].coords[1];  //this is new feature, which have been requested lot. freekickY position will be, where player touches ball

                    if (freekickCoords[0] > Field.THROWIN_P) freekickCoords[0] = Field.THROWIN_P - 0.1;
                    if (freekickCoords[0] < Field.THROWIN_N) freekickCoords[0] = Field.THROWIN_N + 0.1;

                    if (freekickCoords[1] > 6.1) freekickCoords[1] = 6.1;
                    if (freekickCoords[1] < -6.1) freekickCoords[1] = -6.1;

                    if (i == 0) freekickTID = 1; else freekickTID = 0;

                    fouledTID = -1;
                    timerEnabled = false;
                    SetAutomoveFromFreekickArea(4);
                    cancelFreekickDelayer = 500;

                    AddOffsideStat(users[i, j].pID);
                    teams[i].offsides++;

                    ResetOffside();
                    ResetAutopassReceivers();
                    ResetPressing();

                    return;
                }
        }

        void WhistleFreekick()
        {
            if (fouledTID == -1) return;

            if (foulDelay > 0)
            {
                foulDelay--;
                return;
            }

            freekickTID = fouledTID;
            fouledTID = -1;
            isPenalty = false;

            if (freekickCoords[0] > Field.THROWIN_P) freekickCoords[0] = Field.THROWIN_P - 0.1;
            if (freekickCoords[0] < Field.THROWIN_N) freekickCoords[0] = Field.THROWIN_N + 0.1;
            if (freekickCoords[1] > 6.1) freekickCoords[1] = 6.1;
            if (freekickCoords[1] < -6.1) freekickCoords[1] = -6.1;

            //check, if penalty
            if (freekickCoords[0] < Field.KEEPER16_AREA_XP && freekickCoords[0] > Field.KEEPER16_AREA_XN)
            {
                if (freekickCoords[1] > Field.KEEPER16_AREA_YP)
                {
                    isPenalty = true;
                    freekickCoords[0] = 0;
                    freekickCoords[1] = Field.PENALTY_P;
                }
                if (freekickCoords[1] < Field.KEEPER16_AREA_YN)
                {
                    isPenalty = true;
                    freekickCoords[0] = 0;
                    freekickCoords[1] = Field.PENALTY_N;
                }
            }


            timerEnabled = false;
            if (isPenalty)
                SetAutomoveFromFreekickArea(3);
            else
                SetAutomoveFromFreekickArea(4);

            //AddOffsideStat(users[i, j].pID);
            //teams[i].offsides++;


            ResetOffside();
            ResetAutopassReceivers();
            ResetPressing();
        }

        void SlideTackle()
        {
            if (autoMoving > 0) return;

            //start slide tackle, if button pressed
            if (goalkickTID == -1 && throwInTID == -1 && cornerTID == -1 && freekickTID == -1)
            {
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                    {
                        if (!IsPlayerOnline(i, j)) continue;
                        if (users[i, j].slideDelay > 0) continue;
                        if (users[i, j].fallDelay > 0) continue;
                        if (users[i, j].IsMouseAiming())
                        {
                            if (!users[i, j].buttons[1]) continue;
                        }
                        else
                        {
                            if (!users[i, j].buttons[2]) continue;
                        }
                        if (users[i, j].automoveFromFreekickArea) continue;
                        if (keepers[0].ballInPossesDelay > 0 || keepers[1].ballInPossesDelay > 0) continue;

                        //if player is too near at ball, he cant start tackling
                        if (users[i, j].distToBall <= 0.13) continue;

                        if (users[i, j].controlMethod != ControlMethod.MouseKeyboard)
                            if (users[i, j].controlDistance > 0)
                                users[i, j].direction = users[i, j].angle;

                        users[i, j].slideDelay = slideDuration;
                        users[i, j].speed += users[i, j].GetSlideTackleSpeed();
                    }
            }

            byte oppTID;

            //check, if slidetackle hits to opponent
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (!IsPlayerOnline(i, j) && !botsEnabled) continue;
                    if (users[i, j].slideDelay == 0) continue;
                    if (users[i, j].speed < 0.015) continue;
                    if (keepers[0].ballInPossesDelay > 0 || keepers[1].ballInPossesDelay > 0) continue;

                    if (i == 0) oppTID = 1; else oppTID = 0;

                    for (int k = 0; k < maxPlayers; k++)
                    {
                        if (!IsPlayerOnline(oppTID, k) && !botsEnabled) continue;
                        if (users[oppTID, k].slideDelay > 0) continue;
                        if (users[oppTID, k].fallDelay > 0) continue;

                        if (F.Distance(users[i, j].coords[0], users[i, j].coords[1], users[oppTID, k].coords[0], users[oppTID, k].coords[1]) > users[i, j].GetSlideTackleArea()) continue;
                        if (users[oppTID, k].distToBall > (users[i, j].GetSlideTackleArea() * 2)) continue;

                        users[oppTID, k].fallDelay = 60;

                        //freekickCoords[0] = users[oppTID, k].coords[0];
                        //freekickCoords[1] = users[oppTID, k].coords[1];

                        //fouledTID = oppTID;
                        //foulDelay = 10;
                    }
                }

            //check, if slidetackle hits to ball
            if (ball.height < 0.1 && keepers[0].ballInPossesDelay == 0 && keepers[1].ballInPossesDelay == 0)
            {
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                    {
                        if (!IsPlayerOnline(i, j) && !botsEnabled) continue;
                        if (users[i, j].slideDelay == 0) continue;
                        if (disabledTID == i && disabledPID == j) continue;
                        if (keepers[0].ballInPossesDelay > 0 || keepers[1].ballInPossesDelay > 0) continue;

                        if (users[i, j].distToBall > users[i, j].GetSlideTackleArea()) continue;

                        //fouledTID = -1;
                        disabledTID = -1;
                        disabledPID = -1;
                        ball.speed = users[i, j].speed + 0.0174;
                        ball.angle = users[i, j].direction;

                        AddKeeperReaction();
                        AddTouchDelay();
                        UpdateController(i, j);
                        //CheckPlayersInOffside();

                        keeperDistanceShot = -1;
                        ResetAutopassReceivers();

                    }
            }


            //move player
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].slideDelay > 0)
                    {
                        if (!IsPlayerOnline(i, j) && !botsEnabled) continue;

                        users[i, j].speed -= 0.00058;
                        if (users[i, j].speed < 0) users[i, j].speed = 0;

                        users[i, j].Move();
                        users[i, j].slideDelay--;
                    }

            //fall delay
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].fallDelay > 0)
                    {
                        users[i, j].speed -= 0.00058;
                        if (users[i, j].speed < 0) users[i, j].speed = 0;

                        users[i, j].Move();
                        users[i, j].fallDelay--;
                    }

        }

        void StopKeeperMoving()
        {
            if (goalkickTID > -1 || ballInNetDelayer > 0 || cornerTID > -1)
                for (int i = 0; i < 2; i++)
                    if (keepers[i].divingDelay == 0)
                        keepers[i].anim = 0;

            if (keeperReaction > 0)
                keeperReaction--;

            if (keeperCornerWaitDelay > 0)
                keeperCornerWaitDelay--;
        }

        void GKAIGoalkick()
        {
            if (autoMoving > 0) return;
            if (goalkickTID == -1) return;
            if (ball.speed > 0) return;
            if (keepers[0].divingDelay > 0) return;
            if (keepers[1].divingDelay > 0) return;

            //lets check that all players are off from area
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (!IsPlayerOnline(i, j) && !botsEnabled) continue;
                    if (users[i, j].automoveFromFreekickArea) return;
                }

            //*************

            int kID;
            double[] dest = new double[2];
            double maxSpeed = keepers[0].GetMaxSpeed();

            //lets solve, which keeper to use and where ball is placed
            if (goalkickCoords[1] > 0)
            {
                kID = 0;
                if (goalkickCoords[0] < 0)
                    dest[0] = Field.GOALKICK_XN;
                else
                    dest[0] = Field.GOALKICK_XP;
                dest[1] = Field.GOALLINE_P;
            }
            else
            {
                kID = 1;
                if (goalkickCoords[0] < 0)
                    dest[0] = Field.GOALKICK_XN;
                else
                    dest[0] = Field.GOALKICK_XP;
                dest[1] = Field.GOALLINE_N;
            }

            //*************

            //keeper runs behind of ball...
            if (keeperGoalkickScript == 0)
            {
                keepers[kID].angle = F.Angle(keepers[kID].coords[0], keepers[kID].coords[1], dest[0], dest[1]);
                keepers[kID].speed += 0.0012;
                if (keepers[kID].speed > maxSpeed) keepers[kID].speed = maxSpeed;

                keepers[kID].Move();
                keepers[kID].anim = 1;

                //keeper is now behind of ball
                if (F.Distance(keepers[kID].coords[0], keepers[kID].coords[1], dest[0], dest[1]) < 0.03)
                {
                    keepers[kID].speed = 0;
                    //lets turn keeper look at ball
                    if (goalkickCoords[1] < 0)
                        keepers[kID].angle = 90;
                    else
                        keepers[kID].angle = 270;

                    keepers[kID].coords[0] = dest[0];
                    keepers[kID].coords[1] = dest[1];
                    keeperGoalkickScript = 1;
                    keeperGoalkickScriptDelay = 100;
                    keepers[kID].anim = 0;
                }
            }

            //wait some time behind ball...
            if (keeperGoalkickScript == 1)
            {
                keeperGoalkickScriptDelay--;
                if (keeperGoalkickScriptDelay == 0)
                    keeperGoalkickScript = 2;
            }

            //run to ball and kick it
            if (keeperGoalkickScript == 2)
            {
                keepers[kID].angle = F.Angle(keepers[kID].coords[0], keepers[kID].coords[1], goalkickCoords[0], goalkickCoords[1]);
                keepers[kID].speed += 0.0012;
                if (keepers[kID].speed > maxSpeed) keepers[kID].speed = maxSpeed;

                keepers[kID].Move();
                keepers[kID].anim = 1;

                int _tID = -1;

                //keeper kicks ball
                if (F.Distance(keepers[kID].coords[0], keepers[kID].coords[1], goalkickCoords[0], goalkickCoords[1]) < 0.03)
                {
                    if (kID == 0 && homeSide == 1) _tID = 0;
                    if (kID == 0 && homeSide == 2) _tID = 1;
                    if (kID == 1 && homeSide == 1) _tID = 1;
                    if (kID == 1 && homeSide == 2) _tID = 0;

                    if (!GKPass(kID, _tID))
                        GKKick(kID);

                    ResetOffside();
                    keepers[kID].speed = 0;
                    keeperGoalkickScript = 0;
                    goalkickTID = -1;
                    ResetAutomoveFromFreekickArea();
                    BroadcastBlockedAreaRemove(false);
                }

            }

        }

        void GKKick(int kID)
        {
            ball.zSpeed = 50;
            ball.speed = 0.055;

            ResetController();
            if (kID == 0 && homeSide == 1) lastControllersTID[0] = 0;
            if (kID == 0 && homeSide == 2) lastControllersTID[0] = 1;
            if (kID == 1 && homeSide == 1) lastControllersTID[0] = 1;
            if (kID == 1 && homeSide == 2) lastControllersTID[0] = 0;
            CheckPlayersInOffside();

            if (ball.coords[1] < 0)
                ball.angle = 90 + (F.rand.Next(0, 40) - 20);
            else
                ball.angle = 270 + (F.rand.Next(0, 40) - 20);

            keepers[kID].touchDelay = 25;
            keeperDistanceShot = -1;
            cornerInProgress = false;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j].touchDelay = 25;

            ResetAutomoveFromFreekickArea();


        }

        bool GKPass(int kID, int tID)
        {
            double lowestDistance = 1000;
            int pID = -1;
            double d;

            for (int j = 0; j < maxPlayers; j++)
            {
                if (!IsPlayerOnline(tID, j)) continue;

                if (users[tID, j].IsMouseAiming())
                {
                    if (!users[tID, j].buttons[2]) continue;
                }
                else
                {
                    if (!users[tID, j].buttons[1]) continue;
                }


                //if player is in goal area, keeper wont pass to him.
                if (kID == 0)
                    if (users[tID, j].coords[1] > Field.GOALKICK_YP)
                        if (users[tID, j].coords[0] < Field.GOALKICK_XP && users[tID, j].coords[0] > Field.GOALKICK_XN)
                            continue;
                if (kID == 1)
                    if (users[tID, j].coords[1] < Field.GOALKICK_YN)
                        if (users[tID, j].coords[0] < Field.GOALKICK_XP && users[tID, j].coords[0] > Field.GOALKICK_XN)
                            continue;

                d = F.Distance(ball.coords[0], ball.coords[1], users[tID, j].coords[0], users[tID, j].coords[1]);

                if (d < lowestDistance)
                {
                    lowestDistance = d;
                    pID = j;
                }
            }

            //**********

            if (lowestDistance > 5.0) return false;
            if (pID == -1) return false;

            ball.CalculateAutopass(tID, pID, users[tID, pID].coords[0], users[tID, pID].coords[1], users[tID, pID].direction, users[tID, pID].speed);

            ResetController();
            if (kID == 0 && homeSide == 1) lastControllersTID[0] = 0;
            if (kID == 0 && homeSide == 2) lastControllersTID[0] = 1;
            if (kID == 1 && homeSide == 1) lastControllersTID[0] = 1;
            if (kID == 1 && homeSide == 2) lastControllersTID[0] = 0;
            CheckPlayersInOffside();

            keepers[kID].touchDelay = 25;
            keeperDistanceShot = -1;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j].touchDelay = 25;

            ResetAutomoveFromFreekickArea();

            return true;
        }

        void GKAIGoToDistanceShot()
        {
            if (keeperDistanceShot == -1) return;
            if (autoMoving > 0) return;

            double d;
            int kID = keeperDistanceShot;
            double maxSpeed = keepers[0].GetMaxSpeed();

            //lets check, if ball is close enough keeper
            //d = Proc.distance(keepers[kID].coords[0], keepers[kID].coords[1], ball.coords[0], ball.coords[1]);
            //if (d > 180) return;

            d = F.Distance(keepers[kID].coords[0], keepers[kID].coords[1], keeperDistanceCoords[0], keeperDistanceCoords[1]);

            //keeper start to run to position
            if (d > 0.02)
            {
                keepers[kID].speed += 0.0012;
                if (keepers[kID].speed > maxSpeed) keepers[kID].speed = maxSpeed;
                keepers[kID].angle = F.Angle(keepers[kID].coords[0], keepers[kID].coords[1], keeperDistanceCoords[0], keeperDistanceCoords[1]);
                keepers[kID].Move();
                keepers[kID].anim = 1;
            }
            //keeper is in his position, lets stop him
            else
            {
                //lets turn keeper watching to ball
                keepers[kID].angle = F.Angle(keepers[kID].coords[0], keepers[kID].coords[1], ball.coords[0], ball.coords[1]);

                keepers[kID].speed = 0;
                keepers[kID].anim = 0;

                for (int i = 0; i < 2; i++)
                    keepers[kID].coords[i] = keeperDistanceCoords[i];
            }

        }

        void GKAIDecideIfToStartDive()
        {
            int kID = -1;

            //if (keeperReaction > 0) return;
            if (autoMoving > 0) return;
            if (goalkickTID > -1) return;

            if (keeperDistanceShot > -1) return;
            if (keepers[0].divingDelay > 0) return;
            if (keepers[1].divingDelay > 0) return;

            if (ball.coords[0] > 1.3 || ball.coords[0] < -1.3) return;
            if (ball.coords[1] < Field.GOALLINE_P - 1.0 && ball.coords[1] > Field.GOALLINE_N + 1.0) return;

            if (ball.coords[1] > Field.KEEPER16_AREA_YP) kID = 0;
            if (ball.coords[1] < Field.KEEPER16_AREA_YN) kID = 1;
            if (kID == -1) return;

            //check, if ball is coming towards goal
            if (kID == 0)
                if (ball.angle < 25 || ball.angle > 155) return;
            if (kID == 1)
                if (ball.angle < 205 || ball.angle > 335) return;

            //if ball have passed keeper, no reason to dive
            if (kID == 0)
                if (ball.coords[1] > keepers[0].coords[1]) return;
            if (kID == 1)
                if (ball.coords[1] < keepers[1].coords[1]) return;

            //if own player have kicked ball, lets not dive
            if (kID == 0)
            {
                if (homeSide == 1)
                    if (lastControllersTID[0] == 0) return;
                if (homeSide == 2)
                    if (lastControllersTID[0] == 1) return;
            }
            else
            {
                if (homeSide == 2)
                    if (lastControllersTID[0] == 0) return;
                if (homeSide == 1)
                    if (lastControllersTID[0] == 1) return;
            }

            //ball is moving "wrong" direction. this usually happens when ball bounces from post. so lets not start dive
            if (kID == 0 && ball.angle > 180) return;
            if (kID == 1 && ball.angle < 180) return;

            TempBall tempBall = new TempBall(ball.coords[0], ball.coords[1], ball.angle, ball.speed, ball.height, ball.zSpeed);
            tempBall.CalculateTempBall2(keepers[kID].coords[1]);

            if (tempBall.speed < 0.02f) return;

            //ball coming towards keeper, no need to dive
            if (tempBall.coords[0] > (keepers[kID].coords[0] - 0.2) && tempBall.coords[0] < (keepers[kID].coords[0] + 0.2)) return;

            double xDist;

            //****************************
            //******  dive left  *********
            //****************************
            if (tempBall.coords[0] < keepers[kID].coords[0])
            {
                xDist = keepers[kID].coords[0] - tempBall.coords[0];
                if (xDist <= 0.1) keepers[kID].speed = ball.speed / 6;
                if (xDist <= 0.3 && xDist > 0.1) keepers[kID].speed = ball.speed / 5;
                if (xDist <= 0.5 && xDist > 0.3) keepers[kID].speed = ball.speed / 4;
                if (xDist > 0.5) keepers[kID].speed = ball.speed / 3;
                keepers[kID].angle = 180;
                keepers[kID].divingDelay = 70;
                keepers[kID].animID = 11;
                if (keepers[kID].speed < 0.015) keepers[kID].speed = 0.015;
            }
            //****************************
            //******  dive right  ********
            //****************************
            else
            {
                xDist = keepers[kID].coords[0] - tempBall.coords[0];
                if (xDist <= 0.1) keepers[kID].speed = ball.speed / 6;
                if (xDist <= 0.3 && xDist > 0.1) keepers[kID].speed = ball.speed / 5;
                if (xDist <= 0.5 && xDist > 0.3) keepers[kID].speed = ball.speed / 4;
                if (xDist > 0.5) keepers[kID].speed = ball.speed / 3;
                keepers[kID].angle = 0;
                keepers[kID].divingDelay = 70;
                keepers[kID].animID = 15;
                if (keepers[kID].speed < 0.015) keepers[kID].speed = 0.015;
            }

        }

        void GKAIDiving()
        {

            for (int i = 0; i < 2; i++)
                if (keepers[i].divingDelay > 0)
                {
                    keepers[i].divingDelay--;

                    if (keepers[i].divingDelay == 60 && keepers[i].animID == 11)
                        keepers[i].animID = 12;
                    if (keepers[i].divingDelay == 50 && keepers[i].animID == 12)
                        keepers[i].animID = 13;
                    if (keepers[i].divingDelay == 40 && keepers[i].animID == 13)
                    {
                        keepers[i].animID = 14;
                        keepers[i].speed = 0;
                    }
                    if (keepers[i].divingDelay == 0 && keepers[i].animID == 14)
                    {
                        keepers[i].animID = 0;
                        keepers[i].touchDelay = 0;
                    }

                    if (keepers[i].divingDelay == 60 && keepers[i].animID == 15)
                        keepers[i].animID = 16;
                    if (keepers[i].divingDelay == 50 && keepers[i].animID == 16)
                        keepers[i].animID = 17;
                    if (keepers[i].divingDelay == 40 && keepers[i].animID == 17)
                    {
                        keepers[i].animID = 18;
                        keepers[i].speed = 0;
                    }
                    if (keepers[i].divingDelay == 0 && keepers[i].animID == 18)
                    {
                        keepers[i].animID = 0;
                        keepers[i].touchDelay = 0;
                    }

                    keepers[i].Move();

                    //*******************
                    //*****  save  ******
                    //*******************

                    //restrict that ball wont hit to keeper, if it bounces from post
                    if (ball.angle > 180 && ball.coords[1] > 0) return;
                    if (ball.angle < 180 && ball.coords[1] < 0) return;

                    if (keepers[i].touchDelay > 0) continue;

                    if (keepers[i].distToBall > 0.16) continue;

                    if (ball.height > Field.TOPPOST) continue;

                    ball.speed -= 0.02;
                    if (ball.speed < 0) ball.speed += 0.02;

                    F.rand.Next(0, 45);

                    keepers[i].touchDelay = 25;

                    if (i == 0)
                    {
                        if (keepers[i].animID >= 11 && keepers[i].animID <= 14)
                            ball.angle = 202.5f - F.rand.Next(0, 45);
                        if (keepers[i].animID >= 15 && keepers[i].animID <= 18)
                            ball.angle = 337.5 + F.rand.Next(0, 45);
                    }
                    else
                    {
                        if (keepers[i].animID >= 11 && keepers[i].animID <= 14)
                            ball.angle = 157.5f + F.rand.Next(0, 45);
                        if (keepers[i].animID >= 15 && keepers[i].animID <= 18)
                            ball.angle = 22.5f - F.rand.Next(0, 45);
                    }

                    if (ball.angle > 360) ball.angle -= 360;
                    if (ball.angle < 0) ball.angle += 360;

                    if (IsOpponetsShot()) AddShotStat(true);
                    ResetController();
                    UpdateControllerKeeper(i);
                }

        }

        void GKAIPositioning()
        {
            if (keeperDistanceShot > -1) return;

            //keeper will wait center of goal during cornerkick
            //if servers[sID].cornerTID=0 then

            if (autoMoving > 0) return;
            if (goalkickTID > -1) return;
            if (ballInNetDelayer > 0) return;
            if (keepers[0].divingDelay > 0) return;
            if (keepers[1].divingDelay > 0) return;
            if (keepers[0].ballInPossesDelay > 0) return;
            if (keepers[1].ballInPossesDelay > 0) return;

            double maxDistance = 1000.0;
            bool outOfGoalline, ownPlayerControllingBall;
            double d, r, y, a;
            int tID;
            double[] wantedCoords = new double[2];
            double maxSpeed = keepers[0].GetMaxSpeed();

            if (ball.coords[1] > (Field.GOALLINE_P + Field.BALLRADIUS) || ball.coords[1] < (Field.GOALLINE_N - Field.BALLRADIUS))
                outOfGoalline = true;
            else
                outOfGoalline = false;

            //**************

            for (int i = 0; i < 2; i++)
            {
                //****************************************
                //if ball is near enough, and opponents are faw away, keeper starts running to ball
                //****************************************
                if (keeperDistanceShot == -1 && freekickTID == -1 && ball.speed < 0.02 && keeperCornerWaitDelay == 0 && !outOfGoalline)
                    if (ball.coords[0] > Field.KEEPER16_AREA_XN && ball.coords[0] < Field.KEEPER16_AREA_XP)
                        if ((i == 0 && ball.coords[1] > Field.KEEPER16_AREA_YP) || (i == 1 && ball.coords[1] < Field.KEEPER16_AREA_YN))
                        {
                            //lets check, which distance of players is nearest to ball
                            for (int j = 0; j < 2; j++)
                                for (int k = 0; k < maxPlayers; k++)
                                {
                                    if (!IsPlayerOnline(j, k) && !botsEnabled) continue;
                                    if (users[j, k].distToBall < maxDistance) maxDistance = users[j, k].distToBall;
                                }

                            //keeper is nearer to ball, than any player. lets run to ball
                            if (keepers[i].distToBall < maxDistance)
                            {
                                keepers[i].speed += 0.0012;
                                if (keepers[i].speed > maxSpeed) keepers[i].speed = maxSpeed;
                                keepers[i].angle = F.Angle(keepers[i].coords[0], keepers[i].coords[1], ball.coords[0], ball.coords[1]);

                                keepers[i].Move();
                                keepers[i].anim = 1;
                                continue;
                            }
                        }

                //****************************************
                //if action gets too near at keeper, he will run to ball
                //****************************************

                if (keeperDistanceShot == -1 && freekickTID == -1 && keeperCornerWaitDelay == 0 && !outOfGoalline)
                    if (ball.coords[0] > Field.GOALKICK_XN && ball.coords[0] < Field.GOALKICK_XP)
                        if ((i == 0 && ball.coords[1] > Field.GOALKICK_YP) || (i == 1 && ball.coords[1] < Field.GOALKICK_YN))
                        {
                            ownPlayerControllingBall = false;
                            tID = -1;

                            //own player have kicked ball. lets not pick up ball
                            if (i == 0)
                            {
                                if (homeSide == 1) tID = 0;
                                if (homeSide == 2) tID = 1;
                            }
                            else
                            {
                                if (homeSide == 2) tID = 0;
                                if (homeSide == 1) tID = 1;
                            }

                            if (tID == -1) return;

                            //if own player is controlling ball, lets not rush to ball
                            for (int j = 0; j < maxPlayers; j++)
                            {
                                if (users[tID, j].pID == 0) continue;

                                if (users[tID, j].distToBall < 0.07)
                                {
                                    ownPlayerControllingBall = true;
                                    break;
                                }
                            }

                            //keeper runs to ball
                            if (!ownPlayerControllingBall)
                            {
                                keepers[i].speed += 0.0012;
                                if (keepers[i].speed > maxSpeed) keepers[i].speed = maxSpeed;
                                keepers[i].angle = F.Angle(keepers[i].coords[0], keepers[i].coords[1], ball.coords[0], ball.coords[1]);

                                keepers[i].Move();
                                keepers[i].anim = 1;
                                continue;
                            }
                        }

                //****************************************
                //lets calculate wanted coords
                //****************************************

                if (keeperDistanceShot == i) continue;

                if (i == 0)
                    y = Field.GOALLINE_P;
                else
                    y = Field.GOALLINE_N;

                d = F.Distance(0, y, ball.coords[0], ball.coords[1]);
                a = F.Angle(0, y, ball.coords[0], ball.coords[1]);

                //restrict that keeper wong hang out faaaaar away from goal
                //if (d > 800) d = 800;

                r = Math.Cos(F.ToRad * a) * d / 5.0;
                wantedCoords[0] = 0 + r;
                r = Math.Sin(F.ToRad * a) * d / 5.0;
                wantedCoords[1] = y + r;

                if (cornerTID > -1) wantedCoords[0] = 0;
                if (keeperCornerWaitDelay > 0) wantedCoords[0] = 0;

                //lets deny keepers going inside to goal
                if (ball.coords[1] > 0)
                    if (wantedCoords[1] > Field.GOALLINE_P - 0.02)
                        wantedCoords[1] = Field.GOALLINE_P - 0.02;
                if (ball.coords[1] < 0)
                    if (wantedCoords[1] < Field.GOALLINE_N + 0.02)
                        wantedCoords[1] = Field.GOALLINE_N + 0.02;

                d = F.Distance(keepers[i].coords[0], keepers[i].coords[1], wantedCoords[0], wantedCoords[1]);

                //****************************************
                //keeper is wanted coords position
                //****************************************
                if (d < 0.03)
                {
                    keepers[i].speed = 0;
                    //lets turn keeper watching to ball
                    keepers[i].angle = F.Angle(keepers[i].coords[0], keepers[i].coords[1], ball.coords[0], ball.coords[1]);

                    keepers[i].coords[0] = wantedCoords[0];
                    keepers[i].coords[1] = wantedCoords[1];
                }
                //****************************************
                //keeper must run to wanted coord position
                //****************************************
                else
                {
                    keepers[i].speed += 0.0012;
                    if (keepers[i].speed > maxSpeed) keepers[i].speed = maxSpeed;
                    keepers[i].angle = F.Angle(keepers[i].coords[0], keepers[i].coords[1], wantedCoords[0], wantedCoords[1]);

                    keepers[i].Move();
                    keepers[i].anim = 1;
                }

            }
        }

        void GKAIKeeperPickUpBall()
        {
            if (autoMoving > 0) return;
            if (cornerTID > -1) return;
            if (goalkickTID > -1) return;
            if (freekickTID > -1) return;
            if (ballInNetDelayer > 0) return;
            if (keepers[0].divingDelay > 0) return;
            if (keepers[1].divingDelay > 0) return;

            //if ball is near enough, keeper picks it up (or kick, if team-mate have passed it)
            if (keepers[0].ballInPossesDelay == 0 && keepers[1].ballInPossesDelay == 0 && ball.height < Field.TOPPOST)
                for (int i = 0; i < 2; i++)
                {
                    if (keepers[i].touchDelay > 0) continue;
                    //ball is too far away, lets continue
                    if (keepers[i].distToBall > 0.16) continue;

                    //own player have kicked ball. lets not pick up ball
                    if (i == 0)
                    {
                        if (homeSide == 1)
                            if (lastControllersTID[0] == 0 && lastControllersPID[0] > -1)
                            {
                                if (!GKPass(i, 0))
                                    GKKick(i);
                                continue;
                            }
                        if (homeSide == 2)
                            if (lastControllersTID[0] == 1 && lastControllersPID[0] > -1)
                            {
                                if (!GKPass(i, 1))
                                    GKKick(i);
                                continue;
                            }
                    }
                    else
                    {
                        if (homeSide == 2)
                            if (lastControllersTID[0] == 0 && lastControllersPID[0] > -1)
                            {
                                if (!GKPass(i, 0))
                                    GKKick(i);
                                continue;
                            }
                        if (homeSide == 1)
                            if (lastControllersTID[0] == 1 && lastControllersPID[0] > -1)
                            {
                                if (!GKPass(i, 1))
                                    GKKick(i);
                                continue;
                            }
                    }
                    //end of 'own player have kicked ball. lets not pick up ball'

                    //add shots, if ball have enough speed 
                    if (ball.speed > 0.02)
                        if (IsOpponetsShot())
                            AddShotStat(true);

                    //keeper picks up ball
                    keepers[i].speed = 0;
                    ball.speed = 0;
                    keepers[i].ballInPossesDelay = 100;
                    ball.coords[0] = keepers[i].coords[0];
                    keepers[i].anim = 2;

                    keeperDistanceShot = -1;
                    ResetPressing();
                    ResetAutopassReceivers();
                    ResetOffside();
                    cornerInProgress = false;

                    if (i == 0)
                    {
                        keepers[i].angle = 270;
                        ball.coords[1] = keepers[i].coords[1] - 0.01;
                    }
                    else
                    {
                        keepers[i].angle = 90;
                        ball.coords[1] = keepers[i].coords[1] + 0.01;
                    }

                    SetAutomoveFromFreekickArea(3);
                    break;

                }

            //**************

            if (keepers[0].ballInPossesDelay == 0 && keepers[1].ballInPossesDelay == 0) return;

            //lets check that all players are off from area
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (!IsPlayerOnline(i, j) && !botsEnabled) continue;
                    if (users[i, j].automoveFromFreekickArea) return; //some players are still in area. keeper wont kick yet...
                }

            int tID = -1;

            //all players are off from goalkick area.
            //lets wait a while and kick ball
            for (int i = 0; i < 2; i++)
                if (keepers[i].ballInPossesDelay > 0)
                {
                    keepers[i].ballInPossesDelay--;
                    if (keepers[i].ballInPossesDelay > 0) continue;

                    if (i == 0 && homeSide == 1) tID = 0;
                    if (i == 0 && homeSide == 2) tID = 1;
                    if (i == 1 && homeSide == 1) tID = 1;
                    if (i == 1 && homeSide == 2) tID = 0;

                    bool moveBallOutFromPost = false;
                    double r;
                    double angle;

                    //lets move ball little bit, so it wont hit to post, incase keeper is holding ball right near post
                    if (F.Distance(Field.SIDENET_N, Field.GOALLINE_N, ball.coords[0], ball.coords[1]) < 0.055)
                        moveBallOutFromPost = true;

                    if (F.Distance(Field.SIDENET_P, Field.GOALLINE_N, ball.coords[0], ball.coords[1]) < 0.055)
                        moveBallOutFromPost = true;

                    if (F.Distance(Field.SIDENET_N, Field.GOALLINE_P, ball.coords[0], ball.coords[1]) < 0.055)
                        moveBallOutFromPost = true;

                    if (F.Distance(Field.SIDENET_P, Field.GOALLINE_P, ball.coords[0], ball.coords[1]) < 0.055)
                        moveBallOutFromPost = true;

                    if (moveBallOutFromPost)
                    {
                        angle = F.Angle(ball.coords[0], ball.coords[1], 0, 0);
                        r = Math.Cos(F.ToRad * angle) * 0.055;
                        ball.coords[0] = ball.coords[0] + r;
                        r = Math.Sin(F.ToRad * angle) * 0.055;
                        ball.coords[1] = ball.coords[1] + r;
                    }

                    if (!GKPass(i, tID))
                        GKKick(i);

                    BroadcastBlockedAreaRemove(false);
                }


        }

        void CheckPlayersInOffside()
        {
            byte upDown = 0;
            byte oppTID;
            ResetOffside();
            int tID = lastControllersTID[0];
            int pID = lastControllersPID[0];

            //if pID is -1, keeper have kicked ball!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

            double offsideLine = 0;
            if (tID == 0) oppTID = 1; else oppTID = 0;

            if (tID == 0 && homeSide == 1) upDown = 2;
            if (tID == 0 && homeSide == 2) upDown = 1;
            if (tID == 1 && homeSide == 1) upDown = 1;
            if (tID == 1 && homeSide == 2) upDown = 2;

            //offsideline by lowest opposition
            for (int i = 0; i < maxPlayers; i++)
            {
                if (!IsPlayerOnline(oppTID, i) && !botsEnabled) continue;
                if (upDown == 1)
                {
                    if (users[oppTID, i].coords[1] > offsideLine)
                        offsideLine = users[oppTID, i].coords[1];
                }
                else
                {
                    if (users[oppTID, i].coords[1] < offsideLine)
                        offsideLine = users[oppTID, i].coords[1];
                }
            }

            //offsideline by passer/kicker
            if (pID > -1)
            {
                if (upDown == 1)
                {
                    if (users[tID, pID].coords[1] > offsideLine)
                        offsideLine = users[tID, pID].coords[1];
                }
                else
                {
                    if (users[tID, pID].coords[1] < offsideLine)
                        offsideLine = users[tID, pID].coords[1];
                }
            }

            bool sendFlagging = false;

            //lets check players, who are in offside
            for (int i = 0; i < maxPlayers; i++)
            {
                if (i == pID) continue;
                if (!IsPlayerOnline(tID, i) && !botsEnabled) continue;

                if (upDown == 1)
                {
                    if (users[tID, i].coords[1] > offsideLine)
                    {
                        users[tID, i].offside = true;
                        sendFlagging = true;
                    }
                }
                else
                {
                    if (users[tID, i].coords[1] < offsideLine)
                    {
                        users[tID, i].offside = true;
                        sendFlagging = true;
                    }
                }
            }


            if (sendFlagging) BroadcastRefFlags(true);

        }

        void BroadcastBlockedArea(byte action, sbyte teamAllowedToEnter)
        {
            removeBlockAreaMSGSent = false;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)25);

            outmsg.Write(action);    //2=corner, 3=goalkick area blocked, 4=freekick
            outmsg.Write(timerEnabled);
            outmsg.Write(teamAllowedToEnter);

            double[] _blockedCoords = new double[2];

            //corner
            if (action == 2)
                for (int i = 0; i < 2; i++)
                    _blockedCoords[i] = cornerCoords[i];

            //goalkick area blocked
            if (action == 3)
                _blockedCoords[1] = ball.coords[1];

            //freekick
            if (action == 4)
                for (int i = 0; i < 2; i++)
                    _blockedCoords[i] = freekickCoords[i];

            for (int i = 0; i < 2; i++)
                outmsg.Write((Int16)Math.Round(_blockedCoords[i] * 3000));

            List<NetConnection> recipients = new List<NetConnection>();

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j))
                        recipients.Add(users[i, j].connection);

            lock (spectatorData2)
            {
                for (int i = 0; i < spectatorData2.Count; i++)
                    if (spectatorData2[i].connection != null)
                        recipients.Add(spectatorData2[i].connection);
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);
        }

        public void BroadcastBlockedAreaRemove(bool playWhistleSound)
        {
            if (!playWhistleSound) //this was added later. if something bugs, its becouse of this
                if (removeBlockAreaMSGSent) return;
            removeBlockAreaMSGSent = true;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)26);

            outmsg.Write(timerEnabled);
            outmsg.Write(playWhistleSound);

            List<NetConnection> recipients = new List<NetConnection>();

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j))
                        recipients.Add(users[i, j].connection);

            lock (spectatorData2)
            {
                for (int i = 0; i < spectatorData2.Count; i++)
                    if (spectatorData2[i].connection != null)
                        recipients.Add(spectatorData2[i].connection);
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);
        }

        void BroadcastRefFlags(bool flagging)
        {
            if (offsideFlagging == flagging) return;
            offsideFlagging = flagging;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)89);
            outmsg.Write(offsideFlagging);

            List<NetConnection> recipients = new List<NetConnection>();

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j))
                        recipients.Add(users[i, j].connection);

            lock (spectatorData2)
            {
                for (int i = 0; i < spectatorData2.Count; i++)
                    if (spectatorData2[i].connection != null)
                        recipients.Add(spectatorData2[i].connection);
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);
        }

        void ResetFreekickAndCorner()
        {
            bool b = false;

            if (cornerTID > -1)
            {
                keeperCornerWaitDelay = 80;
                b = true;
            }
            if (freekickTID > -1) b = true;

            cornerTID = -1;
            cornerTakerPID = -1;
            freekickTID = -1;
            freekickTakerPID = -1;
            ResetAutomoveFromFreekickArea();

            if (!b) return;

            timerEnabled = true;
        }

        void CheckIfDistanceShoot()
        {
            //lets check, if ball is very far away from goal
            if (ball.coords[1] > Field.GOALLINE_P - 3.5 || ball.coords[1] < Field.GOALLINE_N + 3.5) return;

            TempBall tempBall = new TempBall(ball.coords[0], ball.coords[1], ball.angle, ball.speed, ball.height, ball.zSpeed);
            tempBall.CalculateTempBall();

            //if (tempBall.speed > 0.04f) return;  //TODO!! these cant be correct, becouse changed from ns3 source


            //lets check, if TempBall will go to goal
            if (tempBall.coords[1] > Field.GOALLINE_P || tempBall.coords[1] < Field.GOALLINE_N)
                if (tempBall.coords[0] < Field.SIDENET_P && tempBall.coords[0] > Field.SIDENET_N)
                {
                    if (tempBall.coords[1] > 0)
                    {
                        keeperDistanceShot = 0;
                        keeperDistanceCoords[1] = Field.GOALLINE_P - 0.03;
                    }
                    else
                    {
                        keeperDistanceShot = 1;
                        keeperDistanceCoords[1] = Field.GOALLINE_N + 0.03;
                    }

                    keeperDistanceCoords[0] = tempBall.coords[0];
                    //keeperDistanceCoords[1] = tempBall.coords[1];

                }
        }

        void DisableFreekickTaker(int tID, int pID)
        {
            if (freekickTID > -1 || cornerTID > -1)
            {
                disabledPID = pID;
                disabledTID = tID;
            }

            ResetDisabledPlayer(disabledTID);
        }

        void ResetDisabledPlayer(int tID)
        {
            if (tID == -1) return;
            if (botsEnabled) return;

            if (CountPlayersInTeam(tID) == 1)
            {
                disabledPID = -1;
                disabledTID = -1;
            }
        }

        void AddKeeperReaction()
        {
            if (keeperCornerWaitDelay > 0)
                keeperReaction = 20;
            else
                keeperReaction = 30;
        }

        void Delayers()
        {
            //touch delay decrease
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].touchDelay > 0)
                        users[i, j].touchDelay--;

            //ball is in goal
            if (ballInNetDelayer > 0)
            {
                ballInNetDelayer--;
                if (ballInNetDelayer == 0)
                {
                    ball.coords[0] = 0;
                    ball.coords[1] = 0;
                }
            }

            //keeper touch delay decrease
            for (int i = 0; i < 2; i++)
                if (keepers[i].touchDelay > 0)
                    keepers[i].touchDelay--;

        }

        void CancelFreekickDelayer()
        {

            if (throwInTID == -1 && cornerTID == -1 && freekickTID == -1) return;
            if (throwInTakerPID > -1 || cornerTakerPID > -1 || freekickTakerPID > -1) return;

            //cancel freekick/throwin/corner if nobody goes take it (this is against match ruiners)
            if (cancelFreekickDelayer > 0)
            {
                cancelFreekickDelayer--;
                if (cancelFreekickDelayer == 0)
                {
                    throwInTID = -1;
                    cornerTID = -1;
                    freekickTID = -1;

                    throwInTakerPID = -1;
                    cornerTakerPID = -1;
                    freekickTakerPID = -1;

                    timerEnabled = true;
                    BroadcastBlockedAreaRemove(true);
                }
            }
        }

        void ThrowIn()
        {
            if (throwInTID == -1) return;
            if (ball.speed > 0) return;

            ResetOffside();

            int tID = throwInTID;

            //lets wait until some player arrives to take throwin
            if (throwInTakerPID == -1)
            {
                for (int i = 0; i < maxPlayers; i++)
                    if (users[tID, i].distToBall < 0.1)
                    {
                        throwInTakerPID = i;
                        throwInDelay = 0;
                        break;
                    }
            }

            if (throwInTakerPID == -1) return;
            int pID = throwInTakerPID;

            //lets lock throwin taker
            users[tID, pID].coords[0] = throwInCoords[0];
            users[tID, pID].coords[1] = throwInCoords[1];
            users[tID, pID].speed = 0;

            throwInDelay++;

            //bot AI
            if (!IsPlayerOnline(tID, pID))
            {
                users[tID, pID].angle = DirectionToNearestTeammate(tID, pID, throwInCoords[0], throwInCoords[1]);
                users[tID, pID].angle = ForceThrowinToField(users[tID, pID].angle);
                users[tID, pID].direction = users[tID, pID].angle;

                if (throwInDelay > 100) //2 sec
                {
                    int nearestPID = GetNearestTeamMember(tID, pID);
                    double dist = F.Distance(users[tID, pID].coords[0], users[tID, pID].coords[1], users[tID, nearestPID].coords[0], users[tID, nearestPID].coords[1]);

                    int power = GetThrowinPowerByDistance(dist);

                    ball.DoThrowinByPower(power, users[tID, pID].angle);//150/15=10
                    AddTouchDelay();

                    //this will remove bug, where AI player will "jerk" awhile
                    users[tID, pID].wantedPos = new double[2];

                    UpdateController(tID, pID);
                    throwInTakerPID = -1;
                    throwInTID = -1;

                    disabledTID = tID;
                    disabledPID = pID;
                    ResetDisabledPlayer(disabledTID);

                    timerEnabled = true;
                    BroadcastTimer();
                    return;
                }
            }

            if (BallThrowingLeft()) return;
            if (BallThrowingRight()) return;

            //if throwint takes 10 sec, lets force throwin
            if (throwInDelay > 500)
            {
                users[tID, pID].angle = ForceThrowinToField(users[tID, pID].angle);
                ball.DoThrowinByPower(10, users[tID, pID].angle);//150/15=10
                AddTouchDelay();

                UpdateController(tID, pID);
                throwInTakerPID = -1;
                throwInTID = -1;

                disabledTID = tID;
                disabledPID = pID;
                ResetDisabledPlayer(disabledTID);

                timerEnabled = true;
                BroadcastTimer();
            }

        }

        int GetThrowinPowerByDistance(double dist)
        {
            if (dist > 3.70) return 10;
            if (dist > 3.35) return 9;
            if (dist > 3.00) return 8;
            if (dist > 2.64) return 7;
            if (dist > 2.50) return 6;
            if (dist > 2.11) return 5;
            if (dist > 2.02) return 4;
            if (dist > 1.58) return 3;
            if (dist > 1.50) return 2;
            return 1;
        }

        double ForceThrowinToField(double _angle)
        {
            if (ball.coords[0] > 0)
            {
                if (_angle < 91) _angle = 91;
                if (_angle > 269) _angle = 269;
            }
            else
            {
                if (_angle > 89 && _angle < 180) _angle = 89;
                if (_angle >= 180 && _angle < 271) _angle = 271;
            }

            return _angle;
        }

        double ForceCornerToField(double _angle)
        {
            //right&upper
            if (ball.coords[0] > 0 && ball.coords[1] > 0)
            {
                if (_angle < 181) _angle = 185;
                if (_angle > 269) _angle = 269;
            }
            //left&upper
            if (ball.coords[0] < 0 && ball.coords[1] > 0)
            {
                if (_angle < 271)
                {
                    if (_angle >= 180)
                        _angle = 271;
                    else
                        _angle = 355;
                }
            }
            //right&lower
            if (ball.coords[0] > 0 && ball.coords[1] < 0)
            {
                if (_angle < 91) _angle = 91;
                if (_angle > 179) _angle = 175;
            }
            //left&lower
            if (ball.coords[0] < 0 && ball.coords[1] < 0)
            {
                if (_angle > 89)
                {
                    if (_angle < 180)
                        _angle = 89;
                    else
                        _angle = 5;
                }
            }

            return _angle;
        }

        void Corner()
        {
            if (cornerTID == -1) return;
            if (ball.speed > 0) return;

            int tID = cornerTID;

            //lets wait until some player arrives to take corner
            if (cornerTakerPID == -1)
            {
                for (int i = 0; i < maxPlayers; i++)
                    if (users[tID, i].distToBall < 0.1)
                    {
                        cornerTakerPID = i;
                        cornerDelay = 0;
                        break;
                    }
            }

            if (cornerTakerPID == -1) return;
            int pID = cornerTakerPID;

            //lets lock corner taker
            users[tID, pID].coords[0] = cornerCoords[0];
            users[tID, pID].coords[1] = cornerCoords[1];
            users[tID, pID].speed = 0;

            cornerDelay++;

            //bot AI
            if (!IsPlayerOnline(tID, pID))
            {
                double y;
                if (cornerCoords[1] > 0)
                    y = Field.GOALKICK_YP - F.rand.NextDouble();
                else
                    y = Field.GOALKICK_YN + F.rand.NextDouble();

                users[tID, pID].angle = F.Angle(users[tID, pID].coords[0], users[tID, pID].coords[1], 0, y);
                users[tID, pID].direction = users[tID, pID].angle;

                if (cornerDelay > 100) //2 sec
                {
                    ball.CalculateAICornerKick(users[tID, pID].direction);
                    AddTouchDelay();

                    //this will remove bug, where AI player will "jerk" awhile
                    users[tID, pID].wantedPos = new double[2];

                    UpdateController(tID, pID);
                    cornerTakerPID = -1;
                    cornerTID = -1;

                    timerEnabled = true;
                    BroadcastBlockedAreaRemove(false);
                    return;
                }
            }

            //if corner takes 10 sec, lets force corner            
            if (cornerDelay > 500)
            {
                AddTouchDelay();
                UpdateController(tID, pID);
                timerEnabled = true;
                cornerTakerPID = -1;
                cornerTID = -1;
                ResetAutomoveFromFreekickArea();

                disabledTID = tID;
                disabledPID = pID;
                ResetDisabledPlayer(disabledTID);

                ball.speed = 0.065;
                ball.zSpeed = 0;
                users[tID, pID].angle = ForceCornerToField(users[tID, pID].angle);
                ball.angle = users[tID, pID].angle;
                BroadcastBlockedAreaRemove(false);
            }

        }

        void Offside()
        {
            if (freekickTID == -1) return;
            if (ball.speed > 0) return;

            int tID = freekickTID;

            //lets wait until some player arrives to take freekick
            if (freekickTakerPID == -1)
            {
                for (int i = 0; i < maxPlayers; i++)
                {
                    if (users[tID, i].slideDelay > 0) continue;
                    if (users[tID, i].fallDelay > 0) continue;

                    if (users[tID, i].distToBall < 0.1)
                    {
                        freekickTakerPID = i;
                        freekickDelay = 0;
                        break;
                    }
                }
            }

            if (freekickTakerPID == -1) return;
            int pID = freekickTakerPID;

            //lets lock freekick taker
            users[tID, pID].coords[0] = freekickCoords[0];
            users[tID, pID].coords[1] = freekickCoords[1];
            users[tID, pID].speed = 0;

            freekickDelay++;

            //bot AI
            if (!IsPlayerOnline(tID, pID))
            {
                users[tID, pID].angle = DirectionToNearestTeammate(tID, pID, freekickCoords[0], freekickCoords[1]);
                users[tID, pID].direction = users[tID, pID].angle;

                if (freekickDelay > 100) //2 sec
                {
                    int receiverPID = GetNearestTeamMember(tID, pID);

                    ball.CalculateAutopass(tID, receiverPID, users[tID, receiverPID].coords[0], users[tID, receiverPID].coords[1], users[tID, receiverPID].direction, users[tID, receiverPID].speed);

                    //ball.CalculateKickC(150, users[tID, pID].direction);
                    AddTouchDelay();

                    //this will remove bug, where AI player will "jerk" awhile
                    users[tID, pID].wantedPos = new double[2];

                    UpdateController(tID, pID);
                    freekickTakerPID = -1;
                    freekickTID = -1;

                    disabledTID = tID;
                    disabledPID = pID;
                    ResetDisabledPlayer(disabledTID);

                    timerEnabled = true;
                    BroadcastBlockedAreaRemove(false);
                    CheckPlayersInOffside();
                    return;
                }
            }

            //if freekick takes 10 sec, lets force offside
            if (freekickDelay > 500)
            {
                AddTouchDelay();
                UpdateController(tID, pID);
                timerEnabled = true;
                freekickTakerPID = -1;
                freekickTID = -1;
                ResetAutomoveFromFreekickArea();

                disabledTID = tID;
                disabledPID = pID;
                ResetDisabledPlayer(disabledTID);

                ball.speed = 0.065;
                ball.zSpeed = 0;
                ball.angle = users[tID, pID].angle;
                BroadcastBlockedAreaRemove(false);
                CheckPlayersInOffside();
            }

        }

        bool BallThrowingLeft()
        {
            int pID = throwInTakerPID;
            int tID = throwInTID;

            if (users[tID, pID].buttons[0]) return false;
            if (users[tID, pID].buttonPowers[0] == 0) return false;

            users[tID, pID].angle = ForceThrowinToField(users[tID, pID].angle);
            ball.DoThrowinByPower(users[tID, pID].buttonPowers[0] / 10, users[tID, pID].angle);
            AddTouchDelay();

            UpdateController(tID, pID);
            throwInTakerPID = -1;
            throwInTID = -1;

            disabledTID = tID;
            disabledPID = pID;
            ResetDisabledPlayer(disabledTID);

            timerEnabled = true;
            BroadcastTimer();

            return true;
        }

        bool BallThrowingRight()
        {
            int pID = throwInTakerPID;
            int tID = throwInTID;

            if (!users[tID, pID].buttons[1]) return false;

            users[tID, pID].angle = ForceThrowinToField(users[tID, pID].angle);
            ball.DoThrowinByPower(10, users[tID, pID].angle);
            AddTouchDelay();

            UpdateController(tID, pID);
            throwInTakerPID = -1;
            throwInTID = -1;

            disabledTID = tID;
            disabledPID = pID;
            ResetDisabledPlayer(disabledTID);

            timerEnabled = true;
            BroadcastTimer();

            return true;
        }

        void AddTouchDelay()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].distToBall < 0.2)
                        users[i, j].touchDelay = 25;
        }

        double GetKickingArea()
        {
            return 0.14;
        }

        void UpdateController(int tID, int pID)
        {
            //if user is already lastController, lets exit
            if (pID == lastControllersPID[0] && tID == lastControllersTID[0]) return;

            if (lastControllersTID[0] == tID)
            {
                lastControllersTID[1] = lastControllersTID[0];
                lastControllersPID[1] = lastControllersPID[0];
            }
            else
            {
                lastControllersTID[1] = -1;
                lastControllersPID[1] = -1;
            }

            lastControllersTID[0] = tID;
            lastControllersPID[0] = pID;

            if (lastControllersTID[0] == lastControllersTID[1])
                if (lastControllersPID[0] == lastControllersPID[1])
                {
                    lastControllersTID[1] = -1;
                    lastControllersPID[1] = -1;
                }

        }

        public bool IsThrowInCornerFreekick()
        {
            if (throwInTID > -1) return true;
            if (goalkickTID > -1) return true;
            if (cornerTID > -1) return true;
            if (freekickTID > -1) return true;

            return false;
        }

        public void SoundBroadcast(byte sound, bool timerEnabled)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)18);

            outmsg.Write(sound);
            outmsg.Write(timerEnabled);

            List<NetConnection> recipients = new List<NetConnection>();

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j))
                        recipients.Add(users[i, j].connection);

            lock (spectatorData2)
            {
                for (int i = 0; i < spectatorData2.Count; i++)
                    if (spectatorData2[i].connection != null)
                        recipients.Add(spectatorData2[i].connection);
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);

        }

        void BroadcastHafttimeStatsToClients()
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)15);

            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(teams[i].score);
                outmsg.Write(teams[i].possession);
                outmsg.Write(teams[i].shotsTotal);
                outmsg.Write(teams[i].shotsOnGoal);
                outmsg.Write(teams[i].goalKicks);
                outmsg.Write(teams[i].corners);
                outmsg.Write(teams[i].offsides);
                outmsg.Write(teams[i].throwIns);
            }

            List<NetConnection> recipients = new List<NetConnection>();

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j))
                        recipients.Add(users[i, j].connection);

            lock (spectatorData2)
            {
                for (int i = 0; i < spectatorData2.Count; i++)
                    if (spectatorData2[i].connection != null)
                        recipients.Add(spectatorData2[i].connection);
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);
        }

        void BroadcastGamedata()
        {
            udpSkip++;
            if (udpSkip < 2) return;
            udpSkip = 0;

            NetOutgoingMessage outmsg = server.CreateMessage(113);  //113
            outmsg.Write((byte)11);

            //player coords (Int16)
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    for (int k = 0; k < 2; k++)
                        outmsg.Write((Int16)Math.Round(users[i, j].coords[k] * 3000));


            //keeper coords (Int16)
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                    outmsg.Write((Int16)Math.Round(keepers[i].coords[j] * 3000));

            //ball coords (Int16)
            for (int i = 0; i < 2; i++)
                outmsg.Write((Int16)Math.Round(ball.coords[i] * 2000));

            //player angle & speed (byte)
            byte animID = 0;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    outmsg.Write((byte)Math.Round(users[i, j].direction / 1.42));
                    outmsg.Write((byte)Math.Round(users[i, j].speed * 1000));

                    animID = 0;

                    if (users[i, j].slideDelay > 0) animID = 1;
                    if (users[i, j].fallDelay > 0) animID = 2;
                    if (i == throwInTID && j == throwInTakerPID) animID = 3;
                    outmsg.Write(animID);
                }

            //keeper angle & speed (byte)
            for (int i = 0; i < 2; i++)
            {
                outmsg.Write((byte)Math.Round(keepers[i].angle / 1.42));
                outmsg.Write((byte)Math.Round(keepers[i].speed * 4000));

                animID = 0;
                if (keepers[i].divingDelay > 0) animID = 1;
                outmsg.Write(animID);
            }

            byte _ballHeight;

            if (keepers[0].ballInPossesDelay > 0 || keepers[1].ballInPossesDelay > 0)
                _ballHeight = 8;
            else if (throwInTakerPID > -1)
                _ballHeight = 20;  //this should differ by players body
            else
                _ballHeight = (byte)Math.Round(ball.height * 80);

            //ball angle & speed & height
            outmsg.Write((byte)Math.Round(ball.angle / 1.42));    //ball angle  
            outmsg.Write((byte)Math.Round(ball.speed * 2500));           //ball speed  
            outmsg.Write(_ballHeight);
            outmsg.Write((sbyte)Math.Round(ball.zSpeed));

            //ignore Z speed, if keeper have ball or throwin in progress
            if (keepers[0].ballInPossesDelay > 0 || keepers[1].ballInPossesDelay > 0 || throwInTakerPID > -1)
                outmsg.Write(true);
            else
                outmsg.Write(false);


            //ignore collision
            /*if (autoMoving > 0 || throwInTID > -1 || freekickTID > -1)
                outmsg.Write(true);
            else
                outmsg.Write(false);*/


            List<NetConnection> recipients = new List<NetConnection>();
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j) && users[i, j].udpTrafficEnabled)
                        recipients.Add(users[i, j].connection);

            lock (spectatorData2)
            {
                for (int i = 0; i < spectatorData2.Count; i++)
                    if (spectatorData2[i].connection != null && spectatorData2[i].udpTrafficEnabled)
                        recipients.Add(spectatorData2[i].connection);
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.UnreliableSequenced, 7);




        }

        //check just public room
        public bool IsRoomFull()
        {
            if (GetPlayerCount(true) == maxPlayers && GetPlayerCount(false) == maxPlayers)
                return true;
            else
                return false;
        }

        //check public&challenge
        public bool IsRoomFull(int tID)
        {
            if (roomType == RoomType.Public)
            {
                if (IsRoomFull())
                    return true;
                else
                    return false;
            }
            else
            {
                byte count = 0;

                if (teams[0].tID == tID)
                    count = GetPlayerCount(true);
                else
                    count = GetPlayerCount(false);

                if (count == maxPlayers)
                    return true;
                else
                    return false;
            }
        }

        public bool IsUserAlreadyInServer(string username)
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].username == username)
                        return true;

            return false;
        }

        public int AddPlayerToServer(
            NetConnection conn, int tID, int pID, string nation, string username, bool isVip,
            byte admas, byte body, byte skin, byte hair, byte number/*, byte[] shoe*/)
        {
            int pIDslot;
            int tIDslot;

            if (roomType == RoomType.Public)
                tIDslot = GetRandomTeamSide();
            else
                tIDslot = GetChallengeTeamSide(tID);

            pIDslot = FindEmptyPIDSlot(tIDslot);

            users[tIDslot, pIDslot] = new UserData(conn, tID, pID, nation, username, isVip, admas, body, skin, hair, number, /*shoe.ToArray(),*/ GetUniquePID());

            users[tIDslot, pIDslot].pos = GetAnyAvailablePos(tIDslot, false);

            int foundCount = 0;

            users[tIDslot, pIDslot].kickoffCoords = PositionHandler.GetKickoffPos(
                users[tIDslot, pIDslot].pos.position,
                users[tIDslot, pIDslot].pos.side,
                tIDslot,
                homeSide,
                IsNearestOf2KickoffPlayers(tIDslot, pIDslot, ref foundCount));

            //if challenge, add player to list (if not already added)
            if (roomType == RoomType.Challenge)
            {
                bool isAlreadyAdded = false;

                lock (challengePlayerData2)
                {

                    for (int i = 0; i < challengePlayerData2.Count; i++)
                        if (challengePlayerData2[i].pID == users[tIDslot, pIDslot].pID)
                        {
                            isAlreadyAdded = true;
                            break;
                        }

                    if (!isAlreadyAdded)
                        challengePlayerData2.Add(new ChallengePlayerData((byte)tIDslot, users[tIDslot, pIDslot].pID));

                }
            }

            return users[tIDslot, pIDslot].uniquePID;

        }


        //set kickoff coords
        /*  for (int i = 0; i < 2; i++)
              for (int j = 0; j < maxPlayers; j++)
              {
                  users[i, j].kickoffCoords = PositionHandler.GetKickoffPos(
                      users[i, j].pos.position,
                      users[i, j].pos.side,
                      i, homeSide,
                      IsNearestOf2KickoffPlayers(i, j, ref foundCount)
                      );
              }*/

        int GetUniquePID()
        {
            int uniquePID = 0;

            while (true)
            {
                bool isInvidualID = true;

                uniquePID++;

                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                        if (users[i, j].uniquePID == uniquePID)
                        {
                            isInvidualID = false;
                            break;
                        }

                if (isInvidualID) return uniquePID;
            }
        }

        int FindEmptyPIDSlot(int tIDslot)
        {
            for (int i = 0; i < maxPlayers; i++)
                if (!IsPlayerOnline(tIDslot, i))
                    return i;

            return -1;
        }

        int GetRandomTeamSide()
        {
            int tID = 0;
            byte[] count = new byte[2];

            count[0] = GetPlayerCount(true);
            count[1] = GetPlayerCount(false);

            if (count[0] > count[1]) tID = 1;
            if (count[1] > count[0]) tID = 0;
            if (count[0] == count[1])
            {
                int randNum = F.rand.Next(0, 100);
                if (randNum > 50)
                    tID = 0;
                else
                    tID = 1;
            }

            return tID;
        }

        int GetChallengeTeamSide(int tID)
        {
            if (teams[0].tID == tID)
                return 0;
            else
                return 1;
        }

        byte GetPlayerCount(bool getHomeSide)
        {
            int tID;
            byte count = 0;

            if (getHomeSide)
                tID = 0;
            else
                tID = 1;

            for (int j = 0; j < maxPlayers; j++)
                if (IsPlayerOnline(tID, j))
                    count++;

            return count;
        }

        public void DisconnectUser(int tID, int pID, byte disconnectType)  //disconnectType: 2=left from server, 3=kicked from server
        {
            if (throwInTID == tID && throwInTakerPID == pID) throwInTakerPID = -1;
            if (cornerTID == tID && cornerTakerPID == pID) cornerTakerPID = -1;
            if (freekickTID == tID && freekickTakerPID == pID) freekickTakerPID = -1;

            string username = users[tID, pID].username;

            //we need to also inform other users about disconnect
            users[tID, pID] = new UserData(roomType);

            //get positions for bot
            if (botsEnabled)
            {
                int foundCount = 0;

                users[tID, pID].pos = GetAnyAvailablePos(tID, true);
                users[tID, pID].kickoffCoords = PositionHandler.GetKickoffPos(
                    users[tID, pID].pos.position,
                    users[tID, pID].pos.side,
                    tID, homeSide,
                    IsNearestOf2KickoffPlayers(tID, pID, ref foundCount)
                    );
            }

            BroadcastJoinerData(username, null, disconnectType);

        }

        public void StartGame()
        {
            //*******  general stuff  *******  
            if (roomType == RoomType.Challenge) InitChallengeData();

            timerEnabled = false;
            time = 0;
            timeTicker = 0;
            period = 1;
            homeSide = (byte)F.rand.Next(1, 3);
            kickoff = (byte)F.rand.Next(0, 2);
            kickoffAtBegin = kickoff;
            autoMoving = 1;
            throwInTID = -1;
            goalkickTID = -1;
            cornerTID = -1;
            freekickTID = -1;
            throwInTakerPID = -1;
            cornerTakerPID = -1;
            freekickTakerPID = -1;
            keeperDistanceShot = -1;
            for (int i = 0; i < 2; i++)
            {
                lastControllersTID[i] = -1;
                lastControllersPID[i] = -1;
                teams[i].score = 0;
                teams[i].offsides = 0;
                teams[i].goalKicks = 0;
                teams[i].corners = 0;
                teams[i].throwIns = 0;
                teams[i].shotsTotal = 0;
                teams[i].shotsOnGoal = 0;
                teams[i].possession = 0;
            }

            ball = new Ball(this);

            SetKickOffCoords();
            SetPlrCoordsToBench();

            ResetTimeouts();

            roomState = RoomState.Running;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)14);

            outmsg.Write(period);
            outmsg.Write(homeSide);

            List<NetConnection> recipients = new List<NetConnection>();

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j))
                        recipients.Add(users[i, j].connection);

            lock (spectatorData2)
            {
                for (int i = 0; i < spectatorData2.Count; i++)
                    if (spectatorData2[i].connection != null)
                        recipients.Add(spectatorData2[i].connection);
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);

        }

        void ResetTimeouts()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j))
                        users[i, j].timeout = NetTime.Now;
        }

        void InitChallengeData()
        {
            lock (challengePlayerData2)
            {
                challengePlayerData2.Clear();

                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                    {
                        if (users[i, j].pID == 0) continue;

                        challengePlayerData2.Add(new ChallengePlayerData((byte)i, users[i, j].pID));
                    }
            }

            lock (goalData)
            {
                goalData.Clear();
            }
        }

        void SetPlrCoordsToBench()
        {
            for (int i = 0; i < 2; i++)
            {
                keepers[i].coords[0] = Field.BENCHX + F.rand.NextDouble();
                keepers[i].coords[1] = 0 + F.rand.NextDouble() / 2;
            }

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    users[i, j].coords[0] = Field.BENCHX + F.rand.NextDouble();
                    users[i, j].coords[1] = 0 + F.rand.NextDouble() / 2;
                }
        }

        public void SetKickOffCoords()
        {
            int foundCount = 0;

            keepers[0].kickoffCoords[0] = 0;
            keepers[0].kickoffCoords[1] = 5.76;

            keepers[1].kickoffCoords[0] = 0;
            keepers[1].kickoffCoords[1] = -5.76;

            for (int j = 0; j < maxPlayers; j++)
                users[kickoff, j].gotoKickoff = false;

            if (botsEnabled)
            {
                ResetBotPositions();

                //get positions for bots
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                    {
                        if (IsPlayerOnline(i, j)) continue;
                        users[i, j].pos = GetAnyAvailablePos(i, true);
                    }
            }

            //set kickoff coords
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    users[i, j].kickoffCoords = PositionHandler.GetKickoffPos(
                        users[i, j].pos.position,
                        users[i, j].pos.side,
                        i, homeSide,
                        IsNearestOf2KickoffPlayers(i, j, ref foundCount)
                        );
                }

        }

        void ResetBotPositions()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (IsPlayerOnline(i, j)) continue;
                    users[i, j].pos.position = Position.D;
                    users[i, j].pos.side = Side.R;
                }
        }

        int IsNearestOf2KickoffPlayers(int tID, int pID, ref int foundCount)
        {
            if (foundCount == 2) return -1;
            if (tID != kickoff) return -1;

            double nearest = 1000;
            int _pID = -1;

            for (int j = 0; j < maxPlayers; j++)
            {
                if (users[kickoff, j].gotoKickoff) continue;
                if (!botsEnabled && !IsPlayerOnline(kickoff, j)) continue;

                double[] _coords = PositionHandler.KickoffPositionToCoords(users[kickoff, j].pos.position, users[kickoff, j].pos.side);

                double d = F.Distance(_coords[0], _coords[1], 0, 0);
                if (d < nearest)
                {
                    nearest = d;
                    _pID = j;
                }
            }

            if (_pID > -1 && pID == _pID)
            {
                users[kickoff, _pID].gotoKickoff = true;
                foundCount++;
                return foundCount;
            }

            return -1;
        }

        public PositionAndSide GetAnyAvailablePos(int tID, bool isBot)
        {
            PositionAndSide pos = new PositionAndSide();
            pos.position = Position.D;  //just some default value
            pos.side = Side.R;          //just some default value

            if (IsPositionAvailable(tID, Position.F, Side.LC, isBot))
            {
                pos.position = Position.F;
                pos.side = Side.LC;
                return pos;
            }

            if (IsPositionAvailable(tID, Position.F, Side.RC, isBot))
            {
                pos.position = Position.F;
                pos.side = Side.RC;
                return pos;
            }

            if (IsPositionAvailable(tID, Position.M, Side.LC, isBot))
            {
                pos.position = Position.M;
                pos.side = Side.LC;
                return pos;
            }

            if (IsPositionAvailable(tID, Position.M, Side.RC, isBot))
            {
                pos.position = Position.M;
                pos.side = Side.RC;
                return pos;
            }

            if (IsPositionAvailable(tID, Position.D, Side.LC, isBot))
            {
                pos.position = Position.D;
                pos.side = Side.LC;
                return pos;
            }

            if (IsPositionAvailable(tID, Position.D, Side.RC, isBot))
            {
                pos.position = Position.D;
                pos.side = Side.RC;
                return pos;
            }

            if (IsPositionAvailable(tID, Position.M, Side.L, isBot))
            {
                pos.position = Position.M;
                pos.side = Side.L;
                return pos;
            }

            if (IsPositionAvailable(tID, Position.M, Side.R, isBot))
            {
                pos.position = Position.M;
                pos.side = Side.R;
                return pos;
            }

            if (IsPositionAvailable(tID, Position.D, Side.L, isBot))
            {
                pos.position = Position.D;
                pos.side = Side.L;
                return pos;
            }

            if (IsPositionAvailable(tID, Position.D, Side.R, isBot))
            {
                pos.position = Position.D;
                pos.side = Side.R;
                return pos;
            }

            return pos;
        }

        bool IsPositionAvailable(int tID, Position position, Side side, bool isBot)
        {
            for (int i = 0; i < maxPlayers; i++)
            {
                if (!isBot)
                    if (!IsPlayerOnline(tID, i)) continue;
                if (users[tID, i].pos.position == position && users[tID, i].pos.side == side)
                    return false;
            }

            return true;
        }

        public int CountPlayersInTeam(int tID)
        {
            int res = 0;

            for (int i = 0; i < maxPlayers; i++)
                if (IsPlayerOnline(tID, i))
                    res++;

            return res;
        }

        public void ResetAutomoveFromFreekickArea()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j].automoveFromFreekickArea = false;
        }

        public void BroadcastTimer()
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)16);

            outmsg.Write(timerEnabled);

            List<NetConnection> recipients = new List<NetConnection>();

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j))
                        recipients.Add(users[i, j].connection);

            lock (spectatorData2)
            {
                for (int i = 0; i < spectatorData2.Count; i++)
                    if (spectatorData2[i].connection != null)
                        recipients.Add(spectatorData2[i].connection);
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);

        }

        public void ResetOffside()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j].offside = false;

            BroadcastRefFlags(false);
        }

        public void ResetSlideTacklesAndFallDelays()
        {
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    users[i, j].slideDelay = 0;
                    users[i, j].fallDelay = 0;
                }
        }

        public void BroadcastInfoAboutGoal(byte sound, bool timerEnabled, bool ownGoal)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)19);

            outmsg.Write(timerEnabled);
            outmsg.Write(sound);
            outmsg.Write(ownGoal);
            outmsg.Write(teams[0].score);
            outmsg.Write(teams[1].score);

            int pID1 = lastControllersPID[0];
            int pID2 = lastControllersPID[1];
            int tID1 = lastControllersTID[0];
            int tID2 = lastControllersTID[1];
            string[] usernames = new string[2];
            bool isThereAssister = false;

            if (pID1 > -1 && tID1 > -1)
                usernames[0] = users[tID1, pID1].username;

            if (pID2 > -1 && tID2 > -1)
                if (tID1 == tID2)
                {
                    usernames[1] = users[tID2, pID2].username;
                    isThereAssister = true;
                }

            outmsg.Write(usernames[0]);
            outmsg.Write(usernames[1]);
            outmsg.Write(isThereAssister);

            List<NetConnection> recipients = new List<NetConnection>();

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j))
                        recipients.Add(users[i, j].connection);

            lock (spectatorData2)
            {
                for (int i = 0; i < spectatorData2.Count; i++)
                    if (spectatorData2[i].connection != null)
                        recipients.Add(spectatorData2[i].connection);
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);
        }

        public bool IsOwnGoal(int whichGoal)
        {
            //value in whichGoal needs to be 0-1
            bool res = false;

            if (homeSide == 1 && whichGoal == 0)
            {
                if (lastControllersTID[0] == 0)
                    res = true;
                else
                    res = false;
            }
            if (homeSide == 1 && whichGoal == 1)
            {
                if (lastControllersTID[0] == 1)
                    res = true;
                else
                    res = false;
            }
            if (homeSide == 2 && whichGoal == 1)
            {
                if (lastControllersTID[0] == 0)
                    res = true;
                else
                    res = false;
            }
            if (homeSide == 2 && whichGoal == 0)
            {
                if (lastControllersTID[0] == 1)
                    res = true;
                else
                    res = false;
            }

            return res;
        }

        void PowerBar(int action)
        {
            if (action == 1)
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                        if (IsPlayerOnline(i, j))
                            for (int k = 0; k < 3; k++)
                            {
                                if (!users[i, j].buttons[k]) continue;

                                users[i, j].buttonPowers[k] += 4.64;
                                if (users[i, j].buttonPowers[k] > 150)
                                    users[i, j].buttonPowers[k] = 150;
                            }


            //****************

            if (action == 2)
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                        if (IsPlayerOnline(i, j))
                            for (int k = 0; k < 3; k++)
                                if (!users[i, j].buttons[k])
                                    users[i, j].buttonPowers[k] = 0;

        }

        public void BroadcastJoinerData(string username, NetConnection conn, byte connectedOrDisconnected)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)17);

            outmsg.Write(maxPlayers);
            outmsg.Write(username);
            outmsg.Write(connectedOrDisconnected);  //1=connected to server, 2=left from server, 3=kicked from server
            outmsg.Write((byte)roomState);

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    outmsg.Write(users[i, j].nation);
                    outmsg.Write(users[i, j].username);
                    outmsg.Write(users[i, j].uniquePID);
                    outmsg.Write(users[i, j].ready);
                    outmsg.Write(users[i, j].isVip);
                    outmsg.Write(users[i, j].body);
                    outmsg.Write(users[i, j].skin);
                    outmsg.Write(users[i, j].hair);
                    outmsg.Write(users[i, j].number);
                    for (int k = 0; k < 3; k++)
                        outmsg.Write(users[i, j].shoe[k]);

                }

            if (conn != null)
            {
                server.SendMessage(outmsg, conn, NetDeliveryMethod.ReliableOrdered, 0);
            }
            else
            {
                List<NetConnection> recipients = new List<NetConnection>();

                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < maxPlayers; j++)
                        if (users[i, j].connection != null && IsPlayerOnline(i, j))
                            recipients.Add(users[i, j].connection);

                lock (spectatorData2)
                {
                    for (int i = 0; i < spectatorData2.Count; i++)
                        if (spectatorData2[i].connection != null)
                            recipients.Add(spectatorData2[i].connection);
                }

                if (recipients.Count > 0)
                    server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);
            }

        }

        void ResetController()
        {
            for (int i = 0; i < 2; i++)
            {
                lastControllersTID[i] = -1;
                lastControllersPID[i] = -1;
            }
        }

        void UpdateControllerKeeper(int kID)
        {
            if (kID == 0)
            {
                if (homeSide == 1)
                {
                    lastControllersTID[0] = 0;
                    lastControllersPID[0] = -1;
                }
                else
                {
                    lastControllersTID[0] = 1;
                    lastControllersPID[0] = -1;
                }
            }
            else
            {
                if (homeSide == 1)
                {
                    lastControllersTID[0] = 1;
                    lastControllersPID[0] = -1;
                }
                else
                {
                    lastControllersTID[0] = 0;
                    lastControllersPID[0] = -1;
                }
            }
        }

        int GetNearestBot(int tID, double posX, double posY)
        {
            double nearest = 1000;
            int pID = -1;

            for (int i = 0; i < maxPlayers; i++)
            {
                if (IsPlayerOnline(tID, i)) continue;

                double d = F.Distance(users[tID, i].coords[0], users[tID, i].coords[1], posX, posY);
                if (d < nearest)
                {
                    nearest = d;
                    pID = i;
                }
            }

            return pID;
        }

        void BroadcastCloseStatsWindow()
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)20);

            outmsg.Write((byte)roomState);
            outmsg.Write(period);
            outmsg.Write(homeSide);

            List<NetConnection> recipients = new List<NetConnection>();

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].connection != null && IsPlayerOnline(i, j))
                        recipients.Add(users[i, j].connection);

            lock (spectatorData2)
            {
                for (int i = 0; i < spectatorData2.Count; i++)
                    if (spectatorData2[i].connection != null)
                        recipients.Add(spectatorData2[i].connection);
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);

        }

        public bool IsPlayerOnline(int tID, int pID)
        {
            if (users[tID, pID].pID > 0)
                return true;
            else
                return false;
        }

        void CalculateDynamicPositions()
        {
            dynamicPosDelayer--;
            if (dynamicPosDelayer > 0) return;
            dynamicPosDelayer = 50;

            int pID;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j].dynamicPosFound = false;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    PositionAndSide p = PositionHandler.GetDynamicPosition(j, maxPlayers);

                    pID = GetNearestPlayerToDynamicPos(i, PositionHandler.GetDynamicCoords(p, i, homeSide));

                    users[i, pID].dynamicPosFound = true;
                    users[i, pID].AIDynamicpos = p;
                }
        }

        int GetNearestPlayerToDynamicPos(int tID, double[] targetPos)
        {
            int pID = -1;
            double nearest = 1000;

            for (int j = 0; j < maxPlayers; j++)
            {
                if (users[tID, j].dynamicPosFound) continue;

                double d = F.Distance(users[tID, j].coords[0], users[tID, j].coords[1], targetPos[0], targetPos[1]);
                if (d < nearest)
                {
                    nearest = d;
                    pID = j;
                }
            }

            return pID;
        }

        void CalculatePressingPlayer()
        {
            if (throwInTID > -1) return;
            if (cornerTID > -1) return;
            if (freekickTID > -1) return;
            if (goalkickTID > -1) return;
            if (keepers[0].ballInPossesDelay > 0 || keepers[1].ballInPossesDelay > 0) return;

            ResetPressing();

            int pID = -1;
            double nearest = 1000;

            //set nearest player to press
            for (int i = 0; i < 2; i++)
            {
                if (currentControllerTID == i) continue;
                nearest = 1000;

                for (int j = 0; j < maxPlayers; j++)
                {
                    if (users[i, j].slideDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;

                    if (users[i, j].distToBall < nearest)
                    {
                        nearest = users[i, j].distToBall;
                        pID = j;
                    }
                }
                if (pID > -1)
                    users[i, pID].AIDoPress = true;
            }


            //if attacker gets too close to goal, lets set extra bot to pressing (which is under ball, like DC example)

            int defendingTID;
            pID = -1;
            if (homeSide == 1) defendingTID = 0; else defendingTID = 1;

            //action gets too close to upper goal
            if (lastControllersTID[0] != defendingTID)
                if (F.Distance(ball.coords[0], ball.coords[1], 0, Field.GOALLINE_P) < 4.7)
                {
                    nearest = 1000;

                    for (int j = 0; j < maxPlayers; j++)
                    {
                        if (users[defendingTID, j].slideDelay > 0) continue;
                        if (users[defendingTID, j].fallDelay > 0) continue;
                        if (users[defendingTID, j].coords[1] < ball.coords[1]) continue; //bot is behind ball, so skip him

                        if (users[defendingTID, j].distToBall < nearest)
                        {
                            nearest = users[defendingTID, j].distToBall;
                            pID = j;
                        }
                    }
                    if (pID > -1)
                        users[defendingTID, pID].AIDoPress = true;
                }

            pID = -1;
            if (homeSide == 1) defendingTID = 1; else defendingTID = 0;

            //action gets too close to lower goal
            if (lastControllersTID[0] != defendingTID)
                if (F.Distance(ball.coords[0], ball.coords[1], 0, Field.GOALLINE_N) < 4.7)
                {
                    nearest = 1000;

                    for (int j = 0; j < maxPlayers; j++)
                    {
                        if (users[defendingTID, j].slideDelay > 0) continue;
                        if (users[defendingTID, j].fallDelay > 0) continue;
                        if (users[defendingTID, j].coords[1] > ball.coords[1]) continue; //bot is behind ball, so skip him

                        if (users[defendingTID, j].distToBall < nearest)
                        {
                            nearest = users[defendingTID, j].distToBall;
                            pID = j;
                        }
                    }
                    if (pID > -1)
                        users[defendingTID, pID].AIDoPress = true;
                }




        }

        void AIStartSlide()
        {

            if (throwInTID > -1) return;
            if (cornerTID > -1) return;
            if (freekickTID > -1) return;
            if (goalkickTID > -1) return;

            int oppTID;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (!users[i, j].AIDoPress) continue;
                    if (IsPlayerOnline(i, j)) continue;
                    if (users[i, j].slideDelay > 0) continue;
                    if (users[i, j].fallDelay > 0) continue;
                    if (users[i, j].automoveFromFreekickArea) continue;
                    if (keepers[0].ballInPossesDelay > 0 || keepers[1].ballInPossesDelay > 0) continue;

                    if (i == 0) oppTID = 1; else oppTID = 0;

                    //check, if opponent is controlling ball
                    if (IsSomeTeamMemberControllingBall(oppTID))
                    {
                        //randomly start slide, if ball near enough opponent
                        if (users[i, j].distToBall < 0.4)
                            if (F.rand.Next(400) < IsDangerZone(oppTID))
                            {
                                users[i, j].direction = F.Angle(users[i, j].coords[0], users[i, j].coords[1], ball.coords[0], ball.coords[1]);
                                users[i, j].slideDelay = slideDuration;
                                users[i, j].speed += users[i, j].GetSlideTackleSpeed();
                            }
                    }

                    //jos vihu alle 0.1 pallosta ja ite kauempana, kuin vihu
                }
        }

        int IsDangerZone(int oppTID)
        {
            if (roomType == RoomType.Public) return 10;

            if ((homeSide == 1 && oppTID == 1) || (homeSide == 2 && oppTID == 0))
                if (F.Distance(ball.coords[0], ball.coords[1], 0, Field.GOALLINE_P) < 4.7)
                    return 100;

            if ((homeSide == 1 && oppTID == 0) || (homeSide == 2 && oppTID == 1))
                if (F.Distance(ball.coords[0], ball.coords[1], 0, Field.GOALLINE_N) < 4.7)
                    return 100;

            return 10;
        }

        bool IsSomeTeamMemberControllingBall(int tID)
        {
            for (int j = 0; j < maxPlayers; j++)
                if (users[tID, j].distToBall < 0.1) return true;

            return false;
        }

        bool IsNearestToBall(int tID, int pID)
        {
            double nearest = 1000;
            int _tID = -1;
            int _pID = -1;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (users[i, j].distToBall < nearest)
                    {
                        nearest = users[i, j].distToBall;
                        _tID = i;
                        _pID = j;
                    }
                }

            if (_tID == tID && _pID == pID)
                return true;
            else
                return false;
        }

        double DirectionToNearestTeammate(int tID, int pID, double fromX, double fromY)
        {
            double nearest = 1000;
            int _pID = -1;

            for (int j = 0; j < maxPlayers; j++)
            {
                if (j == pID) continue;
                if (users[tID, j].distToBall < nearest)
                {
                    nearest = users[tID, j].distToBall;
                    _pID = j;
                }
            }

            return F.Angle(fromX, fromY, users[tID, _pID].coords[0], users[tID, _pID].coords[1]);
        }

        int GetNearestTeamMember(int tID, int pID)
        {
            double nearest = 1000;
            int _pID = -1;

            for (int j = 0; j < maxPlayers; j++)
            {
                if (j == pID) continue;
                if (users[tID, j].distToBall < nearest)
                {
                    nearest = users[tID, j].distToBall;
                    _pID = j;
                }
            }

            return _pID;
        }

        public void InformSpectatorsAboutServerClose()
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)58);

            List<NetConnection> recipients = new List<NetConnection>();

            lock (spectatorData2)
            {
                for (int j = 0; j < spectatorData2.Count; j++)
                {
                    if (spectatorData2[j].connection == null) continue;

                    recipients.Add(spectatorData2[j].connection);

                }
            }

            if (recipients.Count > 0)
                server.SendMessage(outmsg, recipients, NetDeliveryMethod.ReliableOrdered, 3);

        }

        public void InformAboutCanceledLeagueMatch()
        {
            NetOutgoingMessage outmsg = clientToMS.CreateMessage();
            outmsg.Write((byte)64);

            outmsg.Write(fixtureID);

            clientToMS.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

    }
}
