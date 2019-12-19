using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;
using MySql.Data.MySqlClient;

namespace MasterServer
{
    class ServerForGS : BaseStuff
    {
        public NetServer server;
        NetPeerConfiguration config;
        public List<GameServer> gameServers = new List<GameServer>();
        public ClientToDS clientToDS;
        public Thread thread;

        public ServerForGS()
        {
            config = new NetPeerConfiguration("NSMobile");
            config.MaximumConnections = 10000;
            config.Port = 14243;
            config.ConnectionTimeout = 10;
            config.PingInterval = 3;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            server = new NetServer(config);
            thread = new Thread(new ThreadStart(Handler));
            server.Start();
            thread.Start();
        }

        void Handler()
        {
            NetIncomingMessage inmsg;
            byte b;

            while (true)
            {
                while ((inmsg = server.ReadMessage()) != null)
                {
                    switch (inmsg.MessageType)
                    {
                        case NetIncomingMessageType.DebugMessage:
                        case NetIncomingMessageType.ErrorMessage:
                        case NetIncomingMessageType.WarningMessage:
                        case NetIncomingMessageType.VerboseDebugMessage:
                            //Console.WriteLine(inmsg.ReadString());
                            break;


                        //*************************************************************************

                        case NetIncomingMessageType.ConnectionApproval:
                            inmsg.SenderConnection.Approve();

                            AddText("gameserver approved");

                            lock (gameServers)
                            {
                                gameServers.Add(new GameServer(inmsg.SenderConnection));
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)inmsg.ReadByte();

                            if (status == NetConnectionStatus.Disconnected)
                            {
                                AddText("gameserver Disconnected");

                                lock (gameServers)
                                    gameServers.RemoveAll(p => p.netConnection == inmsg.SenderConnection);

                                clientToDS.InformAboutGSDisconnect(inmsg.SenderEndPoint.Address.ToString());
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.Data:
                            if (gameServers.Find(p => p.netConnection == inmsg.SenderConnection) == null) break;
                            if (inmsg.LengthBytes < 1) break;

                            b = inmsg.ReadByte();

                            //gameserver sends data of rooms to masterserver (which will re-send it to DS)
                            if (b == 8)
                                Packet8(inmsg);

                            //gameserver wants to verify user and get data for player
                            if (b == 9)
                                Packet9(inmsg);

                            //chat message
                            if (b == 45)
                                Packet45(inmsg);

                            //gameserver sends challenge result to masterserver
                            if (b == 48)
                                Packet48(inmsg);

                            //gameserver informs, that it created match
                            if (b == 52)
                                Packet52(inmsg);

                            //training goal data
                            if (b == 54)
                                Packet54(inmsg);

                            //test message
                            if (b == 59)
                                Packet59(inmsg);

                            //GS informs about canceled league match
                            if (b == 64)
                                Packet64(inmsg);

                            //3 own goals scored in challenge, lets inform about ban
                            if (b == 87)
                                Packet87(inmsg);

                            //challenge/league match gets cancelled during match
                            if (b == 92)
                                Packet92(inmsg);

                            break;

                        //*************************************************************************

                        default:
                            Console.WriteLine("Unhandled type: " + inmsg.MessageType + " " + inmsg.LengthBytes + " bytes " + inmsg.DeliveryMethod + "|" + inmsg.SequenceChannel);
                            break;
                    }
                    server.Recycle(inmsg);
                }

                Thread.Sleep(1);
            }
        }

        //gameserver sends data of rooms to masterserver (which will re-send it to DS)
        void Packet8(NetIncomingMessage inmsg)
        {
            byte location;
            string natCode; //FI
            string servername;
            int roomCount;
            int plrCount; //NOTE!!! this value contains player count (also spectators) in whole server (so it maybe in example 165)
            int spectatorCount;

            try { servername = inmsg.ReadString(); }
            catch { return; }
            try { roomCount = inmsg.ReadInt32(); }
            catch { return; }

            if (roomCount < 1) return;

            string gameserverIP = inmsg.SenderEndPoint.Address.ToString();

            //timeout reset
            for (int i = 0; i < gameServers.Count; i++)
                if (gameServers[i].netConnection.RemoteEndPoint.Address.ToString() == gameserverIP)
                    gameServers[i].timeout = 0;

            int[] uniqueID = new int[roomCount];
            byte[] maxPlayers = new byte[roomCount];
            byte[] roomType = new byte[roomCount];
            byte[] time = new byte[roomCount];
            byte[] roomState = new byte[roomCount];
            bool[] botsEnabled = new bool[roomCount];
            bool[] officiallyStarted = new bool[roomCount];
            int[,] _tID = new int[2, roomCount];
            string[,] _teamnames = new string[2, roomCount];
            byte[,] _score = new byte[2, roomCount];
            int[,,] _pID = new int[2, 10, roomCount];
            int[,,] _plrTID = new int[2, 10, roomCount];
            List<List<int>> _spectatorPID = new List<List<int>>();
            List<List<int>> _spectatorTID = new List<List<int>>();

            for (int i = 0; i < roomCount; i++)
            {
                try { uniqueID[i] = inmsg.ReadInt32(); }
                catch { return; }
                try { maxPlayers[i] = inmsg.ReadByte(); }
                catch { return; }
                try { roomType[i] = inmsg.ReadByte(); }
                catch { return; }
                try { time[i] = inmsg.ReadByte(); }
                catch { return; }
                try { roomState[i] = inmsg.ReadByte(); }
                catch { return; }
                try { botsEnabled[i] = inmsg.ReadBoolean(); }
                catch { return; }
                try { officiallyStarted[i] = inmsg.ReadBoolean(); }
                catch { return; }

                for (int j = 0; j < 2; j++)
                {
                    try { _tID[j, i] = inmsg.ReadInt32(); }
                    catch { return; }
                    try { _teamnames[j, i] = inmsg.ReadString(); }
                    catch { return; }
                    try { _score[j, i] = inmsg.ReadByte(); }
                    catch { return; }
                }

                for (int j = 0; j < 2; j++)
                {
                    for (int k = 0; k < maxPlayers[i]; k++)
                    {
                        try { _pID[j, k, i] = inmsg.ReadInt32(); }
                        catch { return; }
                        try { _plrTID[j, k, i] = inmsg.ReadInt32(); }
                        catch { return; }
                    }
                }

                try { spectatorCount = inmsg.ReadInt32(); }
                catch { return; }

                _spectatorPID.Add(new List<int>());
                _spectatorTID.Add(new List<int>());

                for (int j = 0; j < spectatorCount; j++)
                {
                    try { _spectatorPID[i].Add(inmsg.ReadInt32()); }
                    catch { return; }
                    try { _spectatorTID[i].Add(inmsg.ReadInt32()); }
                    catch { return; }
                }
            }

            try { plrCount = inmsg.ReadInt32(); }
            catch { return; }

            //***************************

            if (!Form1.isLocal)
            {
                MySqlConnection mySqlConnection = OpenSQL();
                IPData ipData = new IPData(gameserverIP, mySqlConnection);
                mySqlConnection.Close();

                natCode = ipData.nameShort;
                location = GetLocationForGS(ipData.nameShort);
                if (location == 255)
                {
                    AddText("Non valid gameserver location. IP: " + gameserverIP);
                    return;
                }
            }
            else
            {
                location = 0; //local server is european
                natCode = "FI";
            }

            //*******************************************

            //write data

            NetOutgoingMessage outmsg = clientToDS.client.CreateMessage();
            outmsg.Write((byte)8);

            outmsg.Write(location);
            outmsg.Write(natCode);
            outmsg.Write(servername);
            outmsg.Write(roomCount); //total room count
            outmsg.Write(plrCount);
            outmsg.Write(gameserverIP);

            for (int i = 0; i < roomCount; i++)
            {
                outmsg.Write(uniqueID[i]);
                outmsg.Write(maxPlayers[i]);
                outmsg.Write(roomType[i]);
                outmsg.Write(time[i]);
                outmsg.Write(roomState[i]);
                outmsg.Write(botsEnabled[i]);
                outmsg.Write(officiallyStarted[i]);

                for (int j = 0; j < 2; j++)
                {
                    outmsg.Write(_tID[j, i]);
                    outmsg.Write(_teamnames[j, i]);
                    outmsg.Write(_score[j, i]);
                }

                for (int j = 0; j < 2; j++)
                    for (int k = 0; k < maxPlayers[i]; k++)
                    {
                        outmsg.Write(_pID[j, k, i]);
                        outmsg.Write(_plrTID[j, k, i]);
                    }

                outmsg.Write(_spectatorPID[i].Count);

                for (int j = 0; j < _spectatorPID[i].Count; j++)
                {
                    outmsg.Write(_spectatorPID[i][j]);
                    outmsg.Write(_spectatorTID[i][j]);
                }

            }


            clientToDS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);

        }

        //gameserver wants to verify user and get data for player
        void Packet9(NetIncomingMessage inmsg)
        {
            int pID;
            int selectedServerUniqueID;
            int uniqueID;
            long GSUniqueIdentifier;
            bool isSpectator;

            try { pID = inmsg.ReadInt32(); }
            catch { return; }
            try { selectedServerUniqueID = inmsg.ReadInt32(); }
            catch { return; }
            try { uniqueID = inmsg.ReadInt32(); }
            catch { return; }
            try { GSUniqueIdentifier = inmsg.ReadInt64(); }
            catch { return; }
            try { isSpectator = inmsg.ReadBoolean(); }
            catch { return; }

            //**************

            int _pID = 0;
            long _uniqueID = 0;
            int tID = 0;
            int lockedTID = 0;
            string nation = "";
            byte admas = 0;
            byte body = 0;
            byte skin = 0;
            byte hair = 0;
            byte number = 0;
            string username = "";
            string[] shoeStr = new string[3];
            byte[] shoe = new byte[3];
            bool vip;

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + pID, mySqlConnection);

            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                _pID = dataReader.GetInt32("id");
                _uniqueID = dataReader.GetInt64("uniqueID");
                username = dataReader.GetString("username");
                tID = dataReader.GetInt32("teamID");
                lockedTID = dataReader.GetInt32("lockedTID");
                admas = dataReader.GetByte("admas");
                nation = dataReader.GetString("nation");
                body = dataReader.GetByte("body");
                skin = dataReader.GetByte("skin");
                hair = dataReader.GetByte("hair");
                number = dataReader.GetByte("number");
                shoeStr = dataReader.GetString("shoe").Split(',');
            }
            dataReader.Close();

            if (_pID == 0 || _uniqueID != uniqueID)
            {
                mySqlConnection.Close();
                return;
            }

            //explode shoe
            for (int i = 0; i < 3; i++)
                byte.TryParse(shoeStr[i], out shoe[i]);

            vip = IsVip(_pID, mySqlConnection);

            //if users vip have expired, lets reset his attribs
            if (!vip)
            {
                body = 1;
                skin = 0;
                hair = 1;
                number = 0;
                shoe[0] = 0;
                shoe[1] = 0;
                shoe[2] = 0;
            }

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)10);

            outmsg.Write(_pID);
            outmsg.Write(tID);
            outmsg.Write(lockedTID);
            outmsg.Write(isSpectator);
            outmsg.Write(nation);
            outmsg.Write(selectedServerUniqueID);
            outmsg.Write(GSUniqueIdentifier);
            outmsg.Write(admas);
            outmsg.Write(body);
            outmsg.Write(skin);
            outmsg.Write(hair);
            outmsg.Write(number);
            outmsg.Write(username);
            outmsg.Write(vip);
            for (int i = 0; i < 3; i++)
                outmsg.Write(shoe[i]);

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);

        }

        //chat message
        void Packet45(NetIncomingMessage inmsg)
        {
            byte receiverType;  //0=user, 1=team
            string receiver;
            string message;
            int pID;
            int tID;

            try { receiverType = inmsg.ReadByte(); }
            catch { return; }
            try { receiver = inmsg.ReadString(); }
            catch { return; }
            try { message = inmsg.ReadString(); }
            catch { return; }
            try { pID = inmsg.ReadInt32(); }
            catch { return; }
            try { tID = inmsg.ReadInt32(); }
            catch { return; }

            if (receiverType > 1) return;
            if (receiver == "") return;
            if (message == "") return;
            if (pID == 0) return;

            NetOutgoingMessage outmsg = clientToDS.client.CreateMessage();
            outmsg.Write((byte)45);

            outmsg.Write(receiverType);
            outmsg.Write(receiver);
            outmsg.Write(message);
            outmsg.Write(pID);
            outmsg.Write(tID);

            clientToDS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        //gameserver sends challenge result to masterserver
        void Packet48(NetIncomingMessage inmsg)
        {
            int fixtureID;

            try { fixtureID = inmsg.ReadInt32(); }
            catch { return; }

            #region read basic data
            int[] tID = new int[2];
            byte[] score = new byte[2];
            byte[] goalKicks = new byte[2];
            byte[] corners = new byte[2];
            byte[] throwIns = new byte[2];
            byte[] offsides = new byte[2];
            byte[] shotsTotal = new byte[2];
            byte[] shotsOnGoal = new byte[2];
            byte[] possession = new byte[2];

            for (int i = 0; i < 2; i++)
            {
                try { tID[i] = inmsg.ReadInt32(); }
                catch { return; }
                try { score[i] = inmsg.ReadByte(); }
                catch { return; }
                try { goalKicks[i] = inmsg.ReadByte(); }
                catch { return; }
                try { corners[i] = inmsg.ReadByte(); }
                catch { return; }
                try { throwIns[i] = inmsg.ReadByte(); }
                catch { return; }
                try { offsides[i] = inmsg.ReadByte(); }
                catch { return; }
                try { shotsTotal[i] = inmsg.ReadByte(); }
                catch { return; }
                try { shotsOnGoal[i] = inmsg.ReadByte(); }
                catch { return; }
                try { possession[i] = inmsg.ReadByte(); }
                catch { return; }
            }
            #endregion

            #region read player data
            int _pID;
            byte _tID;   //contain 0 or 1 (home/away)
            byte _timePlayed;
            byte _shotsTotal;
            byte _shotsOnTarget;
            byte _offsides;
            int _posUP;
            int _posDown;
            int _posLeft;
            int _posRight;
            int _teamGoals;

            List<ChallengePlayerData> challengePlayerData = new List<ChallengePlayerData>();
            int plrCount;

            try { plrCount = inmsg.ReadInt32(); }
            catch { return; }

            for (int i = 0; i < plrCount; i++)
            {
                try { _tID = inmsg.ReadByte(); }
                catch { return; }
                try { _pID = inmsg.ReadInt32(); }
                catch { return; }
                try { _timePlayed = inmsg.ReadByte(); }
                catch { return; }
                try { _shotsTotal = inmsg.ReadByte(); }
                catch { return; }
                try { _shotsOnTarget = inmsg.ReadByte(); }
                catch { return; }
                try { _offsides = inmsg.ReadByte(); }
                catch { return; }
                try { _posUP = inmsg.ReadInt32(); }
                catch { return; }
                try { _posDown = inmsg.ReadInt32(); }
                catch { return; }
                try { _posLeft = inmsg.ReadInt32(); }
                catch { return; }
                try { _posRight = inmsg.ReadInt32(); }
                catch { return; }
                try { _teamGoals = inmsg.ReadInt32(); }
                catch { return; }

                challengePlayerData.Add(new ChallengePlayerData(_tID, _pID, _timePlayed, _shotsTotal, _shotsOnTarget, _offsides, _posUP, _posDown, _posLeft, _posRight, _teamGoals));
            }
            #endregion

            #region read goal data
            int _scorer;
            int _assister;
            byte _goalTime;
            byte _teamScored; //contain 0 or 1 (home/away)
            bool _bothTeamsHave5Players;

            List<GoalData> goalData = new List<GoalData>();
            int goalCount;

            try { goalCount = inmsg.ReadInt32(); }
            catch { return; }

            for (int i = 0; i < goalCount; i++)
            {
                try { _scorer = inmsg.ReadInt32(); }
                catch { return; }
                try { _assister = inmsg.ReadInt32(); }
                catch { return; }
                try { _goalTime = inmsg.ReadByte(); }
                catch { return; }
                try { _teamScored = inmsg.ReadByte(); }
                catch { return; }
                try { _bothTeamsHave5Players = inmsg.ReadBoolean(); }
                catch { return; }

                goalData.Add(new GoalData(_scorer, _assister, _goalTime, _teamScored, _bothTeamsHave5Players));
            }
            #endregion

            //data readed

            #region if both team have 5 or more HUMAN players, apps will count

            int[] teamPlrCounts = new int[2];
            byte playerAppsInc = 0;

            for (int i = 0; i < challengePlayerData.Count; i++)
                teamPlrCounts[challengePlayerData[i].tID]++;

            if (teamPlrCounts[0] >= 5 && teamPlrCounts[1] >= 5)
                playerAppsInc = 1;

            #endregion

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            #region get player data (also verify user, that username is not empty)

            for (int i = 0; i < challengePlayerData.Count; i++)
            {
                cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + challengePlayerData[i].pID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    challengePlayerData[i].username = dataReader.GetString("username");
                    challengePlayerData[i].careerGoals = dataReader.GetInt32("careerGoals");
                    challengePlayerData[i].careerAsts = dataReader.GetInt32("careerAsts");
                    challengePlayerData[i].careerTeamGoals = dataReader.GetInt32("careerTeamGoals");
                    challengePlayerData[i].posUP = dataReader.GetInt32("posUP");
                    challengePlayerData[i].posDown = dataReader.GetInt32("posDown");
                    challengePlayerData[i].posLeft = dataReader.GetInt32("posLeft");
                    challengePlayerData[i].posRight = dataReader.GetInt32("posRight");
                }
                dataReader.Close();

                //verify, that user exists
                if (challengePlayerData[i].username == "")
                {
                    mySqlConnection.Close();
                    return;
                }
            }

            #endregion

            #region get players current career data

            for (int i = 0; i < challengePlayerData.Count; i++)
            {
                cmd = new MySqlCommand("SELECT * FROM careers WHERE pID=" + challengePlayerData[i].pID + " ORDER BY joined DESC LIMIT 1", mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    challengePlayerData[i].careerID = dataReader.GetInt32("id");
                    challengePlayerData[i].goals = dataReader.GetInt32("goals");
                    challengePlayerData[i].asts = dataReader.GetInt32("asts");
                    challengePlayerData[i].teamGoals = dataReader.GetInt32("teamGoals");
                }
                dataReader.Close();
            }

            #endregion

            #region get team data (also verify team, that name is not empty

            string[] teamname = new string[2];
            int[] originalRanks = new int[2];
            int[] wins = new int[2];
            int[] draws = new int[2];
            int[] losses = new int[2];
            int[] highestRank = new int[2];
            int[] lowestRank = new int[2];
            int[] biggestWinMatchID = new int[2];
            int[] biggestDefeatMatchID = new int[2];
            int[] highestScoringGameMatchID = new int[2];
            int[] mostGamesWonAM = new int[2];
            int[] mostGamesLostAM = new int[2];
            int[] mostGamesNotLoseAM = new int[2];
            int[] mostGamesNotWinAM = new int[2];
            int[] mostGamesWonATR = new int[2];
            int[] mostGamesLostATR = new int[2];
            int[] mostGamesNotLoseATR = new int[2];
            int[] mostGamesNotWinATR = new int[2];

            for (int i = 0; i < 2; i++)
            {
                cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + tID[i], mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    teamname[i] = dataReader.GetString("name");
                    originalRanks[i] = dataReader.GetInt32("rank");
                    wins[i] = dataReader.GetInt32("wins");
                    draws[i] = dataReader.GetInt32("draws");
                    losses[i] = dataReader.GetInt32("losses");
                    highestRank[i] = dataReader.GetInt32("highestRank");
                    lowestRank[i] = dataReader.GetInt32("lowestRank");
                    biggestWinMatchID[i] = dataReader.GetInt32("biggestWinMatchID");
                    biggestDefeatMatchID[i] = dataReader.GetInt32("biggestDefeatMatchID");
                    highestScoringGameMatchID[i] = dataReader.GetInt32("highestScoringGameMatchID");
                    mostGamesWonAM[i] = dataReader.GetInt32("mostGamesWonAM");
                    mostGamesLostAM[i] = dataReader.GetInt32("mostGamesLostAM");
                    mostGamesNotLoseAM[i] = dataReader.GetInt32("mostGamesNotLoseAM");
                    mostGamesNotWinAM[i] = dataReader.GetInt32("mostGamesNotWinAM");
                    mostGamesWonATR[i] = dataReader.GetInt32("mostGamesWonATR");
                    mostGamesLostATR[i] = dataReader.GetInt32("mostGamesLostATR");
                    mostGamesNotLoseATR[i] = dataReader.GetInt32("mostGamesNotLoseATR");
                    mostGamesNotWinATR[i] = dataReader.GetInt32("mostGamesNotWinATR");
                }
                dataReader.Close();

                if (teamname[i] == "")
                {
                    mySqlConnection.Close();
                    return;
                }
            }

            RankCalculation rankCalculation = new RankCalculation(originalRanks[0], originalRanks[1], score[0], score[1]);

            #endregion

            #region generate player strings

            string pIDs = "";
            string tIDs = "";
            string timePlayed = "";
            string shotsTotalStr = "";
            string shotsOnTargetStr = "";
            string offsidesStr = "";

            for (int i = 0; i < challengePlayerData.Count; i++)
            {
                pIDs += challengePlayerData[i].pID + ",";
                tIDs += challengePlayerData[i].tID + ",";
                timePlayed += challengePlayerData[i].timePlayed + ",";
                shotsTotalStr += challengePlayerData[i].shotsTotal + ",";
                shotsOnTargetStr += challengePlayerData[i].shotsOnTarget + ",";
                offsidesStr += challengePlayerData[i].offsides + ",";
            }

            pIDs = pIDs.TrimEnd(new char[] { ',' });
            tIDs = tIDs.TrimEnd(new char[] { ',' });
            timePlayed = timePlayed.TrimEnd(new char[] { ',' });
            shotsTotalStr = shotsTotalStr.TrimEnd(new char[] { ',' });
            shotsOnTargetStr = shotsOnTargetStr.TrimEnd(new char[] { ',' });
            offsidesStr = offsidesStr.TrimEnd(new char[] { ',' });

            #endregion

            #region generate goal strings

            string goals = "";
            string assists = "";
            string goalTime = "";
            string teamScored = "";

            for (int i = 0; i < goalData.Count; i++)
            {
                goals += goalData[i].scorer + ",";
                assists += goalData[i].assister + ",";
                goalTime += goalData[i].goalTime + ",";
                teamScored += goalData[i].teamScored + ",";
            }

            goals = goals.TrimEnd(new char[] { ',' });
            assists = assists.TrimEnd(new char[] { ',' });
            goalTime = goalTime.TrimEnd(new char[] { ',' });
            teamScored = teamScored.TrimEnd(new char[] { ',' });

            #endregion

            bool doSwap = false;

            int[] res = AddpossibleLeagueMatch(doSwap, fixtureID, score.ToArray(), mySqlConnection, false, false);

            #region insert match (and get just inserted matchID)

            cmd = new MySqlCommand("INSERT INTO matches SET " +
                "tID0=" + tID[0] + ", " +
                "tID1=" + tID[1] + ", " +
                "score0=" + score[0] + ", " +
                "score1=" + score[1] + ", " +
                "rankChange0=" + rankCalculation.rankChange[0] + ", " +
                "rankChange1=" + rankCalculation.rankChange[1] + ", " +
                "goalKicks0=" + goalKicks[0] + ", " +
                "goalKicks1=" + goalKicks[1] + ", " +
                "offsides0=" + offsides[0] + ", " +
                "offsides1=" + offsides[1] + ", " +
                "shotsTotal0=" + shotsTotal[0] + ", " +
                "shotsTotal1=" + shotsTotal[1] + ", " +
                "shotsOnTarget0=" + shotsOnGoal[0] + ", " +
                "shotsOnTarget1=" + shotsOnGoal[1] + ", " +
                "corners0=" + corners[0] + ", " +
                "corners1=" + corners[1] + ", " +
                "throwIns0=" + throwIns[0] + ", " +
                "throwIns1=" + throwIns[1] + ", " +
                "possession0=" + possession[0] + ", " +
                "possession1=" + possession[1] + ", " +

                "pIDs='" + pIDs + "', " +
                "tIDs='" + tIDs + "', " +
                "timePlayed='" + timePlayed + "', " +
                "shotsTotal='" + shotsTotalStr + "', " +
                "shotsOnTarget='" + shotsOnTargetStr + "', " +
                "offsides='" + offsidesStr + "', " +

                "goals='" + goals + "', " +
                "assists='" + assists + "', " +
                "goalTime='" + goalTime + "', " +
                "teamScored='" + teamScored + "', " +

                "season=" + res[0] + ", " +
                "location=" + res[1] + ", " +
                "division=" + res[2] + ", " +
                "_group=" + res[3] + ", " +

                "time=NOW()"
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            //get last inserted matchID
            int matchID = 0;
            cmd = new MySqlCommand("SELECT LAST_INSERT_ID()", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                matchID = dataReader.GetInt32("LAST_INSERT_ID()");
            }
            dataReader.Close();

            #endregion

            #region check, if team made some record match

            int[] biggestWinGoalDiff = new int[2];
            int[] biggestLoseGoalDiff = new int[2];
            int[] highestScoringGoalCount = new int[2];

            int[] matchTID = new int[2];
            byte[] matchScore = new byte[2];

            //get current records, if those exists
            for (int i = 0; i < 2; i++)
            {
                if (biggestWinMatchID[i] > -1)
                {
                    cmd = new MySqlCommand("SELECT * FROM matches WHERE id=" + biggestWinMatchID[i], mySqlConnection);
                    dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        matchTID[0] = dataReader.GetInt32("tID0");
                        matchTID[1] = dataReader.GetInt32("tID1");
                        matchScore[0] = dataReader.GetByte("score0");
                        matchScore[1] = dataReader.GetByte("score1");
                    }
                    dataReader.Close();

                    if (matchTID[0] == tID[i])
                        biggestWinGoalDiff[i] = matchScore[0] - matchScore[1];
                    else
                        biggestWinGoalDiff[i] = matchScore[1] - matchScore[0];
                }

                if (biggestDefeatMatchID[i] > -1)
                {
                    cmd = new MySqlCommand("SELECT * FROM matches WHERE id=" + biggestDefeatMatchID[i], mySqlConnection);
                    dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        matchTID[0] = dataReader.GetInt32("tID0");
                        matchTID[1] = dataReader.GetInt32("tID1");
                        matchScore[0] = dataReader.GetByte("score0");
                        matchScore[1] = dataReader.GetByte("score1");
                    }
                    dataReader.Close();

                    if (matchTID[0] == tID[i])
                        biggestLoseGoalDiff[i] = matchScore[1] - matchScore[0];
                    else
                        biggestLoseGoalDiff[i] = matchScore[0] - matchScore[1];
                }

                if (highestScoringGameMatchID[i] > -1)
                {
                    cmd = new MySqlCommand("SELECT * FROM matches WHERE id=" + highestScoringGameMatchID[i], mySqlConnection);
                    dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        matchTID[0] = dataReader.GetInt32("tID0");
                        matchTID[1] = dataReader.GetInt32("tID1");
                        matchScore[0] = dataReader.GetByte("score0");
                        matchScore[1] = dataReader.GetByte("score1");
                    }
                    dataReader.Close();

                    highestScoringGoalCount[i] = matchScore[0] + matchScore[1];
                }
            }

            //check, if current result is bigger, that previous record
            int goalDiff;
            int totalGoalCount = score[0] + score[1];

            if (score[0] > score[1])
            {
                goalDiff = score[0] - score[1];
                if (goalDiff > biggestWinGoalDiff[0]) biggestWinMatchID[0] = matchID;
                if (goalDiff > biggestLoseGoalDiff[1]) biggestDefeatMatchID[1] = matchID;
            }
            if (score[1] > score[0])
            {
                goalDiff = score[1] - score[0];
                if (goalDiff > biggestWinGoalDiff[1]) biggestWinMatchID[1] = matchID;
                if (goalDiff > biggestLoseGoalDiff[0]) biggestDefeatMatchID[0] = matchID;
            }

            if (totalGoalCount > highestScoringGoalCount[0]) highestScoringGameMatchID[0] = matchID;
            if (totalGoalCount > highestScoringGoalCount[1]) highestScoringGameMatchID[1] = matchID;

            #endregion

            #region update team data

            if (score[0] > score[1])
            {
                wins[0]++;
                losses[1]++;
                mostGamesWonAM[0]++;
                mostGamesNotLoseAM[0]++;
                mostGamesLostAM[0] = 0;
                mostGamesNotWinAM[0] = 0;
                mostGamesWonAM[1] = 0;
                mostGamesNotLoseAM[1] = 0;
                mostGamesLostAM[1]++;
                mostGamesNotWinAM[1]++;
            }
            else if (score[0] < score[1])
            {
                wins[1]++;
                losses[0]++;
                mostGamesWonAM[1]++;
                mostGamesNotLoseAM[1]++;
                mostGamesLostAM[1] = 0;
                mostGamesNotWinAM[1] = 0;
                mostGamesWonAM[0] = 0;
                mostGamesNotLoseAM[0] = 0;
                mostGamesLostAM[0]++;
                mostGamesNotWinAM[0]++;
            }
            else
            {
                draws[0]++;
                draws[1]++;
                mostGamesWonAM[0] = 0;
                mostGamesWonAM[1] = 0;
                mostGamesLostAM[0] = 0;
                mostGamesLostAM[1] = 0;
                mostGamesNotLoseAM[0]++;
                mostGamesNotLoseAM[1]++;
                mostGamesNotWinAM[0]++;
                mostGamesNotWinAM[1]++;
            }

            //check, if teams made record
            for (int i = 0; i < 2; i++)
            {
                if (mostGamesWonAM[i] > mostGamesWonATR[i]) mostGamesWonATR[i] = mostGamesWonAM[i];
                if (mostGamesLostAM[i] > mostGamesLostATR[i]) mostGamesLostATR[i] = mostGamesLostAM[i];
                if (mostGamesNotLoseAM[i] > mostGamesNotLoseATR[i]) mostGamesNotLoseATR[i] = mostGamesNotLoseAM[i];
                if (mostGamesNotWinAM[i] > mostGamesNotWinATR[i]) mostGamesNotWinATR[i] = mostGamesNotWinAM[i];
            }

            int[] newRanks = new int[2];

            //calculate new ranks and also check, if new rank is bigger or lower than record
            for (int i = 0; i < 2; i++)
            {
                newRanks[i] = originalRanks[i] + rankCalculation.rankChange[i];
                if (newRanks[i] > highestRank[i]) highestRank[i] = newRanks[i];
                if (newRanks[i] < lowestRank[i]) lowestRank[i] = newRanks[i];
            }

            for (int i = 0; i < 2; i++)
            {
                cmd = new MySqlCommand("UPDATE teams SET " +
                    "apps=apps+1, " +
                    "wins=" + wins[i] + ", " +
                    "draws=" + draws[i] + ", " +
                    "losses=" + losses[i] + ", " +
                    "highestRank=" + highestRank[i] + ", " +
                    "lowestRank=" + lowestRank[i] + ", " +
                    "mostGamesWonAM=" + mostGamesWonAM[i] + ", " +
                    "mostGamesLostAM=" + mostGamesLostAM[i] + ", " +
                    "mostGamesNotLoseAM=" + mostGamesNotLoseAM[i] + ", " +
                    "mostGamesNotWinAM=" + mostGamesNotWinAM[i] + ", " +
                    "mostGamesWonATR=" + mostGamesWonATR[i] + ", " +
                    "mostGamesLostATR=" + mostGamesLostATR[i] + ", " +
                    "mostGamesNotLoseATR=" + mostGamesNotLoseATR[i] + ", " +
                    "mostGamesNotWinATR=" + mostGamesNotWinATR[i] + ", " +
                    "biggestWinMatchID=" + biggestWinMatchID[i] + ", " +
                    "biggestDefeatMatchID=" + biggestDefeatMatchID[i] + ", " +
                    "highestScoringGameMatchID=" + highestScoringGameMatchID[i] + ", " +
                    "rank=" + newRanks[i] + " " +
                    "WHERE id=" + tID[i]
                    , mySqlConnection);
                cmd.ExecuteNonQuery();
            }
            #endregion

            #region calculate new player data to users and careers tables

            for (int i = 0; i < challengePlayerData.Count; i++)
            {
                for (int j = 0; j < goalData.Count; j++)
                {
                    if (goalData[j].teamScored != challengePlayerData[i].tID) continue;
                    if (!goalData[j].bothTeamsHave5Players) continue;

                    if (goalData[j].scorer == challengePlayerData[i].pID)
                    {
                        challengePlayerData[i].careerGoals++;
                        challengePlayerData[i].goals++;
                    }
                    if (goalData[j].assister == challengePlayerData[i].pID)
                    {
                        challengePlayerData[i].careerAsts++;
                        challengePlayerData[i].asts++;
                    }
                }

                challengePlayerData[i].posUP += challengePlayerData[i]._posUP;
                challengePlayerData[i].posDown += challengePlayerData[i]._posDown;
                challengePlayerData[i].posLeft += challengePlayerData[i]._posLeft;
                challengePlayerData[i].posRight += challengePlayerData[i]._posRight;

                challengePlayerData[i].careerTeamGoals += challengePlayerData[i]._teamGoals;
                challengePlayerData[i].teamGoals += challengePlayerData[i]._teamGoals;

            }

            #endregion

            #region update player data

            for (int i = 0; i < challengePlayerData.Count; i++)
            {
                cmd = new MySqlCommand("UPDATE users SET " +
                    "careerApps=careerApps+" + playerAppsInc + ", " +
                    "careerGoals=" + challengePlayerData[i].careerGoals + ", " +
                    "careerAsts=" + challengePlayerData[i].careerAsts + ", " +
                    "careerTeamGoals=" + challengePlayerData[i].careerTeamGoals + ", " +

                    "posUP=" + challengePlayerData[i].posUP + ", " +
                    "posDown=" + challengePlayerData[i].posDown + ", " +
                    "posLeft=" + challengePlayerData[i].posLeft + ", " +
                    "posRight=" + challengePlayerData[i].posRight + " " +

                    "WHERE id=" + challengePlayerData[i].pID
                    , mySqlConnection);
                cmd.ExecuteNonQuery();
            }

            #endregion

            #region update lockedTID for user, if league match

            if (fixtureID > 0)
                for (int i = 0; i < challengePlayerData.Count; i++)
                {
                    cmd = new MySqlCommand("UPDATE users SET " +
                        "lockedTID=" + tID[challengePlayerData[i].tID] + " " +

                        "WHERE id=" + challengePlayerData[i].pID
                        , mySqlConnection);
                    cmd.ExecuteNonQuery();
                }

            #endregion

            #region update career data

            for (int i = 0; i < challengePlayerData.Count; i++)
            {
                cmd = new MySqlCommand("UPDATE careers SET " +
                    "apps=apps+" + playerAppsInc + ", " +
                    "goals=" + challengePlayerData[i].goals + ", " +
                    "asts=" + challengePlayerData[i].asts + ", " +
                    "teamGoals=" + challengePlayerData[i].teamGoals + " " +

                    "WHERE id=" + challengePlayerData[i].careerID
                    , mySqlConnection);
                cmd.ExecuteNonQuery();
            }

            #endregion

            mySqlConnection.Close();

        }

        //gameserver informs, that it created match
        void Packet52(NetIncomingMessage inmsg)
        {
            int[] tID = new int[2];
            string[] teamnames = new string[2];

            for (int i = 0; i < 2; i++)
            {
                try { tID[i] = inmsg.ReadInt32(); }
                catch { return; }
                try { teamnames[i] = inmsg.ReadString(); }
                catch { return; }
            }

            NetOutgoingMessage outmsg = clientToDS.client.CreateMessage();
            outmsg.Write((byte)52);

            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(tID[i]);
                outmsg.Write(teamnames[i]);
            }

            clientToDS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);

        }

        //training goal data
        void Packet54(NetIncomingMessage inmsg)
        {
            byte maxPlayers;
            byte tIDScored;
            int scorer;
            int assister;
            int bannedPID;
            int[,] pID = new int[2, 10];

            try { maxPlayers = inmsg.ReadByte(); }
            catch { return; }
            try { tIDScored = inmsg.ReadByte(); }
            catch { return; }
            try { scorer = inmsg.ReadInt32(); }
            catch { return; }
            try { assister = inmsg.ReadInt32(); }
            catch { return; }
            try { bannedPID = inmsg.ReadInt32(); }
            catch { return; }

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    try { pID[i, j] = inmsg.ReadInt32(); }
                    catch { return; }

            //data readed

            int practiseGoalsInc;
            int practiseAssistsInc;
            int practiseTeamGoalsInc;

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                {
                    if (pID[i, j] == 0) continue;

                    practiseGoalsInc = 0;
                    practiseAssistsInc = 0;

                    if (scorer == pID[i, j]) practiseGoalsInc = 1;
                    if (assister == pID[i, j]) practiseAssistsInc = 1;
                    if (tIDScored == i)
                        practiseTeamGoalsInc = 1;
                    else
                        practiseTeamGoalsInc = -1;

                    cmd = new MySqlCommand("UPDATE users SET " +
                        "practiseGoals=practiseGoals+" + practiseGoalsInc + ", " +
                        "practiseAssists=practiseAssists+" + practiseAssistsInc + ", " +
                        "practiseTeamGoals=practiseTeamGoals+" + practiseTeamGoalsInc + " " +
                        "WHERE id=" + pID[i, j]
                        , mySqlConnection);
                    cmd.ExecuteNonQuery();
                }

            if (bannedPID > 0)
                BanUserBecouseOwnGoals(mySqlConnection, bannedPID, inmsg.SenderConnection);

            mySqlConnection.Close();
        }

        //test message
        void Packet59(NetIncomingMessage inmsg)
        {
            AddText("Packet59");
        }

        //GS informs about canceled league match
        void Packet64(NetIncomingMessage inmsg)
        {
            int fixtureID;
            byte[] score = new byte[2];

            try { fixtureID = inmsg.ReadInt32(); }
            catch { return; }

            MySqlConnection mySqlConnection = OpenSQL();

            AddpossibleLeagueMatch(false, fixtureID, score.ToArray(), mySqlConnection, true, false);

            mySqlConnection.Close();
        }

        //3 own goals scored in challenge, lets inform about ban
        void Packet87(NetIncomingMessage inmsg)
        {
            int bannedPID;

            try { bannedPID = inmsg.ReadInt32(); }
            catch { return; }

            MySqlConnection mySqlConnection = OpenSQL();

            if (bannedPID > 0)
                BanUserBecouseOwnGoals(mySqlConnection, bannedPID, inmsg.SenderConnection);

            mySqlConnection.Close();
        }

        //challenge/league match gets cancelled during match
        void Packet92(NetIncomingMessage inmsg)
        {
            int fixtureID;

            try { fixtureID = inmsg.ReadInt32(); }
            catch { return; }

            #region read basic data
            int[] tID = new int[2];
            byte[] score = new byte[2];

            for (int i = 0; i < 2; i++)
            {
                try { tID[i] = inmsg.ReadInt32(); }
                catch { return; }
                try { score[i] = inmsg.ReadByte(); }
                catch { return; }
            }
            #endregion

            #region read player data
            int _pID;
            byte _tID;   //contain 0 or 1 (home/away)

            List<ChallengePlayerData> challengePlayerData = new List<ChallengePlayerData>();
            int plrCount;

            try { plrCount = inmsg.ReadInt32(); }
            catch { return; }

            for (int i = 0; i < plrCount; i++)
            {
                try { _tID = inmsg.ReadByte(); }
                catch { return; }
                try { _pID = inmsg.ReadInt32(); }
                catch { return; }

                challengePlayerData.Add(new ChallengePlayerData(_tID, _pID, 0, 0, 0, 0, 0, 0, 0, 0, 0));
            }
            #endregion


            //data readed

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            #region get team data (also verify team, that name is not empty

            string[] teamname = new string[2];
            int[] originalRanks = new int[2];
            int[] wins = new int[2];
            int[] draws = new int[2];
            int[] losses = new int[2];
            int[] highestRank = new int[2];
            int[] lowestRank = new int[2];
            int[] biggestWinMatchID = new int[2];
            int[] biggestDefeatMatchID = new int[2];
            int[] highestScoringGameMatchID = new int[2];
            int[] mostGamesWonAM = new int[2];
            int[] mostGamesLostAM = new int[2];
            int[] mostGamesNotLoseAM = new int[2];
            int[] mostGamesNotWinAM = new int[2];
            int[] mostGamesWonATR = new int[2];
            int[] mostGamesLostATR = new int[2];
            int[] mostGamesNotLoseATR = new int[2];
            int[] mostGamesNotWinATR = new int[2];

            for (int i = 0; i < 2; i++)
            {
                cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + tID[i], mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    teamname[i] = dataReader.GetString("name");
                    originalRanks[i] = dataReader.GetInt32("rank");
                    wins[i] = dataReader.GetInt32("wins");
                    draws[i] = dataReader.GetInt32("draws");
                    losses[i] = dataReader.GetInt32("losses");
                    highestRank[i] = dataReader.GetInt32("highestRank");
                    lowestRank[i] = dataReader.GetInt32("lowestRank");
                    biggestWinMatchID[i] = dataReader.GetInt32("biggestWinMatchID");
                    biggestDefeatMatchID[i] = dataReader.GetInt32("biggestDefeatMatchID");
                    highestScoringGameMatchID[i] = dataReader.GetInt32("highestScoringGameMatchID");
                    mostGamesWonAM[i] = dataReader.GetInt32("mostGamesWonAM");
                    mostGamesLostAM[i] = dataReader.GetInt32("mostGamesLostAM");
                    mostGamesNotLoseAM[i] = dataReader.GetInt32("mostGamesNotLoseAM");
                    mostGamesNotWinAM[i] = dataReader.GetInt32("mostGamesNotWinAM");
                    mostGamesWonATR[i] = dataReader.GetInt32("mostGamesWonATR");
                    mostGamesLostATR[i] = dataReader.GetInt32("mostGamesLostATR");
                    mostGamesNotLoseATR[i] = dataReader.GetInt32("mostGamesNotLoseATR");
                    mostGamesNotWinATR[i] = dataReader.GetInt32("mostGamesNotWinATR");
                }
                dataReader.Close();

                if (teamname[i] == "")
                {
                    mySqlConnection.Close();
                    return;
                }
            }

            RankCalculation rankCalculation = new RankCalculation(originalRanks[0], originalRanks[1], score[0], score[1]);

            if (score[0] > score[1])
            {
                //rankCalculation.rankChange[0] = 510;
                rankCalculation.rankChange[1] = -510;
            }
            if (score[1] > score[0])
            {
                rankCalculation.rankChange[0] = -510;
                //rankCalculation.rankChange[1] = 510;
            }

            #endregion

            bool doSwap = false;

            //match have been challenged, not autostarted, so lets check, if its valid league game
            //if (fixtureID == 0) fixtureID = GetPossibleFixtureID(tID.ToArray(), mySqlConnection, ref doSwap);

            #region update team data

            if (score[0] > score[1])
            {
                wins[0]++;
                losses[1]++;
                mostGamesWonAM[0]++;
                mostGamesNotLoseAM[0]++;
                mostGamesLostAM[0] = 0;
                mostGamesNotWinAM[0] = 0;
                mostGamesWonAM[1] = 0;
                mostGamesNotLoseAM[1] = 0;
                mostGamesLostAM[1]++;
                mostGamesNotWinAM[1]++;
            }
            else if (score[0] < score[1])
            {
                wins[1]++;
                losses[0]++;
                mostGamesWonAM[1]++;
                mostGamesNotLoseAM[1]++;
                mostGamesLostAM[1] = 0;
                mostGamesNotWinAM[1] = 0;
                mostGamesWonAM[0] = 0;
                mostGamesNotLoseAM[0] = 0;
                mostGamesLostAM[0]++;
                mostGamesNotWinAM[0]++;
            }
            else
            {
                draws[0]++;
                draws[1]++;
                mostGamesWonAM[0] = 0;
                mostGamesWonAM[1] = 0;
                mostGamesLostAM[0] = 0;
                mostGamesLostAM[1] = 0;
                mostGamesNotLoseAM[0]++;
                mostGamesNotLoseAM[1]++;
                mostGamesNotWinAM[0]++;
                mostGamesNotWinAM[1]++;
            }

            //check, if teams made record
            for (int i = 0; i < 2; i++)
            {
                if (mostGamesWonAM[i] > mostGamesWonATR[i]) mostGamesWonATR[i] = mostGamesWonAM[i];
                if (mostGamesLostAM[i] > mostGamesLostATR[i]) mostGamesLostATR[i] = mostGamesLostAM[i];
                if (mostGamesNotLoseAM[i] > mostGamesNotLoseATR[i]) mostGamesNotLoseATR[i] = mostGamesNotLoseAM[i];
                if (mostGamesNotWinAM[i] > mostGamesNotWinATR[i]) mostGamesNotWinATR[i] = mostGamesNotWinAM[i];
            }

            int[] newRanks = new int[2];

            //calculate new ranks and also check, if new rank is bigger or lower than record
            for (int i = 0; i < 2; i++)
            {
                newRanks[i] = originalRanks[i] + rankCalculation.rankChange[i];
                if (newRanks[i] > highestRank[i]) highestRank[i] = newRanks[i];
                if (newRanks[i] < lowestRank[i]) lowestRank[i] = newRanks[i];
            }

            for (int i = 0; i < 2; i++)
            {
                cmd = new MySqlCommand("UPDATE teams SET " +
                    "apps=apps+1, " +
                    "wins=" + wins[i] + ", " +
                    "draws=" + draws[i] + ", " +
                    "losses=" + losses[i] + ", " +
                    "highestRank=" + highestRank[i] + ", " +
                    "lowestRank=" + lowestRank[i] + ", " +
                    "mostGamesWonAM=" + mostGamesWonAM[i] + ", " +
                    "mostGamesLostAM=" + mostGamesLostAM[i] + ", " +
                    "mostGamesNotLoseAM=" + mostGamesNotLoseAM[i] + ", " +
                    "mostGamesNotWinAM=" + mostGamesNotWinAM[i] + ", " +
                    "mostGamesWonATR=" + mostGamesWonATR[i] + ", " +
                    "mostGamesLostATR=" + mostGamesLostATR[i] + ", " +
                    "mostGamesNotLoseATR=" + mostGamesNotLoseATR[i] + ", " +
                    "mostGamesNotWinATR=" + mostGamesNotWinATR[i] + ", " +
                    "biggestWinMatchID=" + biggestWinMatchID[i] + ", " +
                    "biggestDefeatMatchID=" + biggestDefeatMatchID[i] + ", " +
                    "highestScoringGameMatchID=" + highestScoringGameMatchID[i] + ", " +
                    "rank=" + newRanks[i] + " " +
                    "WHERE id=" + tID[i]
                    , mySqlConnection);
                cmd.ExecuteNonQuery();
            }
            #endregion

            int[] res = AddpossibleLeagueMatch(false, fixtureID, score.ToArray(), mySqlConnection, true, true);

            #region insert match (no details)
            cmd = new MySqlCommand("INSERT INTO matches SET " +
                "tID0=" + tID[0] + ", " +
                "tID1=" + tID[1] + ", " +
                "score0=" + score[0] + ", " +
                "score1=" + score[1] + ", " +
                "rankChange0=" + rankCalculation.rankChange[0] + ", " +
                "rankChange1=" + rankCalculation.rankChange[1] + ", " +
                "detailsAvailable=0," +
                "season=" + res[0] + ", " +
                "location=" + res[1] + ", " +
                "division=" + res[2] + ", " +
                "_group=" + res[3] + ", " +

                "time=NOW()"
                , mySqlConnection);
            cmd.ExecuteNonQuery();
            #endregion

            #region update lockedTID for user, if league match

            if (fixtureID > 0)
                for (int i = 0; i < challengePlayerData.Count; i++)
                {
                    cmd = new MySqlCommand("UPDATE users SET " +
                        "lockedTID=" + tID[challengePlayerData[i].tID] + " " +

                        "WHERE id=" + challengePlayerData[i].pID
                        , mySqlConnection);
                    cmd.ExecuteNonQuery();
                }

            #endregion

            mySqlConnection.Close();
        }

        void BanUserBecouseOwnGoals(MySqlConnection mySqlConnection, int bannedPID, NetConnection conn)
        {
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            byte banCount = 0;
            ulong steamID = 0;
            DateTime bannedTime = TimeNow(mySqlConnection);

            cmd = new MySqlCommand("SELECT banCount,steamID FROM users WHERE id=" + bannedPID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                banCount = dataReader.GetByte("banCount");
                steamID = dataReader.GetUInt64("steamID");
            }
            dataReader.Close();

            banCount++;

            if (banCount == 1 || banCount == 2)
            {
                if (banCount == 1) bannedTime = bannedTime.AddHours(1);
                if (banCount == 2) bannedTime = bannedTime.AddDays(1);

                if (steamID == 0) //banned user is mobile player
                {
                    cmd = new MySqlCommand("UPDATE users SET " +
                        "banCount=" + banCount + "," +
                        "bannedTime='" + bannedTime.ToString("yyyy-MM-dd HH:mm:ss") + "' " +
                        "WHERE id=" + bannedPID
                        , mySqlConnection);
                    cmd.ExecuteNonQuery();
                }
                else //banned user is steam player
                {
                    cmd = new MySqlCommand("UPDATE users SET " +
                        "banCount=" + banCount + "," +
                        "bannedTime='" + bannedTime.ToString("yyyy-MM-dd HH:mm:ss") + "' " +
                        "WHERE steamID=" + steamID
                        , mySqlConnection);
                    cmd.ExecuteNonQuery();
                }
            }

            if (banCount == 3)
            {
                if (steamID == 0) //banned user is mobile player
                {
                    cmd = new MySqlCommand("UPDATE users SET " +
                        "banCount=" + banCount + "," +
                        "permaBanned=1 " +
                        "WHERE id=" + bannedPID
                        , mySqlConnection);
                    cmd.ExecuteNonQuery();
                }
                else //banned user is steam player
                {
                    cmd = new MySqlCommand("UPDATE users SET " +
                        "banCount=" + banCount + "," +
                        "permaBanned=1 " +
                        "WHERE steamID=" + steamID
                        , mySqlConnection);
                    cmd.ExecuteNonQuery();
                }
            }

            //inform GS about user, which needs to be kicked out from server
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)87);
            outmsg.Write(bannedPID);
            server.SendMessage(outmsg, conn, NetDeliveryMethod.ReliableOrdered, 0);

        }

        byte GetLocationForGS(string nameShort)
        {
            if (nameShort == "DE") return 0;
            if (nameShort == "NL") return 0;
            if (nameShort == "PL") return 0;
            if (nameShort == "PT") return 0;
            if (nameShort == "GB") return 0;

            if (nameShort == "BR") return 1;
            if (nameShort == "US") return 2;
            if (nameShort == "SG") return 3;

            return 255;
        }

        public int[] AddpossibleLeagueMatch(bool doSwap, int fixtureID, byte[] score, MySqlConnection mySqlConnection, bool isCancelled, bool penaltyToLoser)
        {
            int[] res = new int[4];

            if (fixtureID == 0) return res;

            MySqlDataReader dataReader;
            MySqlCommand cmd;

            int season = GetSeason(mySqlConnection);

            #region get data from fixtureID

            byte location = 0;
            byte division = 0;
            UInt16 _group = 0;
            DateTime dateTime = new DateTime();
            int[] tIDSlots = new int[2];
            DateTime dateTimeNow = TimeNow(mySqlConnection);

            cmd = new MySqlCommand("SELECT * FROM fixtures WHERE id=" + fixtureID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                dateTime = dataReader.GetDateTime("time");
                tIDSlots[0] = dataReader.GetInt32("tID0");
                tIDSlots[1] = dataReader.GetInt32("tID1");

                location = dataReader.GetByte("location");
                division = dataReader.GetByte("division");
                _group = dataReader.GetUInt16("_group");
            }
            dataReader.Close();

            if (doSwap)
            {
                byte _score = score[0];
                score[0] = score[1];
                score[1] = _score;
            }

            if (_group == 0) return res;
            System.TimeSpan diff = dateTime.Subtract(dateTimeNow);
            if (diff.Days > 6) return res;

            #endregion

            #region get league data

            string[] arrayStr;

            int[] tIDs = new int[12];
            byte[] apps = new byte[12];
            byte[] wins = new byte[12];
            byte[] draws = new byte[12];
            byte[] losses = new byte[12];
            UInt16[] gf = new UInt16[12];
            UInt16[] ga = new UInt16[12];
            Int16[] pts = new Int16[12];


            cmd = new MySqlCommand("SELECT * FROM league WHERE " +
                "location=" + location + " AND " +
                "division=" + division + " AND " +
                "_group=" + _group + " AND " +
                "season=" + season
                , mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                arrayStr = dataReader.GetString("tIDs").Split(',');
                for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                    int.TryParse(arrayStr[i], out tIDs[i]);
                arrayStr = dataReader.GetString("apps").Split(',');
                for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                    byte.TryParse(arrayStr[i], out apps[i]);
                arrayStr = dataReader.GetString("wins").Split(',');
                for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                    byte.TryParse(arrayStr[i], out wins[i]);
                arrayStr = dataReader.GetString("draws").Split(',');
                for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                    byte.TryParse(arrayStr[i], out draws[i]);
                arrayStr = dataReader.GetString("losses").Split(',');
                for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                    byte.TryParse(arrayStr[i], out losses[i]);
                arrayStr = dataReader.GetString("gf").Split(',');
                for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                    UInt16.TryParse(arrayStr[i], out gf[i]);
                arrayStr = dataReader.GetString("ga").Split(',');
                for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                    UInt16.TryParse(arrayStr[i], out ga[i]);
                arrayStr = dataReader.GetString("pts").Split(',');
                for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                    Int16.TryParse(arrayStr[i], out pts[i]);
            }
            dataReader.Close();
            #endregion

            #region delete fixture
            cmd = new MySqlCommand("DELETE FROM fixtures WHERE id=" + fixtureID, mySqlConnection);
            cmd.ExecuteNonQuery();
            #endregion

            #region calculate new league data

            apps[tIDSlots[0]]++;
            apps[tIDSlots[1]]++;

            if (score[0] > score[1])
            {
                wins[tIDSlots[0]]++;
                losses[tIDSlots[1]]++;
                pts[tIDSlots[0]] += 3;
            }
            else if (score[0] < score[1])
            {
                losses[tIDSlots[0]]++;
                wins[tIDSlots[1]]++;
                pts[tIDSlots[1]] += 3;
            }
            else
            {
                draws[tIDSlots[0]]++;
                draws[tIDSlots[1]]++;
                //no points are given for both teams, if match is cancelled
                if (!isCancelled)
                {
                    pts[tIDSlots[0]] += 1;
                    pts[tIDSlots[1]] += 1;
                }
            }

            //if (!isCancelled)
            //{
            gf[tIDSlots[0]] += score[0];
            ga[tIDSlots[0]] += score[1];
            gf[tIDSlots[1]] += score[1];
            ga[tIDSlots[1]] += score[0];
            //}

            if (penaltyToLoser)
            {
                if (score[0] > score[1])
                    pts[tIDSlots[1]] -= 3;
                else
                    pts[tIDSlots[0]] -= 3;

            }

            #endregion

            #region update league data

            string appsString = "";
            string winsString = "";
            string drawsString = "";
            string lossesString = "";
            string gfString = "";
            string gaString = "";
            string ptsString = "";

            for (int i = 0; i < 12; i++)
            {
                appsString += apps[i] + ",";
                winsString += wins[i] + ",";
                drawsString += draws[i] + ",";
                lossesString += losses[i] + ",";
                gfString += gf[i] + ",";
                gaString += ga[i] + ",";
                ptsString += pts[i] + ",";
            }

            appsString = appsString.TrimEnd(new char[] { ',' });
            winsString = winsString.TrimEnd(new char[] { ',' });
            drawsString = drawsString.TrimEnd(new char[] { ',' });
            lossesString = lossesString.TrimEnd(new char[] { ',' });
            gfString = gfString.TrimEnd(new char[] { ',' });
            gaString = gaString.TrimEnd(new char[] { ',' });
            ptsString = ptsString.TrimEnd(new char[] { ',' });

            cmd = new MySqlCommand("UPDATE league SET " +
                "apps='" + appsString + "', " +
                "wins='" + winsString + "', " +
                "draws='" + drawsString + "', " +
                "losses='" + lossesString + "', " +
                "gf='" + gfString + "', " +
                "ga='" + gaString + "', " +
                "pts='" + ptsString + "' " +
                "WHERE " +
                "season=" + season + " AND " +
                "location=" + location + " AND " +
                "division=" + division + " AND " +
                "_group=" + _group
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            #endregion

            res[0] = season;
            res[1] = location;
            res[2] = division;
            res[3] = _group;

            return res;
        }


    }
}
