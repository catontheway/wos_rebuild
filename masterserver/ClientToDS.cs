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
    class ClientToDS : BaseStuff
    {
        public string ipToDatabaseServer = "128.199.48.165";
        public NetClient client;
        NetPeerConfiguration config;
        public ClientToBS clientToBS;
        public ServerForGS serverForGS;
        public ServerForU serverForU;
        public List<RoomData> rooms = new List<RoomData>();
        public int totalOnlinePlayerCount = 0;
        public Thread thread;

        public ClientToDS()
        {
            if (Form1.isLocal)
                ipToDatabaseServer = "127.0.0.1";

            config = new NetPeerConfiguration("NSMobile");
            client = new NetClient(config);
            thread = new Thread(new ThreadStart(Handler));
            client.Start();
            client.Connect(ipToDatabaseServer, 14246);
            thread.Start();
        }

        void Handler()
        {
            NetIncomingMessage inmsg;
            byte b;

            while (true)
            {
                while ((inmsg = client.ReadMessage()) != null)
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

                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)inmsg.ReadByte();

                            if (status == NetConnectionStatus.Connected)
                                ConnectedToDatabaseServer();

                            if (status == NetConnectionStatus.Disconnected)
                            {
                                clientToBS.InformAboutEnabled(false);
                                Form1.Timer3Enabled = true;
                                AddText("Disconnected from DS");
                                //AddText("reconnecting to databaseserver");
                                //client.Connect(ipToDatabaseServer, 14246);
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.Data:
                            if (inmsg.LengthBytes < 1) break;

                            b = inmsg.ReadByte();

                            //DS broadcasts rooms to masterservers
                            if (b == 23)
                                Packet23(inmsg);

                            //DS sends info, if user is online
                            if (b == 27)
                                Packet27(inmsg);

                            //DS sends online users of team
                            if (b == 28)
                                Packet28(inmsg);

                            //DS have broadcasted chat message to all masterservers
                            if (b == 45)
                                Packet45(inmsg);

                            //DS informs some masterserver about challenge start
                            if (b == 47)
                                Packet47(inmsg);

                            //info about created match
                            if (b == 52)
                                Packet52(inmsg);

                            //team have invited user, lets inform possible online user about invite
                            if (b == 57)
                                Packet57(inmsg);

                            //DS tells one masterserver to check possible league matches
                            if (b == 62)
                                Packet62(inmsg);

                            //DS tells, which league matches needs to be started and which GS
                            if (b == 63)
                                Packet63(inmsg);

                            // /info msg
                            if (b == 85)
                                Packet85(inmsg);

                            //admin message
                            if (b == 86)
                                Packet86(inmsg);

                            //shutdown msg
                            if (b == 90)
                                Packet90(inmsg);

                            //addvip
                            if (b == 91)
                                Packet91(inmsg);

                            break;

                        //*************************************************************************

                        default:
                            Console.WriteLine("Unhandled type: " + inmsg.MessageType + " " + inmsg.LengthBytes + " bytes " + inmsg.DeliveryMethod + "|" + inmsg.SequenceChannel);
                            break;
                    }
                    client.Recycle(inmsg);
                }

                Thread.Sleep(1);
            }
        }

        //DS broadcasts rooms to masterservers
        void Packet23(NetIncomingMessage inmsg)
        {
            int masterserverCount;
            int gameserverCount;
            int playerCountInTeamsCount;
            string servername;
            string IP;
            int uniqueID;
            byte maxPlayers;
            byte roomType;
            byte time;
            byte roomState;
            bool botsEnabled;
            byte location;
            string natCode; //FI
            byte[] score = new byte[2];
            int[] tID = new int[2];
            string[] teamnames = new string[2];
            int[,] pID;
            int[,] plrTID;

            int _TID;
            byte _plrCount;
            bool _isPlaying;

            lock (serverForU.playerCountInTeams)
            {
                serverForU.playerCountInTeams.Clear();

                try { totalOnlinePlayerCount = inmsg.ReadInt32(); }
                catch { return; }
                try { playerCountInTeamsCount = inmsg.ReadInt32(); }
                catch { return; }

                for (int i = 0; i < playerCountInTeamsCount; i++)
                {
                    try { _TID = inmsg.ReadInt32(); }
                    catch { return; }
                    try { _plrCount = inmsg.ReadByte(); }
                    catch { return; }
                    try { _isPlaying = inmsg.ReadBoolean(); }
                    catch { return; }

                    serverForU.playerCountInTeams.Add(new PlayerCountInTeam(_TID, _plrCount, _isPlaying));
                }

            }

            lock (rooms)
            {
                rooms.Clear();

                try { masterserverCount = inmsg.ReadInt32(); }
                catch { return; }

                for (int i = 0; i < masterserverCount; i++)
                {
                    try { gameserverCount = inmsg.ReadInt32(); }
                    catch { return; }

                    for (int j = 0; j < gameserverCount; j++)
                    {
                        try { servername = inmsg.ReadString(); }
                        catch { return; }
                        try { IP = inmsg.ReadString(); }
                        catch { return; }
                        try { uniqueID = inmsg.ReadInt32(); }
                        catch { return; }
                        try { maxPlayers = inmsg.ReadByte(); }
                        catch { return; }
                        try { roomType = inmsg.ReadByte(); }
                        catch { return; }
                        try { time = inmsg.ReadByte(); }
                        catch { return; }
                        try { roomState = inmsg.ReadByte(); }
                        catch { return; }
                        try { botsEnabled = inmsg.ReadBoolean(); }
                        catch { return; }
                        try { location = inmsg.ReadByte(); }
                        catch { return; }
                        try { natCode = inmsg.ReadString(); }
                        catch { return; }

                        for (int k = 0; k < 2; k++)
                        {
                            try { score[k] = inmsg.ReadByte(); }
                            catch { return; }
                            try { tID[k] = inmsg.ReadInt32(); }
                            catch { return; }
                            try { teamnames[k] = inmsg.ReadString(); }
                            catch { return; }
                        }

                        pID = new int[2, maxPlayers];
                        plrTID = new int[2, maxPlayers];

                        for (int k = 0; k < 2; k++)
                            for (int l = 0; l < maxPlayers; l++)
                            {
                                try { pID[k, l] = inmsg.ReadInt32(); }
                                catch { return; }
                                try { plrTID[k, l] = inmsg.ReadInt32(); }
                                catch { return; }
                            }

                        rooms.Add(new RoomData(servername, IP, uniqueID, maxPlayers, roomType, time, roomState, botsEnabled, location, natCode, pID, plrTID, score, tID, teamnames));
                    }
                }
            }
        }

        //DS sends info, if user is online
        void Packet27(NetIncomingMessage inmsg)
        {
            int pID = 0;
            long remoteUniqueIdentifier;
            bool isOnline = false;

            try { pID = inmsg.ReadInt32(); }
            catch { return; }
            try { remoteUniqueIdentifier = inmsg.ReadInt64(); }
            catch { return; }
            try { isOnline = inmsg.ReadBoolean(); }
            catch { return; }

            UserConnection user = serverForU.users.Find(p => p.netConnection.RemoteUniqueIdentifier == remoteUniqueIdentifier);
            if (user == null) return;

            //****  data verified  **********

            string username = "";
            string teamname = "";
            int tID = 0;
            DateTime vip = new DateTime();
            byte FBlike = 0;
            bool ownAccount = false;
            string text = "";
            int logoSize = 0;
            byte[] logoData = null;
            DateTime created = new DateTime();
            DateTime lastLogin = new DateTime();
            byte admas = 0;
            bool LFT = false;
            byte skin = 0;
            byte hair = 0;
            byte body = 0;
            byte b;
            int[] trophies = new int[3];
            string nation = "";
            string IP = "";
            int sameIPCount = 0;
            List<string> sameIPUsername = new List<string>();
            int[] _dateTimeArr;
            bool alreadyInvited = false;
            int[] practiseStats = new int[3];  //goals, asts, teamgoals

            string[] shoeStr = new string[3];
            byte[] shoe = new byte[3];

            MySqlConnection mySqlConnection = OpenSQL();

            #region search user data
            //****************************
            //****  search user data  ****
            //****************************
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + pID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                username = dataReader.GetString("username");
                tID = dataReader.GetInt32("teamID");
                if (user.pID == pID) ownAccount = true;
                if (ownAccount) vip = dataReader.GetDateTime("vipExpire");

                text = dataReader.GetString("text");
                created = dataReader.GetDateTime("created");
                lastLogin = dataReader.GetDateTime("lastlogin");
                admas = dataReader.GetByte("admas");
                trophies[0] = dataReader.GetInt32("trophy1");
                trophies[1] = dataReader.GetInt32("trophy2");
                trophies[2] = dataReader.GetInt32("trophy3");
                FBlike = dataReader.GetByte("FBlike");
                skin = dataReader.GetByte("skin");
                hair = dataReader.GetByte("hair");
                body = dataReader.GetByte("body");
                nation = dataReader.GetString("nation");
                b = dataReader.GetByte("LFT");
                if (b == 1) LFT = true;
                IP = dataReader.GetString("IP");

                logoSize = dataReader.GetInt32(dataReader.GetOrdinal("logoSize"));
                if (logoSize > 0)
                {
                    logoData = new byte[logoSize];
                    dataReader.GetBytes(dataReader.GetOrdinal("logo"), 0, logoData, 0, (int)logoSize);
                }

                practiseStats[0] = dataReader.GetInt32("practiseGoals");
                practiseStats[1] = dataReader.GetInt32("practiseAssists");
                practiseStats[2] = dataReader.GetInt32("practiseTeamGoals");

            }
            dataReader.Close();

            //user not found. no need to inform user about error as this shoudnt happen
            if (pID == 0)
            {
                mySqlConnection.Close();
                return;
            }

            #endregion

            #region search career data

            List<int> careerTID = new List<int>();
            List<int> careerApps = new List<int>();
            List<int> careerGoals = new List<int>();
            List<int> careerAsts = new List<int>();
            List<int> careerTG = new List<int>();
            List<string> careerName = new List<string>();

            //get data
            cmd = new MySqlCommand("SELECT * FROM careers WHERE pID=" + pID + " ORDER BY joined DESC", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                careerTID.Add(dataReader.GetInt32("tID"));
                careerApps.Add(dataReader.GetInt32("apps"));
                careerGoals.Add(dataReader.GetInt32("goals"));
                careerAsts.Add(dataReader.GetInt32("asts"));
                careerTG.Add(dataReader.GetInt32("teamGoals"));
                careerName.Add("");
            }
            dataReader.Close();

            //search team names
            for (int i = 0; i < careerTID.Count; i++)
            {
                cmd = new MySqlCommand("SELECT name FROM teams where id=" + careerTID[i], mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    careerName[i] = dataReader.GetString("name");
                }
                dataReader.Close();
            }

            #endregion

            #region search users from same IP
            //*************************************
            //****  search users from same IP  ****
            //*************************************
            cmd = new MySqlCommand("SELECT username FROM users WHERE IP='" + MySqlHelper.EscapeString(IP) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                string _username = dataReader.GetString("username");

                //skip player, which data is requested
                if (_username != username)
                {
                    sameIPCount++;
                    sameIPUsername.Add(dataReader.GetString("username"));
                }
            }
            dataReader.Close();
            #endregion

            #region check, if already invited

            if (user.tID > 0)
            {
                cmd = new MySqlCommand("SELECT id FROM invites WHERE pID=" + pID + " AND tID=" + user.tID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    alreadyInvited = true;
                }
                dataReader.Close();
            }

            #endregion

            #region search invites, if own account

            List<int> inviteTIDs = new List<int>();
            List<string> inviteTIDsTeamname = new List<string>();

            if (ownAccount)
            {
                cmd = new MySqlCommand("SELECT tID FROM invites WHERE pID=" + pID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    inviteTIDs.Add(dataReader.GetInt32("tID"));
                }
                dataReader.Close();

                //get names for tID's
                for (int i = 0; i < inviteTIDs.Count; i++)
                {
                    cmd = new MySqlCommand("SELECT name FROM teams where id=" + inviteTIDs[i], mySqlConnection);
                    dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        inviteTIDsTeamname.Add(dataReader.GetString("name"));
                    }
                    dataReader.Close();
                }
            }

            #endregion

            #region get user admas (user, who have sent this 27 message)

            byte ownAdmas = 0;
            cmd = new MySqlCommand("SELECT admas FROM users WHERE id=" + user.pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                ownAdmas = dataReader.GetByte("admas");
            }
            dataReader.Close();

            #endregion

            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)27);

            outmsg.Write(username);
            outmsg.Write(ownAccount);
            outmsg.Write(text);

            outmsg.Write(logoSize);
            if (logoSize > 0)
            {
                outmsg.WritePadBits();
                outmsg.Write(logoData);
            }

            if (ownAccount)
            {
                //vip
                _dateTimeArr = DateTimeToArray(vip);
                for (int i = 0; i < 6; i++)
                    outmsg.Write(_dateTimeArr[i]);

                //servertime (mySQL)
                DateTime dateTimeNow = TimeNow(mySqlConnection);
                _dateTimeArr = DateTimeToArray(dateTimeNow);
                for (int i = 0; i < 6; i++)
                    outmsg.Write(_dateTimeArr[i]);

                outmsg.Write(inviteTIDsTeamname.Count);
                for (int i = 0; i < inviteTIDs.Count; i++)
                    outmsg.Write(inviteTIDsTeamname[i]);

                if (user.version > 30)
                    outmsg.Write(FBlike);
            }

            //created
            _dateTimeArr = DateTimeToArray(created);
            for (int i = 0; i < 6; i++)
                outmsg.Write(_dateTimeArr[i]);

            //last login
            _dateTimeArr = DateTimeToArray(lastLogin);
            for (int i = 0; i < 6; i++)
                outmsg.Write(_dateTimeArr[i]);

            for (int i = 0; i < 3; i++)
                outmsg.Write(trophies[i]);

            outmsg.Write(isOnline);
            outmsg.Write(admas);
            outmsg.Write(ownAdmas);
            outmsg.Write(LFT);
            outmsg.Write(skin);
            outmsg.Write(hair);
            outmsg.Write(body);
            outmsg.Write(nation);
            outmsg.Write(alreadyInvited);
            outmsg.Write(practiseStats[0]);
            outmsg.Write(practiseStats[1]);
            outmsg.Write(practiseStats[2]);

            outmsg.Write(sameIPCount);
            for (int i = 0; i < sameIPCount; i++)
                outmsg.Write(sameIPUsername[i]);

            outmsg.Write(careerTID.Count);
            for (int i = 0; i < careerTID.Count; i++)
            {
                outmsg.Write(careerName[i]);
                outmsg.Write(careerApps[i]);
                outmsg.Write(careerGoals[i]);
                outmsg.Write(careerAsts[i]);
                outmsg.Write(careerTG[i]);
            }

            serverForU.server.SendMessage(outmsg, user.netConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();

        }

        //DS sends online users of team
        void Packet28(NetIncomingMessage inmsg)
        {

            int tID = 0;
            long remoteUniqueIdentifier;
            int pIDCount;
            List<int> pIDOnline = new List<int>();

            try { tID = inmsg.ReadInt32(); }
            catch { return; }

            try { remoteUniqueIdentifier = inmsg.ReadInt64(); }
            catch { return; }

            try { pIDCount = inmsg.ReadInt32(); }
            catch { return; }

            for (int i = 0; i < pIDCount; i++)
                try { pIDOnline.Add(inmsg.ReadInt32()); }
                catch { return; }

            UserConnection user = serverForU.users.Find(p => p.netConnection.RemoteUniqueIdentifier == remoteUniqueIdentifier);
            if (user == null) return;
            //if (user.pID == 0) return;

            //****  data verified  **********

            string teamname = "";
            int founder = 0;
            string founderName = "";
            DateTime created = new DateTime();
            int rank = 0;
            int teamapps = 0;
            int wins = 0;
            int draws = 0;
            int losses = 0;
            int[] _dateTimeArr;
            int location = 0;
            int division = 0;
            int _group = 0;
            string text = "";
            int logoSize = 0;
            int[] trophies = new int[3];
            byte[] logoData = null;
            byte[] shirtstyle = new byte[2];
            string rgbString;
            string[] arrayStr;
            byte[] shirtRgb = new byte[24];
            bool allowJoinWithoutInvite = false;
            byte b;
            bool isOwnTeam = false;
            bool isLockedTeam = false;
            int highestRank = 0;
            int lowestRank = 0;
            int[] wonRow = new int[2];
            int[] loseRow = new int[2];
            int[] noLost = new int[2];
            int[] noWin = new int[2];

            int[] recordMatchID = new int[3];
            DateTime[] recordDate = new DateTime[3];
            int[] recordHomeTID = new int[3];
            int[] recordAwayTID = new int[3];
            string[] recordHomeTeamname = new string[3];
            string[] recordAwayTeamname = new string[3];
            byte[] recordHomeScore = new byte[3];
            byte[] recordAwayScore = new byte[3];
            int[] recordRankChange = new int[3];
            byte[] recordDetailsAvailable = new byte[3];

            MySqlConnection mySqlConnection = OpenSQL();

            #region  search team data

            MySqlCommand cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + tID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                teamname = dataReader.GetString("name");
                tID = dataReader.GetInt32("id");    //used later against hackers
                if (tID == user.tID) isOwnTeam = true;
                founder = dataReader.GetInt32("founder");
                created = dataReader.GetDateTime("created");
                rank = dataReader.GetInt32("rank");
                teamapps = dataReader.GetInt32("apps");
                wins = dataReader.GetInt32("wins");
                draws = dataReader.GetInt32("draws");
                losses = dataReader.GetInt32("losses");
                location = dataReader.GetInt32("location");
                text = dataReader.GetString("text");

                trophies[0] = dataReader.GetInt32("trophy1");
                trophies[1] = dataReader.GetInt32("trophy2");
                trophies[2] = dataReader.GetInt32("trophy3");

                if (dataReader["division"] != DBNull.Value)
                {
                    division = dataReader.GetInt32("division");
                    _group = dataReader.GetInt32("_group");
                }

                logoSize = dataReader.GetInt32(dataReader.GetOrdinal("logoSize"));
                if (logoSize > 0)
                {
                    logoData = new byte[logoSize];
                    dataReader.GetBytes(dataReader.GetOrdinal("logo"), 0, logoData, 0, (int)logoSize);
                }

                for (int i = 0; i < 2; i++)
                    shirtstyle[i] = dataReader.GetByte("shirtstyle" + i);
                rgbString = dataReader.GetString("rgb");
                arrayStr = rgbString.Split(',');
                for (int i = 0; i < 24; i++)
                    byte.TryParse(arrayStr[i], out shirtRgb[i]);

                b = dataReader.GetByte("allowJoinWithoutInvite");
                if (b == 1) allowJoinWithoutInvite = true;

                highestRank = dataReader.GetInt32("highestRank");
                lowestRank = dataReader.GetInt32("lowestRank");
                recordMatchID[0] = dataReader.GetInt32("biggestWinMatchID");
                recordMatchID[1] = dataReader.GetInt32("biggestDefeatMatchID");
                recordMatchID[2] = dataReader.GetInt32("highestScoringGameMatchID");
                wonRow[0] = dataReader.GetInt32("mostGamesWonAM");
                loseRow[0] = dataReader.GetInt32("mostGamesLostAM");
                noLost[0] = dataReader.GetInt32("mostGamesNotLoseAM");
                noWin[0] = dataReader.GetInt32("mostGamesNotWinAM");
                wonRow[1] = dataReader.GetInt32("mostGamesWonATR");
                loseRow[1] = dataReader.GetInt32("mostGamesLostATR");
                noLost[1] = dataReader.GetInt32("mostGamesNotLoseATR");
                noWin[1] = dataReader.GetInt32("mostGamesNotWinATR");

            }
            dataReader.Close();

            if (tID == 0)//against hackers
            {
                mySqlConnection.Close();
                return;
            }

            #endregion

            #region founder name

            cmd = new MySqlCommand("SELECT username FROM users WHERE id=" + founder, mySqlConnection);

            dataReader = cmd.ExecuteReader();

            while (dataReader.Read())
            {
                founderName = dataReader.GetString("username");
            }
            dataReader.Close();

            #endregion

            #region players

            List<string> username = new List<string>();
            List<int> pID = new List<int>();
            List<string> nation = new List<string>();
            List<int> posUP = new List<int>();
            List<int> posDown = new List<int>();
            List<int> posLeft = new List<int>();
            List<int> posRight = new List<int>();
            List<byte> admas = new List<byte>();
            List<byte> number = new List<byte>();
            List<bool> online = new List<bool>();

            List<int> apps = new List<int>();
            List<int> goals = new List<int>();
            List<int> asts = new List<int>();
            List<int> teamGoals = new List<int>();

            cmd = new MySqlCommand("SELECT * FROM users WHERE teamID=" + tID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                username.Add(dataReader.GetString("username"));
                pID.Add(dataReader.GetInt32("id"));
                nation.Add(dataReader.GetString("nation"));
                posUP.Add(dataReader.GetInt32("posUP"));
                posDown.Add(dataReader.GetInt32("posDown"));
                posLeft.Add(dataReader.GetInt32("posLeft"));
                posRight.Add(dataReader.GetInt32("posRight"));
                admas.Add(dataReader.GetByte("admas"));
                number.Add(dataReader.GetByte("number"));
                online.Add(false);
            }
            dataReader.Close();

            //search career data for players (only current career team)
            for (int i = 0; i < pID.Count; i++)
            {
                cmd = new MySqlCommand("SELECT * FROM careers WHERE pID=" + pID[i] + " AND tID=" + tID + " ORDER BY joined DESC LIMIT 1", mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    apps.Add(dataReader.GetInt32("apps"));
                    goals.Add(dataReader.GetInt32("goals"));
                    asts.Add(dataReader.GetInt32("asts"));
                    teamGoals.Add(dataReader.GetInt32("teamGoals"));
                }
                dataReader.Close();
            }

            for (int i = 0; i < online.Count; i++)
                for (int j = 0; j < pIDOnline.Count; j++)
                    if (pID[i] == pIDOnline[j])
                    {
                        online[i] = true;
                        break;
                    }

            #endregion

            #region get user admas and lockedTID (user, who have sent this 28 message)

            byte ownAdmas = 0;
            int lockedTID = 0;

            cmd = new MySqlCommand("SELECT admas, lockedTID FROM users WHERE id=" + user.pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                ownAdmas = dataReader.GetByte("admas");
                lockedTID = dataReader.GetInt32("lockedTID");
            }
            dataReader.Close();

            if (lockedTID == tID) isLockedTeam = true;

            #endregion

            #region invites

            List<int> invitePID = new List<int>();
            List<string> inviteUsername = new List<string>();
            List<string> inviteNation = new List<string>();
            List<int> invitePosUp = new List<int>();
            List<int> invitePosDown = new List<int>();
            List<int> invitePosLeft = new List<int>();
            List<int> invitePosRight = new List<int>();
            List<string> inviteTeamname = new List<string>();

            if (isOwnTeam)
            {
                cmd = new MySqlCommand("SELECT * FROM invites WHERE tID=" + tID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    invitePID.Add(dataReader.GetInt32("pID"));
                }
                dataReader.Close();

                //seach data to invites
                for (int i = 0; i < invitePID.Count; i++)
                {
                    bool found = false;
                    string _inviteUsername = "";
                    string _inviteNation = "";
                    int _invitePosUp = 0;
                    int _invitePosDown = 0;
                    int _invitePosLeft = 0;
                    int _invitePosRight = 0;
                    int _inviteTID = 0;

                    cmd = new MySqlCommand("SELECT * FROM users where id=" + invitePID[i], mySqlConnection);
                    dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        found = true;
                        _inviteUsername = dataReader.GetString("username");
                        _inviteNation = dataReader.GetString("nation");
                        _invitePosUp = dataReader.GetInt32("posUp");
                        _invitePosDown = dataReader.GetInt32("posDown");
                        _invitePosLeft = dataReader.GetInt32("posLeft");
                        _invitePosRight = dataReader.GetInt32("posRight");
                        _inviteTID = dataReader.GetInt32("teamID");
                    }
                    dataReader.Close();

                    if (!found) continue;

                    inviteUsername.Add(_inviteUsername);
                    inviteNation.Add(_inviteNation);
                    invitePosUp.Add(_invitePosUp);
                    invitePosDown.Add(_invitePosDown);
                    invitePosLeft.Add(_invitePosLeft);
                    invitePosRight.Add(_invitePosRight);

                    //if user have team, lets get teamname
                    if (_inviteTID > 0)
                    {
                        bool _teamnameAdded = false;

                        cmd = new MySqlCommand("SELECT name FROM teams where id=" + _inviteTID, mySqlConnection);
                        dataReader = cmd.ExecuteReader();
                        while (dataReader.Read())
                        {
                            inviteTeamname.Add(dataReader.GetString("name"));
                            _teamnameAdded = true;
                        }
                        dataReader.Close();

                        //jossain on bugi, et pelaaja jättää joukkueensa (tässä tapauksessa owner) mut userin teamID ei nollautunut
                        //joten tämä estää crashin tässä kohtaa
                        if (!_teamnameAdded)
                            inviteTeamname.Add("");
                    }
                    else
                    {
                        inviteTeamname.Add("");
                    }
                }
            } //end of if (isOwnTeam)

            #endregion

            #region get record match data (if exists)
            for (int i = 0; i < 3; i++)
            {
                if (recordMatchID[i] == -1) continue;

                cmd = new MySqlCommand("SELECT * FROM matches WHERE id=" + recordMatchID[i], mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    recordDate[i] = dataReader.GetDateTime("time");
                    recordHomeTID[i] = dataReader.GetInt32("tID0");
                    recordAwayTID[i] = dataReader.GetInt32("tID1");
                    recordHomeScore[i] = dataReader.GetByte("score0");
                    recordAwayScore[i] = dataReader.GetByte("score1");
                    recordDetailsAvailable[i] = dataReader.GetByte("detailsAvailable");

                    if (tID == recordHomeTID[i])
                        recordRankChange[i] = dataReader.GetInt32("rankChange0");
                    else
                        recordRankChange[i] = dataReader.GetInt32("rankChange1");
                }
                dataReader.Close();

                //get hometeam name
                if (recordHomeTID[i] > -1)
                {
                    cmd = new MySqlCommand("SELECT name FROM teams WHERE id=" + recordHomeTID[i], mySqlConnection);
                    dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        recordHomeTeamname[i] = dataReader.GetString("name");
                    }
                    dataReader.Close();
                }

                //get awayteam name
                if (recordAwayTID[i] > -1)
                {
                    cmd = new MySqlCommand("SELECT name FROM teams WHERE id=" + recordAwayTID[i], mySqlConnection);
                    dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        recordAwayTeamname[i] = dataReader.GetString("name");
                    }
                    dataReader.Close();
                }

            }

            #endregion

            //************************************
            //*******  write data  ***************
            //************************************

            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)28);

            outmsg.Write(ownAdmas);
            outmsg.Write(isOwnTeam);
            outmsg.Write(isLockedTeam);
            outmsg.Write(allowJoinWithoutInvite);
            outmsg.Write(teamname);
            outmsg.Write(rank);
            outmsg.Write(teamapps);
            outmsg.Write(wins);
            outmsg.Write(draws);
            outmsg.Write(losses);
            outmsg.Write(location);

            if (user.version > 30)
            {
                outmsg.Write(division);
                outmsg.Write(_group);
            }
            outmsg.Write(text);
            outmsg.Write(founderName);

            for (int i = 0; i < 3; i++)
                outmsg.Write(trophies[i]);

            outmsg.Write(logoSize);
            if (logoSize > 0)
            {
                outmsg.WritePadBits();
                outmsg.Write(logoData);
            }

            for (int i = 0; i < 2; i++)
                outmsg.Write(shirtstyle[i]);
            for (int i = 0; i < 24; i++)
                outmsg.Write(shirtRgb[i]);

            outmsg.Write(highestRank);
            outmsg.Write(lowestRank);

            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(wonRow[i]);
                outmsg.Write(loseRow[i]);
                outmsg.Write(noLost[i]);
                outmsg.Write(noWin[i]);
            }

            for (int i = 0; i < 3; i++)
            {
                outmsg.Write(recordMatchID[i]);

                _dateTimeArr = DateTimeToArray(recordDate[i]);
                for (int j = 0; j < 6; j++)
                    outmsg.Write(_dateTimeArr[j]);

                outmsg.Write(recordHomeTeamname[i]);
                outmsg.Write(recordAwayTeamname[i]);

                outmsg.Write(recordHomeScore[i]);
                outmsg.Write(recordAwayScore[i]);
                outmsg.Write(recordRankChange[i]);
                outmsg.Write(recordDetailsAvailable[i]);
            }

            //created
            _dateTimeArr = DateTimeToArray(created);
            for (int i = 0; i < 6; i++)
                outmsg.Write(_dateTimeArr[i]);

            //player data
            outmsg.Write(pID.Count);
            for (int i = 0; i < pID.Count; i++)
            {
                outmsg.Write(username[i]);
                outmsg.Write(nation[i]);
                outmsg.Write(posUP[i]);
                outmsg.Write(posDown[i]);
                outmsg.Write(posLeft[i]);
                outmsg.Write(posRight[i]);
                outmsg.Write(admas[i]);
                outmsg.Write(number[i]);
                outmsg.Write(online[i]);
                outmsg.Write(apps[i]);
                outmsg.Write(goals[i]);
                outmsg.Write(asts[i]);
                outmsg.Write(teamGoals[i]);
            }

            //invites
            outmsg.Write(inviteUsername.Count);
            for (int i = 0; i < inviteUsername.Count; i++)
            {
                outmsg.Write(inviteUsername[i]);
                outmsg.Write(inviteNation[i]);
                outmsg.Write(invitePosUp[i]);
                outmsg.Write(invitePosDown[i]);
                outmsg.Write(invitePosLeft[i]);
                outmsg.Write(invitePosRight[i]);
                outmsg.Write(inviteTeamname[i]);
            }

            serverForU.server.SendMessage(outmsg, user.netConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //DS have broadcasted chat message to all masterservers
        void Packet45(NetIncomingMessage inmsg)
        {
            int senderPID;
            int senderTID;
            string senderUsername = "";
            string senderTeamname = "";

            byte receiverType;  //0=to user, 1=to team, 2=public
            string receiver;   //receiver username or teamname
            int receiverPID = 0;
            int receiverTID = 0;
            string message;

            try { receiverType = inmsg.ReadByte(); }
            catch { return; }
            try { receiver = inmsg.ReadString(); }
            catch { return; }
            try { message = inmsg.ReadString(); }
            catch { return; }
            try { senderPID = inmsg.ReadInt32(); }
            catch { return; }
            try { senderTID = inmsg.ReadInt32(); }
            catch { return; }

            if (receiverType > 2) return;
            if (receiver == "") return;
            if (message == "") return;
            if (senderPID == 0) return;

            //data readed            

            MySqlConnection mySqlConnection = OpenSQL();

            #region get sender username
            MySqlCommand cmd = new MySqlCommand("SELECT username FROM users WHERE id=" + senderPID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                senderUsername = dataReader.GetString("username");
            }
            dataReader.Close();

            if (senderUsername == "")
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region get sender teamname
            if (senderTID > 0)
            {
                cmd = new MySqlCommand("SELECT name FROM teams WHERE id=" + senderTID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    senderTeamname = dataReader.GetString("name");
                }
                dataReader.Close();
                if (senderTeamname == "")
                {
                    mySqlConnection.Close();
                    return;
                }
            }
            #endregion

            #region message is to user, so lets get receiverPID
            if (receiverType == 0)
            {
                cmd = new MySqlCommand("SELECT id FROM users WHERE username='" + MySqlHelper.EscapeString(receiver) + "'", mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    receiverPID = dataReader.GetInt32("id");
                }
                dataReader.Close();

                if (receiverPID == 0)
                {
                    mySqlConnection.Close();
                    return;
                }
            }
            #endregion

            #region message is to team, so lets get receiverTID
            if (receiverType == 1)
            {
                cmd = new MySqlCommand("SELECT id FROM teams WHERE name='" + MySqlHelper.EscapeString(receiver) + "'", mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    receiverTID = dataReader.GetInt32("id");
                }
                dataReader.Close();

                if (receiverTID == 0)
                {
                    mySqlConnection.Close();
                    return;
                }
            }
            #endregion

            #region send data (serverForU)

            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)45);

            outmsg.Write(receiverType);
            outmsg.Write(receiver);
            outmsg.Write(message);
            outmsg.Write(senderUsername);
            outmsg.Write(senderTeamname);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            #region message to user
            if (receiverType == 0)
            {
                foreach (UserConnection u in serverForU.users)
                {
                    if (u.netConnection == null) continue;
                    if (u.pID == senderPID) continue;

                    if (u.pID == receiverPID)
                    {
                        var another = serverForU.server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        serverForU.server.SendMessage(another, u.netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                        break;
                    }
                }
            }
            #endregion

            #region message to team (sender HAVE team)
            if (receiverType == 1 && senderTID > 0)
            {
                foreach (UserConnection u in serverForU.users)
                {
                    if (u.netConnection == null) continue;
                    if (u.pID == senderPID) continue;

                    if (u.tID == receiverTID || u.tID == senderTID)
                    {
                        var another = serverForU.server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        serverForU.server.SendMessage(another, u.netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                    }
                }
            }
            #endregion

            #region message to team (sender HAVENT team)
            if (receiverType == 1 && senderTID == 0)
            {
                foreach (UserConnection u in serverForU.users)
                {
                    if (u.netConnection == null) continue;
                    if (u.pID == senderPID) continue;

                    if (u.tID == receiverTID)
                    {
                        var another = serverForU.server.CreateMessage();
                        another.Write(saved);
                        another.LengthBits = savedBitLength;
                        serverForU.server.SendMessage(another, u.netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                    }
                }
            }
            #endregion

            #region public message
            if (receiverType == 2)
            {
                foreach (UserConnection u in serverForU.users)
                {
                    if (u.netConnection == null) continue;
                    if (u.pID == senderPID) continue;

                    var another = serverForU.server.CreateMessage();
                    another.Write(saved);
                    another.LengthBits = savedBitLength;
                    serverForU.server.SendMessage(another, u.netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                }
            }
            #endregion

            #endregion

            //if public message, no need to send to gameservers
            if (receiverType == 2)
            {
                mySqlConnection.Close();
                return;
            }

            #region send data (serverForGS)

            outmsg = serverForGS.server.CreateMessage();
            outmsg.Write((byte)45);

            outmsg.Write(receiverType);
            outmsg.Write(receiverPID);
            outmsg.Write(receiverTID);
            outmsg.Write(message);
            outmsg.Write(senderUsername);
            outmsg.Write(senderTeamname);

            saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            savedBitLength = outmsg.LengthBits;

            foreach (GameServer g in serverForGS.gameServers)
            {
                if (g.netConnection == null) continue;

                var another = serverForGS.server.CreateMessage();
                another.Write(saved);
                another.LengthBits = savedBitLength;
                serverForGS.server.SendMessage(another, g.netConnection, NetDeliveryMethod.ReliableOrdered, 3);
            }

            #endregion

            mySqlConnection.Close();
        }

        //DS informs some masterserver about challenge start
        void Packet47(NetIncomingMessage inmsg)
        {

            string IP;
            bool botsEnabled;
            byte maxPlayers;
            int[] tID = new int[2];
            byte[] selectedKit = new byte[2];

            try { IP = inmsg.ReadString(); }
            catch { return; }
            try { botsEnabled = inmsg.ReadBoolean(); }
            catch { return; }
            try { maxPlayers = inmsg.ReadByte(); }
            catch { return; }

            for (int i = 0; i < 2; i++)
            {
                try { tID[i] = inmsg.ReadInt32(); }
                catch { return; }
                try { selectedKit[i] = inmsg.ReadByte(); }
                catch { return; }
            }

            //data readed

            string[] teamnames = new string[2];
            string rgbString;
            string[] arrayStr;
            byte[,] shirtRgb = new byte[2, 24];
            byte[] shirtstyle = new byte[2];

            MySqlConnection mySqlConnection = OpenSQL();

            #region get team data
            for (int i = 0; i < 2; i++)
            {
                MySqlCommand cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + tID[i], mySqlConnection);
                MySqlDataReader dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    teamnames[i] = dataReader.GetString("name");

                    shirtstyle[i] = dataReader.GetByte("shirtstyle" + selectedKit[i]);

                    rgbString = dataReader.GetString("rgb");
                    arrayStr = rgbString.Split(',');
                    for (int j = 0; j < 24; j++)
                        byte.TryParse(arrayStr[j], out shirtRgb[i, j]);

                }
                dataReader.Close();
            }
            #endregion

            //send data

            //find gameserver by IP, who should host challenge
            for (int i = 0; i < serverForGS.gameServers.Count; i++)
                if (serverForGS.gameServers[i].netConnection.RemoteEndPoint.Address.ToString() == IP)
                {
                    NetOutgoingMessage outmsg = serverForGS.server.CreateMessage();
                    outmsg.Write((byte)47);

                    outmsg.Write(false);  //officiallyStarted
                    outmsg.Write((int)0); //fixtureID, if officiallyStarted
                    outmsg.Write(botsEnabled);
                    outmsg.Write(maxPlayers);

                    for (int j = 0; j < 2; j++)
                    {
                        outmsg.Write(false);
                        outmsg.Write(tID[j]);
                        outmsg.Write(teamnames[j]);
                        outmsg.Write(selectedKit[j]);    //0-1 (home or away colors)
                        outmsg.Write(shirtstyle[j]);     //0-9 (which mask is used)                   

                        for (int k = 0; k < 24; k++)
                            outmsg.Write(shirtRgb[j, k]);

                    }

                    serverForGS.server.SendMessage(outmsg, serverForGS.gameServers[i].netConnection, NetDeliveryMethod.ReliableOrdered, 0);
                    break;
                }

            mySqlConnection.Close();
        }

        //info about created match
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

            #region send to lobbyusers

            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)52);

            for (int i = 0; i < 2; i++)
                outmsg.Write(teamnames[i]);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            foreach (UserConnection u in serverForU.users)
            {
                if (u.netConnection == null) continue;
                if (u.tID == 0) continue;
                if (u.tID == tID[0] || u.tID == tID[1])
                {
                    var another = serverForU.server.CreateMessage();
                    another.Write(saved);
                    another.LengthBits = savedBitLength;
                    serverForU.server.SendMessage(another, u.netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                }
            }

            #endregion

            #region send to gameservers

            outmsg = serverForGS.server.CreateMessage();
            outmsg.Write((byte)52);

            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(tID[i]);
                outmsg.Write(teamnames[i]);
            }

            saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            savedBitLength = outmsg.LengthBits;

            foreach (GameServer g in serverForGS.gameServers)
            {
                if (g.netConnection == null) continue;

                var another = serverForGS.server.CreateMessage();
                another.Write(saved);
                another.LengthBits = savedBitLength;
                serverForGS.server.SendMessage(another, g.netConnection, NetDeliveryMethod.ReliableOrdered, 3);
            }

            #endregion

        }

        //team have invited user, lets inform possible online user about invite
        void Packet57(NetIncomingMessage inmsg)
        {
            int pID;

            try { pID = inmsg.ReadInt32(); }
            catch { return; }

            #region send to lobbyusers

            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)57);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            foreach (UserConnection u in serverForU.users)
            {
                if (u.netConnection == null) continue;
                if (u.pID == pID)
                {
                    var another = serverForU.server.CreateMessage();
                    another.Write(saved);
                    another.LengthBits = savedBitLength;
                    serverForU.server.SendMessage(another, u.netConnection, NetDeliveryMethod.ReliableOrdered, 3);
                    break;
                }
            }

            #endregion

            #region send to gameservers

            outmsg = serverForGS.server.CreateMessage();
            outmsg.Write((byte)57);
            outmsg.Write(pID);

            saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            savedBitLength = outmsg.LengthBits;

            foreach (GameServer g in serverForGS.gameServers)
            {
                if (g.netConnection == null) continue;

                var another = serverForGS.server.CreateMessage();
                another.Write(saved);
                another.LengthBits = savedBitLength;
                serverForGS.server.SendMessage(another, g.netConnection, NetDeliveryMethod.ReliableOrdered, 3);
            }

            #endregion

        }

        //DS tells one masterserver to check possible league matches AND delete unactive users
        void Packet62(NetIncomingMessage inmsg)
        {
            //jos loginista aikaa 7 päivää
            //jos kaikki posUP, posDown ym=0, 

            //select username from users where DATE_SUB(CURDATE(),INTERVAL 90 DAY) > lastlogin;
            //etsi vanhemmat kuin 90 päivää

            MySqlConnection mySqlConnection = OpenSQL();

            int season = GetSeason(mySqlConnection);

            DeleteInactiveUsers(mySqlConnection, true);
            DeleteInactiveUsers(mySqlConnection, false);

            MySqlCommand cmd;
            MySqlDataReader dataReader;

            //DateTime serverIime = TimeNow(mySqlConnection);
            //string dateTimeStr = serverIime.Year + "-" + serverIime.Month + "-" + serverIime.Day + " " + serverIime.Hour + ":" + serverIime.Minute + ":" + serverIime.Second;

            #region get fixtures, which should be started

            List<Fixture> fixtures = new List<Fixture>();

            //cmd = new MySqlCommand("SELECT * FROM fixtures WHERE time<'" + dateTimeStr + "'", mySqlConnection);  now()
            cmd = new MySqlCommand("SELECT * FROM fixtures WHERE time<now()", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                fixtures.Add(new Fixture(dataReader.GetInt32("id"), dataReader.GetInt32("tID0"), dataReader.GetInt32("tID1"), dataReader.GetByte("location"), dataReader.GetByte("division"), dataReader.GetUInt16("_group")));
            }
            dataReader.Close();
            #endregion

            #region get leagues, which have matches
            List<Fixture> leaguesWhichHaveMatches = new List<Fixture>();

            for (int i = 0; i < fixtures.Count; i++)
            {
                bool alreadyAdded = false;

                for (int j = 0; j < leaguesWhichHaveMatches.Count; j++)
                    if (fixtures[i].location == leaguesWhichHaveMatches[j].location && fixtures[i].division == leaguesWhichHaveMatches[j].division && fixtures[i]._group == leaguesWhichHaveMatches[j]._group)
                    {
                        alreadyAdded = true;
                        break;
                    }

                if (!alreadyAdded)
                    leaguesWhichHaveMatches.Add(new Fixture(0, 0, 0, fixtures[i].location, fixtures[i].division, fixtures[i]._group));
            }
            #endregion

            #region get tIDs from leagues and set those to fixtures.tID[]
            for (int i = 0; i < leaguesWhichHaveMatches.Count; i++)
            {
                string tIDstring = "";
                string[] arrayStr;
                int[] tIDs = new int[12];

                cmd = new MySqlCommand("SELECT tIDs FROM league WHERE " +
                    "location=" + leaguesWhichHaveMatches[i].location + " AND " +
                    "division=" + leaguesWhichHaveMatches[i].division + " AND " +
                    "_group=" + leaguesWhichHaveMatches[i]._group + " AND " +
                    "season=" + season
                    , mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    tIDstring = dataReader.GetString("tIDs");
                    arrayStr = tIDstring.Split(',');
                    for (int j = 0; j < Form1.maxTeamsInDiv; j++)
                        int.TryParse(arrayStr[j], out tIDs[j]);

                }
                dataReader.Close();

                for (int k = 0; k < fixtures.Count; k++)
                {
                    if (fixtures[k].location == leaguesWhichHaveMatches[i].location && fixtures[k].division == leaguesWhichHaveMatches[i].division && fixtures[k]._group == leaguesWhichHaveMatches[i]._group)
                    {
                        fixtures[k].tID[0] = tIDs[fixtures[k].tIDSlot[0]];
                        fixtures[k].tID[1] = tIDs[fixtures[k].tIDSlot[1]];
                    }
                }

            }
            #endregion

            #region if another/both teams are bots, calculate league points and delete fixture
            for (int j = 0; j < fixtures.Count; j++)
            {
                string[] arrayStr;
                byte[] apps = new byte[12];
                byte[] wins = new byte[12];
                byte[] draws = new byte[12];
                byte[] losses = new byte[12];
                UInt16[] pts = new UInt16[12];

                if (fixtures[j].tID[0] == 0 || fixtures[j].tID[1] == 0)
                {
                    cmd = new MySqlCommand("SELECT * FROM league WHERE " +
                        "location=" + fixtures[j].location + " AND " +
                        "division=" + fixtures[j].division + " AND " +
                        "_group=" + fixtures[j]._group + " AND " +
                        "season=" + season
                        , mySqlConnection);
                    dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        arrayStr = dataReader.GetString("apps").Split(',');
                        for (int i = 0; i < 12; i++)
                            byte.TryParse(arrayStr[i], out apps[i]);

                        arrayStr = dataReader.GetString("wins").Split(',');
                        for (int i = 0; i < 12; i++)
                            byte.TryParse(arrayStr[i], out wins[i]);

                        arrayStr = dataReader.GetString("draws").Split(',');
                        for (int i = 0; i < 12; i++)
                            byte.TryParse(arrayStr[i], out draws[i]);

                        arrayStr = dataReader.GetString("losses").Split(',');
                        for (int i = 0; i < 12; i++)
                            byte.TryParse(arrayStr[i], out losses[i]);

                        arrayStr = dataReader.GetString("pts").Split(',');
                        for (int i = 0; i < 12; i++)
                            UInt16.TryParse(arrayStr[i], out pts[i]);
                    }
                    dataReader.Close();

                    apps[fixtures[j].tIDSlot[0]]++;
                    apps[fixtures[j].tIDSlot[1]]++;

                    //bot vs bot
                    if (fixtures[j].tID[0] == 0 && fixtures[j].tID[1] == 0)
                    {
                        draws[fixtures[j].tIDSlot[0]]++;
                        draws[fixtures[j].tIDSlot[1]]++;
                    }

                    if (fixtures[j].tID[0] > 0)
                    {
                        pts[fixtures[j].tIDSlot[0]] += 3;
                        wins[fixtures[j].tIDSlot[0]]++;
                        losses[fixtures[j].tIDSlot[1]]++;
                    }
                    if (fixtures[j].tID[1] > 0)
                    {
                        pts[fixtures[j].tIDSlot[1]] += 3;
                        wins[fixtures[j].tIDSlot[1]]++;
                        losses[fixtures[j].tIDSlot[0]]++;
                    }

                    string appsString = "";
                    string winsString = "";
                    string drawsString = "";
                    string lossesString = "";
                    string ptsString = "";

                    //generate new strings
                    for (int i = 0; i < 12; i++)
                    {
                        appsString += apps[i] + ",";
                        winsString += wins[i] + ",";
                        drawsString += draws[i] + ",";
                        lossesString += losses[i] + ",";
                        ptsString += pts[i] + ",";
                    }

                    appsString = appsString.TrimEnd(new char[] { ',' });
                    winsString = winsString.TrimEnd(new char[] { ',' });
                    drawsString = drawsString.TrimEnd(new char[] { ',' });
                    lossesString = lossesString.TrimEnd(new char[] { ',' });
                    ptsString = ptsString.TrimEnd(new char[] { ',' });

                    cmd = new MySqlCommand("UPDATE league SET " +
                        "apps='" + appsString + "', " +
                        "wins='" + winsString + "', " +
                        "draws='" + drawsString + "', " +
                        "losses='" + lossesString + "', " +
                        "pts='" + ptsString + "' " +
                        "WHERE " +
                        "season=" + season + " AND " +
                        "location=" + fixtures[j].location + " AND " +
                        "division=" + fixtures[j].division + " AND " +
                        "_group=" + fixtures[j]._group
                        , mySqlConnection);
                    cmd.ExecuteNonQuery();

                    cmd = new MySqlCommand("DELETE FROM fixtures WHERE id=" + fixtures[j].fixtureID, mySqlConnection);
                    cmd.ExecuteNonQuery();
                }

            }
            #endregion


            //send data

            int matchCount = 0;

            NetOutgoingMessage outmsg = client.CreateMessage();
            outmsg.Write((byte)62);

            //calculate matchCount
            for (int i = 0; i < fixtures.Count; i++)
                if (fixtures[i].tID[0] > 0 && fixtures[i].tID[1] > 0)
                    matchCount++;

            outmsg.Write(matchCount);

            for (int i = 0; i < fixtures.Count; i++)
                if (fixtures[i].tID[0] > 0 && fixtures[i].tID[1] > 0)
                {
                    outmsg.Write(fixtures[i].fixtureID);
                    outmsg.Write(fixtures[i].location);
                    outmsg.Write(fixtures[i].tID[0]);
                    outmsg.Write(fixtures[i].tID[1]);
                }

            client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();

        }

        //DS tells, which league matches needs to be started and which GS
        public void Packet63(NetIncomingMessage inmsg)
        {
            int fixtureCount = 0;

            try { fixtureCount = inmsg.ReadInt32(); }
            catch { return; }

            int[] fixtureIDs = new int[fixtureCount];
            string[] IPs = new string[fixtureCount];
            int[,] tID = new int[2, fixtureCount];

            for (int i = 0; i < fixtureCount; i++)
            {
                try { fixtureIDs[i] = inmsg.ReadInt32(); }
                catch { return; }
                try { IPs[i] = inmsg.ReadString(); }
                catch { return; }
                try { tID[0, i] = inmsg.ReadInt32(); }
                catch { return; }
                try { tID[1, i] = inmsg.ReadInt32(); }
                catch { return; }
            }

            //data readed

            string[] teamnames = new string[2];
            bool[] isOfficialLeagueBotTeam = new bool[2];
            string rgbString;
            string[] arrayStr;
            byte[,] shirtRgb = new byte[2, 24];  //tID, rgb
            byte[,] shirtstyles = new byte[2, 2];   //tID, 0-9 (which mask is used)  
            //byte[] selectedKit = new byte[2];  //0-1 (home or away colors)
            //selectedKit[0] = 0; //väliaikasesti molemmille kotipaidat. voitas kuitenki kalkuloida jotku paita differences
            //selectedKit[1] = 0;

            MySqlConnection mySqlConnection = OpenSQL();

            #region get team data

            for (int k = 0; k < fixtureCount; k++)
            {
                for (int i = 0; i < 2; i++)
                {
                    isOfficialLeagueBotTeam[i] = false;

                    //this is bot team
                    if (tID[i, k] == 0)
                    {
                        isOfficialLeagueBotTeam[i] = true;
                        teamnames[i] = "Bot team";
                        shirtstyles[i, 0] = 0;
                        shirtstyles[i, 1] = 0;
                        for (int j = 0; j < 12; j++)
                            shirtRgb[i, j] = 0;
                        for (int j = 12; j < 24; j++)
                            shirtRgb[i, j] = 255;
                        continue;
                    }

                    MySqlCommand cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + tID[i, k], mySqlConnection);
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        teamnames[i] = dataReader.GetString("name");

                        shirtstyles[i, 0] = dataReader.GetByte("shirtstyle0");
                        shirtstyles[i, 1] = dataReader.GetByte("shirtstyle1");

                        rgbString = dataReader.GetString("rgb");
                        arrayStr = rgbString.Split(',');
                        for (int j = 0; j < 24; j++)
                            byte.TryParse(arrayStr[j], out shirtRgb[i, j]);

                    }
                    dataReader.Close();
                }

                byte[] _rgbH = new byte[24];
                byte[] _rgbA = new byte[24];

                for (int j = 0; j < 24; j++) _rgbH[j] = shirtRgb[0, j];
                for (int j = 0; j < 24; j++) _rgbA[j] = shirtRgb[1, j];

                byte[] selectedKit = DecideSelectedKits(_rgbH.ToArray(), _rgbA.ToArray());

                //find gameserver by IP, who should host challenge
                for (int i = 0; i < serverForGS.gameServers.Count; i++)
                    if (serverForGS.gameServers[i].netConnection.RemoteEndPoint.Address.ToString() == IPs[k])
                    {
                        NetOutgoingMessage outmsg = serverForGS.server.CreateMessage();
                        outmsg.Write((byte)47);

                        outmsg.Write(true);  //officiallyStarted
                        outmsg.Write(fixtureIDs[k]);
                        outmsg.Write(true); //bots enabled
                        outmsg.Write((byte)5);

                        for (int j = 0; j < 2; j++)
                        {
                            outmsg.Write(isOfficialLeagueBotTeam[j]);
                            outmsg.Write(tID[j, k]);
                            outmsg.Write(teamnames[j]);
                            outmsg.Write(selectedKit[j]);    //0-1 (home or away colors)
                            outmsg.Write(shirtstyles[j, selectedKit[j]]);     //0-9 (which mask is used)                   

                            for (int m = 0; m < 24; m++)
                                outmsg.Write(shirtRgb[j, m]);

                        }
                        serverForGS.server.SendMessage(outmsg, serverForGS.gameServers[i].netConnection, NetDeliveryMethod.ReliableOrdered, 0);
                        break;
                    }


            }
            #endregion


            mySqlConnection.Close();
        }

        // /info msg
        void Packet85(NetIncomingMessage inmsg)
        {

            string username;
            string where;
            long remoteUniqueIdentifier;

            try { username = inmsg.ReadString(); }
            catch { return; }
            try { where = inmsg.ReadString(); }
            catch { return; }
            try { remoteUniqueIdentifier = inmsg.ReadInt64(); }
            catch { return; }

            UserConnection user = serverForU.users.Find(p => p.netConnection.RemoteUniqueIdentifier == remoteUniqueIdentifier);
            if (user == null) return;

            //****  data verified  **********

            //************************************
            //*******  write data  ***************
            //************************************

            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)85);

            outmsg.Write(username);
            outmsg.Write(where);

            serverForU.server.SendMessage(outmsg, user.netConnection, NetDeliveryMethod.ReliableOrdered, 0);

        }

        //admin message
        void Packet86(NetIncomingMessage inmsg)
        {
            string message;

            try { message = inmsg.ReadString(); }
            catch { return; }

            //data readed          

            #region to lobby users
            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
            outmsg.Write((byte)86);
            outmsg.Write(message);

            var saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            var savedBitLength = outmsg.LengthBits;

            for (int i = 0; i < serverForU.users.Count; i++)
            {
                if (serverForU.users[i].netConnection == null) continue;

                var another = serverForU.server.CreateMessage();
                another.Write(saved);
                another.LengthBits = savedBitLength;
                serverForU.server.SendMessage(another, serverForU.users[i].netConnection, NetDeliveryMethod.ReliableOrdered, 3);
            }
            #endregion

            #region to gameservers
            outmsg = serverForGS.server.CreateMessage();
            outmsg.Write((byte)86);
            outmsg.Write(message);

            saved = new byte[outmsg.LengthBytes];
            Buffer.BlockCopy(outmsg.Data, 0, saved, 0, outmsg.LengthBytes);
            savedBitLength = outmsg.LengthBits;

            for (int i = 0; i < serverForGS.gameServers.Count; i++)
            {
                if (serverForGS.gameServers[i].netConnection == null) continue;

                var another = serverForGS.server.CreateMessage();
                another.Write(saved);
                another.LengthBits = savedBitLength;
                serverForGS.server.SendMessage(another, serverForGS.gameServers[i].netConnection, NetDeliveryMethod.ReliableOrdered, 3);
            }
            #endregion

        }

        //shutdown msg
        void Packet90(NetIncomingMessage inmsg)
        {
            serverForU.server.Shutdown("");
            serverForGS.server.Shutdown("");
        }

        //addvip
        void Packet91(NetIncomingMessage inmsg)
        {
            string username;
            int days;

            try { username = inmsg.ReadString(); }
            catch { return; }
            try { days = inmsg.ReadInt32(); }
            catch { return; }

            int pID = 0;

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd = new MySqlCommand("SELECT id FROM users WHERE username='" + MySqlHelper.EscapeString(username) + "'", mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                pID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            if (pID == 0)
            {
                mySqlConnection.Close();
                return;
            }

            AddVip(pID, mySqlConnection, days, 0, 0);


            mySqlConnection.Close();
        }

        public void DeleteInactiveUsers(MySqlConnection mySqlConnection, bool is30Days)
        {
            int season = GetSeason(mySqlConnection);

            MySqlCommand cmd;
            MySqlDataReader dataReader;

            //******************

            List<int> pID = new List<int>();
            List<int> plrTID = new List<int>();

            if (is30Days)
                cmd = new MySqlCommand("SELECT id,teamID FROM users WHERE DATE_SUB(CURDATE(),INTERVAL 30 DAY) > lastlogin AND practiseTeamGoals=0 LIMIT 10", mySqlConnection);
            else
                cmd = new MySqlCommand("SELECT id,teamID FROM users WHERE DATE_SUB(CURDATE(),INTERVAL 180 DAY) > lastlogin LIMIT 10", mySqlConnection);

            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                pID.Add(dataReader.GetInt32("id"));
                plrTID.Add(dataReader.GetInt32("teamID"));

            }
            dataReader.Close();

            for (int i = 0; i < pID.Count; i++)
            {
                //delete user, if vip expired
                if (!IsVip(pID[i], mySqlConnection))
                {
                    cmd = new MySqlCommand("DELETE FROM users WHERE id=" + pID[i], mySqlConnection);
                    cmd.ExecuteNonQuery();

                    cmd = new MySqlCommand("DELETE FROM careers WHERE pID=" + pID[i], mySqlConnection);
                    cmd.ExecuteNonQuery();

                    //delete invites
                    cmd = new MySqlCommand("DELETE FROM invites WHERE pID=" + pID[i], mySqlConnection);
                    cmd.ExecuteNonQuery();

                    //get player count of team
                    if (plrTID[i] > 0)
                    {
                        bool deleteTeam = false;

                        cmd = new MySqlCommand("SELECT count(id) FROM users WHERE teamID=" + plrTID[i], mySqlConnection);
                        dataReader = cmd.ExecuteReader();
                        while (dataReader.Read())
                        {
                            int count = dataReader.GetInt32("count(id)");
                            if (count == 0) deleteTeam = true;

                        }
                        dataReader.Close();

                        if (deleteTeam)
                        {
                            #region get team data & remove from league

                            int location = 0;
                            int division = 0;
                            int _group = 0;

                            cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + plrTID[i], mySqlConnection);
                            dataReader = cmd.ExecuteReader();
                            while (dataReader.Read())
                            {
                                location = dataReader.GetInt32("location");
                                if (dataReader["division"] != DBNull.Value)
                                {
                                    division = dataReader.GetInt32("division");
                                    _group = dataReader.GetInt32("_group");
                                }
                            }
                            dataReader.Close();

                            if (division > 0)
                            {
                                string tIDstring = "";
                                string[] arrayStr;
                                int[] tIDs = new int[Form1.maxTeamsInDiv];

                                //get tIDs from league
                                cmd = new MySqlCommand("SELECT tIDs FROM league WHERE " +
                                    "location=" + location + " AND " +
                                    "division=" + division + " AND " +
                                    "_group=" + _group + " AND " +
                                    "season=" + season
                                    , mySqlConnection);
                                dataReader = cmd.ExecuteReader();
                                while (dataReader.Read())
                                {
                                    tIDstring = dataReader.GetString("tIDs");
                                    arrayStr = tIDstring.Split(',');
                                    for (int j = 0; j < Form1.maxTeamsInDiv; j++)
                                        int.TryParse(arrayStr[j], out tIDs[j]);

                                }
                                dataReader.Close();

                                //remove tID 
                                for (int j = 0; j < Form1.maxTeamsInDiv; j++)
                                    if (tIDs[j] == plrTID[i])
                                    {
                                        tIDs[j] = 0;
                                        break;
                                    }

                                //generate new tIDs string
                                tIDstring = "";
                                for (int j = 0; j < Form1.maxTeamsInDiv; j++)
                                    tIDstring += tIDs[j] + ",";
                                tIDstring = tIDstring.TrimEnd(new char[] { ',' });

                                //update tIDs to league
                                cmd = new MySqlCommand("UPDATE league SET tIDs='" + tIDstring + "' WHERE " +
                                    "location=" + location + " AND " +
                                    "division=" + division + " AND " +
                                    "_group=" + _group + " AND " +
                                    "season=" + season
                                    , mySqlConnection);
                                cmd.ExecuteNonQuery();


                            }
                            #endregion

                            //delete team
                            cmd = new MySqlCommand("DELETE FROM teams WHERE id=" + plrTID[i], mySqlConnection);
                            cmd.ExecuteNonQuery();

                            //delete invites
                            cmd = new MySqlCommand("DELETE FROM invites WHERE tID=" + plrTID[i], mySqlConnection);
                            cmd.ExecuteNonQuery();

                            #region delete matches (where also opponent team is deleted)
                            List<int> matchIDs = new List<int>();
                            List<int> matchOppTID = new List<int>();

                            cmd = new MySqlCommand("SELECT id,tID0,tID1 FROM matches WHERE tID0=" + plrTID[i] + " OR tID1=" + plrTID[i], mySqlConnection);
                            dataReader = cmd.ExecuteReader();
                            while (dataReader.Read())
                            {
                                matchIDs.Add(dataReader.GetInt32("id"));
                                if (dataReader.GetInt32("tID0") == plrTID[i])
                                    matchOppTID.Add(dataReader.GetInt32("tID1"));
                                else
                                    matchOppTID.Add(dataReader.GetInt32("tID0"));
                            }
                            dataReader.Close();

                            //check, if opponent team exists
                            for (int j = 0; j < matchIDs.Count; j++)
                            {
                                bool opponentTeamExists = false;

                                cmd = new MySqlCommand("SELECT id FROM teams WHERE id=" + matchOppTID[j], mySqlConnection);
                                dataReader = cmd.ExecuteReader();
                                while (dataReader.Read())
                                {
                                    opponentTeamExists = true;
                                }
                                dataReader.Close();

                                if (!opponentTeamExists)
                                {
                                    cmd = new MySqlCommand("DELETE FROM matches WHERE id=" + matchIDs[j], mySqlConnection);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            #endregion


                        }
                    }
                }

            }

        }

        byte[] DecideSelectedKits(byte[] _rgbH, byte[] _rgbA)
        {
            byte[] res = new byte[2];

            int[] RGBCount = new int[4];
            int biggest = 0;
            int kitsDecided = 0;

            RGBCount[0] = CompareRGB(new byte[3] { _rgbH[0], _rgbH[1], _rgbH[2] }, new byte[3] { _rgbA[0], _rgbA[1], _rgbA[2] });
            RGBCount[1] = CompareRGB(new byte[3] { _rgbH[0], _rgbH[1], _rgbH[2] }, new byte[3] { _rgbA[12], _rgbA[13], _rgbA[14] });
            RGBCount[2] = CompareRGB(new byte[3] { _rgbH[12], _rgbH[13], _rgbH[14] }, new byte[3] { _rgbA[0], _rgbA[1], _rgbA[2] });
            RGBCount[3] = CompareRGB(new byte[3] { _rgbH[12], _rgbH[13], _rgbH[14] }, new byte[3] { _rgbA[12], _rgbA[13], _rgbA[14] });

            for (int i = 0; i < 4; i++)
                if (RGBCount[i] > biggest)
                {
                    biggest = RGBCount[i];
                    kitsDecided = i;
                }

            if (kitsDecided == 0)
            {
                res[0] = 0;
                res[1] = 0;
            }
            if (kitsDecided == 1)
            {
                res[0] = 0;
                res[1] = 1;
            }
            if (kitsDecided == 2)
            {
                res[0] = 1;
                res[1] = 0;
            }
            if (kitsDecided == 3)
            {
                res[0] = 1;
                res[1] = 1;
            }

            return res;

        }

        int CompareRGB(byte[] homeRGB, byte[] awayRGB)
        {
            int _r, _g, _b;

            if (homeRGB[0] > awayRGB[0]) _r = homeRGB[0] - awayRGB[0]; else _r = awayRGB[0] - homeRGB[0];
            if (homeRGB[1] > awayRGB[1]) _g = homeRGB[1] - awayRGB[1]; else _g = awayRGB[1] - homeRGB[1];
            if (homeRGB[2] > awayRGB[2]) _b = homeRGB[2] - awayRGB[2]; else _b = awayRGB[2] - homeRGB[2];

            return _r + _g + _b;
        }

        void ConnectedToDatabaseServer()
        {
            AddText("connected to DatabaseServer");

            clientToBS.InformAboutEnabled(true);

            //send gameservers and users to database server
            NetOutgoingMessage outmsg = client.CreateMessage();
            outmsg.Write((byte)22);
            //outmsg.Write(connectionsCount);
            client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void SendLobbyUsersToDS()
        {
            NetOutgoingMessage outmsg = client.CreateMessage();
            outmsg.Write((byte)44);

            outmsg.Write(serverForU.users.Count);
            for (int i = 0; i < serverForU.users.Count; i++)
            {
                outmsg.Write(serverForU.users[i].pID);
                outmsg.Write(serverForU.users[i].tID);
            }

            client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void InformAboutGSDisconnect(string gameserverIP)
        {
            NetOutgoingMessage outmsg = client.CreateMessage();
            outmsg.Write((byte)49);

            outmsg.Write(gameserverIP);

            client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
        }




    }
}
