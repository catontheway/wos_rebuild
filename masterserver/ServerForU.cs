using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Drawing;
using System.IO;
using System.Diagnostics;

namespace MasterServer
{
    class ServerForU : BaseStuff
    {
        public NetServer server;
        NetPeerConfiguration config;
        public List<UserConnection> users = new List<UserConnection>();
        public ClientToDS clientToDS;
        public ClientToBS clientToBS;
        public List<PlayerCountInTeam> playerCountInTeams = new List<PlayerCountInTeam>();
        public Thread thread;

        public ServerForU()
        {
            config = new NetPeerConfiguration("NSMobile");
            config.MaximumConnections = 10000;
            config.Port = 14244;
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
            UserConnection user = null;

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

                            lock (users)
                            {
                                users.Add(new UserConnection(inmsg.SenderConnection));
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)inmsg.ReadByte();

                            if (status == NetConnectionStatus.Disconnected)
                            {
                                user = users.Find(p => p.netConnection == inmsg.SenderConnection);
                                lock (users)
                                    users.RemoveAll(p => p.netConnection == inmsg.SenderConnection);
                            }

                            break;

                        //*************************************************************************

                        case NetIncomingMessageType.Data:
                            user = users.Find(p => p.netConnection == inmsg.SenderConnection);
                            if (user == null || inmsg.LengthBytes < 1) break;

                            b = inmsg.ReadByte();

                            //user registers username
                            if (b == 4)
                                Packet4(inmsg, user);

                            //user logins
                            if (b == 6)
                                Packet6(inmsg, user);

                            //user requests room list (public rooms)
                            if (b == 7)
                                Packet7(inmsg, user);

                            //user requests player data 
                            if (b == 27)
                                Packet27(inmsg);

                            //user requests team data 
                            if (b == 28)
                                Packet28(inmsg);

                            //user creates team
                            if (b == 29)
                                Packet29(inmsg, user);

                            //user requests online teams
                            if (b == 30)
                                Packet30(inmsg);

                            //user requests matches
                            if (b == 31)
                                Packet31(inmsg, user);

                            //quick search
                            if (b == 32)
                                Packet32(inmsg);

                            //user requests player stats
                            if (b == 33)
                                Packet33(inmsg);

                            //user requests team stats
                            if (b == 34)
                                Packet34(inmsg);

                            //user requests team stats records
                            if (b == 35)
                                Packet35(inmsg);

                            //team have sent invite to user
                            if (b == 36)
                                Packet36(inmsg, user);

                            //user responds to invite (accept/reject)
                            if (b == 37)
                                Packet37(inmsg, user);

                            //user leaves team
                            if (b == 38)
                                Packet38(inmsg, user);

                            //user changes admas status
                            if (b == 39)
                                Packet39(inmsg, user);

                            //user is kicked from team
                            if (b == 40)
                                Packet40(inmsg, user);

                            //invite cancel
                            if (b == 41)
                                Packet41(inmsg, user);

                            //user requests results
                            if (b == 42)
                                Packet42(inmsg);

                            //user request details about specific match
                            if (b == 43)
                                Packet43(inmsg);

                            //chat message
                            if (b == 45)
                                Packet45(inmsg, user);

                            //user requests data for challenge screen
                            if (b == 46)
                                Packet46(inmsg, user);

                            //user request to start challenge
                            if (b == 47)
                                Packet47(inmsg, user);

                            //user modifies player (in user->settings)
                            if (b == 50)
                                Packet50(inmsg, user);

                            //user join without invite
                            if (b == 51)
                                Packet51(inmsg, user);

                            //user save team settings
                            if (b == 53)
                                Packet53(inmsg, user);

                            //user requests online player count
                            if (b == 55)
                                Packet55(inmsg);

                            //user requests competition
                            if (b == 60)
                                Packet60(inmsg, user);

                            //user requests competition fixtures
                            if (b == 61)
                                Packet61(inmsg);

                            //user have changed logo
                            if (b == 65)
                                Packet65(inmsg, user);

                            //user sets free text
                            if (b == 66)
                                Packet66(inmsg, user);

                            //user performs adv search players
                            if (b == 67)
                                Packet67(inmsg);

                            //user performs adv search teams
                            if (b == 68)
                                Packet68(inmsg);

                            //team wants to join to league
                            if (b == 69)
                                Packet69(inmsg, user);

                            //user modifies player (in play->settings)
                            if (b == 70)
                                Packet70(inmsg, user);

                            //user have completed reward video or FB liked
                            if (b == 71)
                                Packet71(inmsg, user);

                            //user changes LFT status
                            if (b == 72)
                                Packet72(inmsg, user);

                            //user sends his version and platform to masterserver
                            if (b == 73)
                                Packet73(inmsg, user);

                            //user have completed payment
                            if (b == 74)
                                Packet74(inmsg, user);

                            //misc click (send plrs from nations)
                            if (b == 76)
                                Packet76(inmsg);

                            //user sends his version and platform to masterserver and requests username (steam)
                            if (b == 77)
                                Packet77(inmsg, user);

                            //user creates username (steam)
                            if (b == 78)
                                Packet78(inmsg, user);

                            //user logins with steam username
                            if (b == 79)
                                Packet79(inmsg, user);

                            //client start purchase process (steam)
                            if (b == 80)
                                Packet80(inmsg, user);

                            //user sends finalize steam purchase data
                            if (b == 81)
                                Packet81(inmsg, user);

                            //user sends finalize steam purchase data
                            if (b == 85)
                                Packet85(inmsg, user);

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

        //user registers username
        void Packet4(NetIncomingMessage inmsg, UserConnection user)
        {
            if (user.pID > 0) return;

            string username = "";
            string password = "";
            byte platform;

            try { username = inmsg.ReadString(); }
            catch { return; }
            try { password = inmsg.ReadString(); }
            catch { return; }
            try { platform = inmsg.ReadByte(); }
            catch { return; }

            if (username.Length > 15) return;
            if (password.Length > 15) return;
            if (username == "" || password == "") return;
            //****  data verified  ****

            username = username.Trim();
            password = password.Trim();

            MySqlConnection mySqlConnection = OpenSQL();

            int pID = 0;
            MySqlCommand cmd = new MySqlCommand("SELECT id FROM users WHERE username='" + MySqlHelper.EscapeString(username) + "'", mySqlConnection);

            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                pID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            //username already taken
            if (pID > 0)
            {
                SendInfoMsg(inmsg.SenderConnection, 1);
                mySqlConnection.Close();
                return;
            }

            string MD5Password = MD5Hash(password);

            cmd = new MySqlCommand("INSERT INTO users SET " +
                "username=binary'" + MySqlHelper.EscapeString(username) + "', " +
                "password=binary'" + MySqlHelper.EscapeString(MD5Password) + "', " +
                "created=NOW()" + ", " +
                "vipExpire=NOW()"
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            //lets add some vip to new user
            /*cmd = new MySqlCommand("SELECT id FROM users WHERE username='" + MySqlHelper.EscapeString(username) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                pID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            AddVip(pID, mySqlConnection, 0, 0, 30);*/

            LoginThread loginThread = new LoginThread(username, password, 0, platform, inmsg.SenderConnection, user, mySqlConnection, clientToDS, this);
            loginThread.thread.Start();
            //Login2(username, password, 0, platform, inmsg.SenderConnection, user, mySqlConnection);
        }

        //user logins
        void Packet6(NetIncomingMessage inmsg, UserConnection user)
        {
            string username = "";
            string password = "";
            byte platform;

            try { username = inmsg.ReadString(); }  //username
            catch { return; }
            try { password = inmsg.ReadString(); }  //password            
            catch { return; }
            try { platform = inmsg.ReadByte(); }
            catch { return; }

            if (username == "" || password == "") return;
            if (username.Length > 15) return;
            if (password.Length > 15) return;
            //****  data verified  ****

            username = username.Trim();
            password = password.Trim();

            LoginThread loginThread = new LoginThread(username, password, 0, platform, inmsg.SenderConnection, user, null, clientToDS, this);
            loginThread.thread.Start();
            //Login2(username, password, 0, platform, inmsg.SenderConnection, user, null);
        }

        //user requests room list (public rooms)
        void Packet7(NetIncomingMessage inmsg, UserConnection user)
        {
            byte roomType;

            try { roomType = inmsg.ReadByte(); }
            catch { return; }

            if (roomType != 0) return;

            lock (clientToDS.rooms)
            {
                int _count = 0;

                for (int i = 0; i < clientToDS.rooms.Count; i++)
                    if (clientToDS.rooms[i].roomType == 0)
                        _count++;

                //send data to user
                NetOutgoingMessage outmsg = server.CreateMessage();
                outmsg.Write((byte)7);
                outmsg.Write(_count);

                foreach (RoomData r in clientToDS.rooms)
                {
                    if (r.roomType != 0) continue;

                    outmsg.Write(r.uniqueID);
                    outmsg.Write(r.IP);

                    outmsg.Write(r.servername);
                    outmsg.Write(r.GetPlayerCount());  //current player count
                    outmsg.Write(r.maxPlayers);  //max player count
                    outmsg.Write(r.time);  //time
                    //if(user.version>30)
                    outmsg.Write(r.roomState);
                    outmsg.Write(r.botsEnabled);
                    outmsg.Write(r.location);
                    outmsg.Write(r.natCode);
                }

                server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        //user requests player data 
        void Packet27(NetIncomingMessage inmsg)
        {
            string username;

            try { username = inmsg.ReadString(); }
            catch { return; }

            if (username == "") return;

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

            long remoteUniqueIdentifier = inmsg.SenderConnection.RemoteUniqueIdentifier;

            NetOutgoingMessage outmsg = clientToDS.client.CreateMessage();
            outmsg.Write((byte)27);
            outmsg.Write(pID);
            outmsg.Write(remoteUniqueIdentifier);

            clientToDS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user requests team data 
        void Packet28(NetIncomingMessage inmsg)
        {
            string teamname;

            try { teamname = inmsg.ReadString(); }
            catch { return; }

            if (teamname == "") return;

            int tID = 0;

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd = new MySqlCommand("SELECT id FROM teams WHERE name='" + MySqlHelper.EscapeString(teamname) + "'", mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            if (tID == 0)
            {
                mySqlConnection.Close();
                return;
            }

            long remoteUniqueIdentifier = inmsg.SenderConnection.RemoteUniqueIdentifier;

            NetOutgoingMessage outmsg = clientToDS.client.CreateMessage();
            outmsg.Write((byte)28);
            outmsg.Write(tID);
            outmsg.Write(remoteUniqueIdentifier);

            clientToDS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();

        }

        //user creates team
        void Packet29(NetIncomingMessage inmsg, UserConnection user)
        {

            if (user.pID == 0) return;
            if (user.tID > 0) return;

            string teamname;
            bool allowJoiningWithoutInvite;
            byte b;
            byte location;

            try { teamname = inmsg.ReadString(); }
            catch { return; }
            try { allowJoiningWithoutInvite = inmsg.ReadBoolean(); }
            catch { return; }
            try { location = inmsg.ReadByte(); }
            catch { return; }

            if (teamname == "") return;
            if (teamname.Length > 20) return;
            if (location > 3) return;
            teamname = teamname.Trim();

            //****  data verified  ****

            int tID = 0;
            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;

            //lets check that teamname is available
            cmd = new MySqlCommand("SELECT id FROM teams WHERE name='" + MySqlHelper.EscapeString(teamname) + "'", mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            //teamname already taken
            if (tID > 0)
            {
                SendInfoMsg(inmsg.SenderConnection, 3);
                mySqlConnection.Close();
                return;
            }

            if (allowJoiningWithoutInvite) b = 1; else b = 0;

            //insert team
            cmd = new MySqlCommand("INSERT INTO teams SET " +
                "name=binary'" + MySqlHelper.EscapeString(teamname) + "'," +
                "founder=" + user.pID + "," +
                "location=" + location + "," +
                "allowJoinWithoutInvite=" + b + "," +
                "lastLogin=NOW()" + "," +
                "created=NOW()"
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            //search id, which were just inserted
            cmd = new MySqlCommand("SELECT id FROM teams WHERE name='" + MySqlHelper.EscapeString(teamname) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            //before we insert new career entry, lets check users previous careers.
            //if there is career entry without any apps, we can delete that
            cmd = new MySqlCommand("DELETE FROM careers WHERE apps=0 AND pID=" + user.pID, mySqlConnection);
            cmd.ExecuteNonQuery();

            //create new career entry
            cmd = new MySqlCommand("INSERT INTO careers SET " +
                "pID=" + user.pID + ", " +
                "tID=" + tID + ", " +
                "joined=NOW()"
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            //update user to new team and lets set him as a master
            cmd = new MySqlCommand("UPDATE users SET teamID=" + tID + ", admas=3, LFT=0 WHERE id=" + user.pID, mySqlConnection);
            cmd.ExecuteNonQuery();

            user.tID = tID;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)29);
            outmsg.Write(tID);
            outmsg.Write(teamname);
            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);

            mySqlConnection.Close();

        }

        //user requests online teams
        void Packet30(NetIncomingMessage inmsg)
        {
            List<string> teamnames = new List<string>();
            List<byte> location = new List<byte>();
            List<byte> allowJoinWithoutInvite = new List<byte>();
            List<int> rank = new List<int>();
            List<byte> plrOnlineCount = new List<byte>();
            List<bool> isPlaying = new List<bool>();

            MySqlConnection mySqlConnection = OpenSQL();


            lock (playerCountInTeams)
            {
                for (int i = 0; i < playerCountInTeams.Count; i++)
                {
                    //if (playerCountInTeams[i].plrCount < 3) continue;

                    MySqlCommand cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + playerCountInTeams[i].tID, mySqlConnection);
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        teamnames.Add(dataReader.GetString("name"));
                        location.Add(dataReader.GetByte("location"));
                        allowJoinWithoutInvite.Add(dataReader.GetByte("allowJoinWithoutInvite"));
                        rank.Add(dataReader.GetInt32("rank"));
                        plrOnlineCount.Add(playerCountInTeams[i].plrCount);
                        isPlaying.Add(playerCountInTeams[i].isPlaying);
                    }
                    dataReader.Close();
                }
            }


            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)30);

            outmsg.Write(teamnames.Count);

            for (int i = 0; i < teamnames.Count; i++)
            {
                outmsg.Write(teamnames[i]);
                outmsg.Write(location[i]);
                outmsg.Write(allowJoinWithoutInvite[i]);
                outmsg.Write(rank[i]);
                outmsg.Write(plrOnlineCount[i]);
                outmsg.Write(isPlaying[i]);
            }

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user requests matches
        void Packet31(NetIncomingMessage inmsg, UserConnection user)
        {
            byte roomType;

            try { roomType = inmsg.ReadByte(); }
            catch { return; }

            if (roomType != 1) return;

            lock (clientToDS.rooms)
            {
                int _count = 0;

                for (int i = 0; i < clientToDS.rooms.Count; i++)
                    if (clientToDS.rooms[i].roomType == 1)
                        _count++;

                //send data to user
                NetOutgoingMessage outmsg = server.CreateMessage();
                outmsg.Write((byte)31);
                outmsg.Write(_count);

                foreach (RoomData r in clientToDS.rooms)
                {
                    if (r.roomType != 1) continue;

                    outmsg.Write(r.uniqueID);
                    outmsg.Write(r.IP);
                    outmsg.Write(r.location);
                    outmsg.Write(r.natCode);

                    outmsg.Write(r.GetPlayerCount());  //current player count
                    outmsg.Write(r.maxPlayers);  //max player count
                    outmsg.Write(r.time);  //time
                    //if (user.version > 30)
                    outmsg.Write(r.roomState);

                    outmsg.Write(r.botsEnabled);
                    outmsg.Write(r.score[0]);
                    outmsg.Write(r.teamnames[0]);
                    outmsg.Write(r.score[1]);
                    outmsg.Write(r.teamnames[1]);   
                }

                server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        //quick search
        void Packet32(NetIncomingMessage inmsg)
        {
            string searchName;

            try { searchName = inmsg.ReadString(); }
            catch { return; }

            searchName = searchName.Trim();
            searchName = searchName.ToLower();
            if (searchName == "") return;

            //****** data verified ************

            MySqlConnection mySqlConnection = OpenSQL();

            int LIMIT = 20;
            string foundname;
            string foundnameLowercase;

            bool foundExactUser = false;
            List<string> usernames = new List<string>();
            List<int> pID = new List<int>();
            List<int> plrTID = new List<int>();
            List<string> nation = new List<string>();
            List<int> posUP = new List<int>();
            List<int> posDown = new List<int>();
            List<int> posLeft = new List<int>();
            List<int> posRight = new List<int>();
            List<string> plrTeamname = new List<string>();
            List<int> careerTotalApps = new List<int>();
            List<int> careerTotalGoals = new List<int>();
            List<int> careerTotalAsts = new List<int>();
            List<int> careerTotalTeamGoals = new List<int>();
            List<int> practiseGoals = new List<int>();
            List<int> practiseAssists = new List<int>();
            List<int> practiseTeamGoals = new List<int>();

            #region search exact user
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM users WHERE username='" + MySqlHelper.EscapeString(searchName) + "'", mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                foundname = dataReader.GetString("username");
                usernames.Add(foundname);
                pID.Add(dataReader.GetInt32("id"));
                plrTID.Add(dataReader.GetInt32("teamID"));
                nation.Add(dataReader.GetString("nation"));
                posUP.Add(dataReader.GetInt32("posUP"));
                posDown.Add(dataReader.GetInt32("posDown"));
                posLeft.Add(dataReader.GetInt32("posLeft"));
                posRight.Add(dataReader.GetInt32("posRight"));
                plrTeamname.Add("");
                practiseGoals.Add(dataReader.GetInt32("practiseGoals"));
                practiseAssists.Add(dataReader.GetInt32("practiseAssists"));
                practiseTeamGoals.Add(dataReader.GetInt32("practiseTeamGoals"));

                LIMIT = 19;
                foundExactUser = true;
            }
            dataReader.Close();
            #endregion

            #region search like user
            cmd = new MySqlCommand("SELECT * FROM users WHERE username LIKE '%" + MySqlHelper.EscapeString(searchName) + "%' LIMIT " + LIMIT, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                foundname = dataReader.GetString("username");
                foundnameLowercase = foundname.ToLower();
                if (foundnameLowercase != searchName) //we already tried to find exact same searchName in previous query, so lets skip this one
                {
                    usernames.Add(foundname);
                    pID.Add(dataReader.GetInt32("id"));
                    plrTID.Add(dataReader.GetInt32("teamID"));
                    nation.Add(dataReader.GetString("nation"));
                    posUP.Add(dataReader.GetInt32("posUP"));
                    posDown.Add(dataReader.GetInt32("posDown"));
                    posLeft.Add(dataReader.GetInt32("posLeft"));
                    posRight.Add(dataReader.GetInt32("posRight"));
                    plrTeamname.Add("");
                    practiseGoals.Add(dataReader.GetInt32("practiseGoals"));
                    practiseAssists.Add(dataReader.GetInt32("practiseAssists"));
                    practiseTeamGoals.Add(dataReader.GetInt32("practiseTeamGoals"));
                }
            }
            dataReader.Close();
            #endregion

            #region search teamnames for users
            for (int i = 0; i < usernames.Count; i++)
            {
                if (plrTID[i] == 0) continue;

                cmd = new MySqlCommand("SELECT name FROM teams WHERE id=" + plrTID[i], mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    plrTeamname[i] = dataReader.GetString("name");
                }
                dataReader.Close();
            }
            #endregion

            #region search career datas for users
            for (int i = 0; i < usernames.Count; i++)
            {
                cmd = new MySqlCommand("SELECT count(id) AS careerTotalTeamCount, " +
                    "COALESCE(SUM(apps),0) AS careerTotalApps, " +
                    "COALESCE(SUM(goals),0) AS careerTotalGoals, " +
                    "COALESCE(SUM(asts),0) AS careerTotalAsts, " +
                    "COALESCE(SUM(teamGoals),0) AS careerTotalTeamGoals " +
                    "FROM careers WHERE pID=" + pID[i], mySqlConnection);
                dataReader = cmd.ExecuteReader();

                //careerTotalteamGoals
                //above COALESCE function replaces NUll values with 0 (or value, you add to function)
                while (dataReader.Read())
                {
                    careerTotalApps.Add(dataReader.GetInt32("careerTotalApps"));
                    careerTotalGoals.Add(dataReader.GetInt32("careerTotalGoals"));
                    careerTotalAsts.Add(dataReader.GetInt32("careerTotalAsts"));
                    careerTotalTeamGoals.Add(dataReader.GetInt32("careerTotalTeamGoals"));
                }
                dataReader.Close();
            }
            #endregion

            //**********************************
            //**** end of user searching  ******
            //**********************************

            LIMIT = 20;

            bool foundExactTeam = false;
            List<string> teamnames = new List<string>();
            List<int> tID = new List<int>();
            List<int> location = new List<int>();
            List<int> rank = new List<int>();
            List<int> plrCount = new List<int>();
            List<int> apps = new List<int>();
            List<int> wins = new List<int>();
            List<int> draws = new List<int>();
            List<int> losses = new List<int>();

            #region search exact team
            cmd = new MySqlCommand("SELECT * FROM teams WHERE name='" + MySqlHelper.EscapeString(searchName) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                foundname = dataReader.GetString("name");
                teamnames.Add(foundname);
                tID.Add(dataReader.GetInt32("id"));
                plrCount.Add(dataReader.GetByte("plrCount"));
                location.Add(dataReader.GetInt32("location"));
                rank.Add(dataReader.GetInt32("rank"));
                apps.Add(dataReader.GetInt32("apps"));
                wins.Add(dataReader.GetInt32("wins"));
                draws.Add(dataReader.GetInt32("draws"));
                losses.Add(dataReader.GetInt32("losses"));

                LIMIT = 19;
                foundExactTeam = true;
            }
            dataReader.Close();
            #endregion

            #region search like team
            cmd = new MySqlCommand("SELECT * FROM teams WHERE name LIKE '%" + MySqlHelper.EscapeString(searchName) + "%' LIMIT " + LIMIT, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                foundname = dataReader.GetString("name");
                foundnameLowercase = foundname.ToLower();
                if (foundnameLowercase != searchName) //we already tried to find exact same searchName in previous query, so lets skip this one
                {
                    teamnames.Add(foundname);
                    tID.Add(dataReader.GetInt32("id"));
                    location.Add(dataReader.GetInt32("location"));
                    rank.Add(dataReader.GetInt32("rank"));
                    plrCount.Add(dataReader.GetByte("plrCount"));
                    apps.Add(dataReader.GetInt32("apps"));
                    wins.Add(dataReader.GetInt32("wins"));
                    draws.Add(dataReader.GetInt32("draws"));
                    losses.Add(dataReader.GetInt32("losses"));
                }
            }
            dataReader.Close();
            #endregion

            //**********************************
            //*********** write data  **********
            //**********************************

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)32);

            outmsg.Write(foundExactUser);
            outmsg.Write(foundExactTeam);

            //players
            outmsg.Write(usernames.Count);

            for (int i = 0; i < usernames.Count; i++)
            {
                outmsg.Write(usernames[i]);
                outmsg.Write(nation[i]);
                outmsg.Write(posUP[i]);
                outmsg.Write(posDown[i]);
                outmsg.Write(posLeft[i]);
                outmsg.Write(posRight[i]);
                outmsg.Write(plrTeamname[i]);
                outmsg.Write(careerTotalApps[i]);
                outmsg.Write(careerTotalGoals[i]);
                outmsg.Write(careerTotalAsts[i]);
                outmsg.Write(careerTotalTeamGoals[i]);
                outmsg.Write(practiseGoals[i]);
                outmsg.Write(practiseAssists[i]);
                outmsg.Write(practiseTeamGoals[i]);
            }

            //teams
            outmsg.Write(teamnames.Count);

            for (int i = 0; i < teamnames.Count; i++)
            {
                outmsg.Write(teamnames[i]);
                outmsg.Write(location[i]);
                outmsg.Write(rank[i]);
                outmsg.Write(plrCount[i]);
                outmsg.Write(apps[i]);
                outmsg.Write(wins[i]);
                outmsg.Write(draws[i]);
                outmsg.Write(losses[i]);
            }

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user requests player stats
        void Packet33(NetIncomingMessage inmsg)
        {
            byte sorting;
            string sortStr = "";

            try { sorting = inmsg.ReadByte(); }
            catch { return; }

            if (sorting == 1) sortStr = "users.careerApps";
            if (sorting == 2) sortStr = "users.careerGoals";
            if (sorting == 3) sortStr = "users.careerAsts";
            if (sorting == 4) sortStr = "users.careerTeamGoals";
            if (sorting == 5) sortStr = "users.practiseGoals";
            if (sorting == 6) sortStr = "users.practiseAssists";
            if (sorting == 7) sortStr = "users.practiseTeamGoals";


            if (sortStr == "") return;

            //***************************

            List<string> username = new List<string>();
            List<string> teamname = new List<string>();
            List<string> nation = new List<string>();
            List<int> posUP = new List<int>();
            List<int> posDown = new List<int>();
            List<int> posLeft = new List<int>();
            List<int> posRight = new List<int>();
            List<int> practiseGoals = new List<int>();
            List<int> practiseAssists = new List<int>();
            List<int> practiseTeamGoals = new List<int>();
            List<int> careerTeamGoals = new List<int>();
            List<int> careerApps = new List<int>();
            List<int> careerGoals = new List<int>();
            List<int> careerAsts = new List<int>();

            MySqlConnection mySqlConnection = OpenSQL();

            //get team data
            MySqlCommand cmd = new MySqlCommand("SELECT " +
                "users.username," +
                "users.nation," +
                "users.posUp," +
                "users.posDown," +
                "users.posLeft," +
                "users.posRight," +
                "users.practiseGoals," +
                "users.practiseAssists," +
                "users.practiseTeamGoals," +
                "users.careerTeamGoals," +
                "users.careerApps," +
                "users.careerGoals," +
                "users.careerAsts," +
                "teams.name " +
                "FROM users  " +
                "LEFT JOIN teams ON teams.id = users.teamID  " +
                "GROUP BY users.id  " +
                "ORDER BY " + sortStr + " DESC " +
                "LIMIT 50"
                , mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                username.Add(dataReader.GetString("username"));

                if (dataReader["name"] != DBNull.Value)
                    teamname.Add(dataReader.GetString("name"));
                else
                    teamname.Add("");

                nation.Add(dataReader.GetString("nation"));
                posUP.Add(dataReader.GetInt32("posUP"));
                posDown.Add(dataReader.GetInt32("posDown"));
                posLeft.Add(dataReader.GetInt32("posLeft"));
                posRight.Add(dataReader.GetInt32("posRight"));
                practiseGoals.Add(dataReader.GetInt32("practiseGoals"));
                practiseAssists.Add(dataReader.GetInt32("practiseAssists"));
                practiseTeamGoals.Add(dataReader.GetInt32("practiseTeamGoals"));
                careerTeamGoals.Add(dataReader.GetInt32("careerTeamGoals"));
                careerApps.Add(dataReader.GetInt32("careerApps"));
                careerGoals.Add(dataReader.GetInt32("careerGoals"));
                careerAsts.Add(dataReader.GetInt32("careerAsts"));
            }
            dataReader.Close();

            //write data out

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)33);

            outmsg.Write(username.Count);
            for (int i = 0; i < username.Count; i++)
            {
                outmsg.Write(username[i]);
                outmsg.Write(nation[i]);
                outmsg.Write(teamname[i]);

                outmsg.Write(posUP[i]);
                outmsg.Write(posDown[i]);
                outmsg.Write(posLeft[i]);
                outmsg.Write(posRight[i]);

                outmsg.Write(practiseGoals[i]);
                outmsg.Write(practiseAssists[i]);
                outmsg.Write(practiseTeamGoals[i]);

                outmsg.Write(careerTeamGoals[i]);
                outmsg.Write(careerApps[i]);
                outmsg.Write(careerGoals[i]);
                outmsg.Write(careerAsts[i]);
            }

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user requests team stats
        void Packet34(NetIncomingMessage inmsg)
        {
            byte sorting;
            string sortStr = "";

            try { sorting = inmsg.ReadByte(); }
            catch { return; }

            if (sorting == 1) sortStr = "rank";
            if (sorting == 2) sortStr = "apps";
            if (sorting == 3) sortStr = "wins";
            if (sorting == 4) sortStr = "draws";
            if (sorting == 5) sortStr = "losses";

            if (sortStr == "") return;

            //***************************

            List<string> name = new List<string>();
            List<int> location = new List<int>();
            List<int> rank = new List<int>();
            List<int> apps = new List<int>();
            List<int> wins = new List<int>();
            List<int> draws = new List<int>();
            List<int> losses = new List<int>();

            MySqlConnection mySqlConnection = OpenSQL();

            //get team data
            MySqlCommand cmd = new MySqlCommand("SELECT " +
                "name," +
                "location," +
                "rank," +
                "apps," +
                "wins," +
                "draws," +
                "losses " +
                "FROM teams " +
                "ORDER BY " + sortStr + " DESC " +
                "LIMIT 20"
                , mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                name.Add(dataReader.GetString("name"));
                location.Add(dataReader.GetInt32("location"));
                rank.Add(dataReader.GetInt32("rank"));
                apps.Add(dataReader.GetInt32("apps"));
                wins.Add(dataReader.GetInt32("wins"));
                draws.Add(dataReader.GetInt32("draws"));
                losses.Add(dataReader.GetInt32("losses"));
            }
            dataReader.Close();

            //write data out

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)34);

            outmsg.Write(name.Count);
            for (int i = 0; i < name.Count; i++)
            {
                outmsg.Write(name[i]);
                outmsg.Write(location[i]);
                outmsg.Write(rank[i]);
                outmsg.Write(apps[i]);
                outmsg.Write(wins[i]);
                outmsg.Write(draws[i]);
                outmsg.Write(losses[i]);
            }

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user requests team stats records
        void Packet35(NetIncomingMessage inmsg)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)35);

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
        }

        //team have sent invite to user
        void Packet36(NetIncomingMessage inmsg, UserConnection user)
        {
            if (user.pID == 0) return;
            if (user.tID == 0) return;

            string username;

            try { username = inmsg.ReadString(); }
            catch { return; }

            username = username.Trim();
            if (username == "") return;

            //verifying done

            MySqlCommand cmd;
            MySqlDataReader dataReader;
            MySqlConnection mySqlConnection = OpenSQL();
            byte admas = 0;
            int inviteID = 0;
            int tID1 = 0; //invitor
            int tID2 = 0; //user to be invited
            bool alreadyInvited = false;

            //lets get invitors data
            cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + user.pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                admas = dataReader.GetByte("admas");
                tID1 = dataReader.GetInt32("teamID");
            }
            dataReader.Close();

            if (admas < 2 || tID1 == 0)
            {
                mySqlConnection.Close();
                return;
            }

            //lets get data for player, who is invited
            cmd = new MySqlCommand("SELECT * FROM users WHERE username='" + MySqlHelper.EscapeString(username) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                inviteID = dataReader.GetInt32("id");
                tID2 = dataReader.GetInt32("teamID");
            }
            dataReader.Close();

            if (inviteID == 0 || tID1 == tID2)
            {
                mySqlConnection.Close();
                return;
            }

            //lets check, that team havent already invited user
            cmd = new MySqlCommand("SELECT * FROM invites WHERE pID=" + inviteID + " AND tID=" + tID1, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                alreadyInvited = true;
            }
            dataReader.Close();

            if (alreadyInvited)
            {
                mySqlConnection.Close();
                return;
            }

            //check, that team havent already max 50 invites
            int invitesCount = 0;

            cmd = new MySqlCommand("SELECT count(id) FROM invites WHERE tid=" + tID1, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                invitesCount = dataReader.GetInt32("count(id)");
            }
            dataReader.Close();

            if (invitesCount >= 50)
            {
                SendInfoMsg(inmsg.SenderConnection, 4);
                mySqlConnection.Close();
                return;
            }

            cmd = new MySqlCommand("INSERT INTO invites SET pID=" + inviteID + ",tID=" + tID1, mySqlConnection);
            cmd.ExecuteNonQuery();

            mySqlConnection.Close();

            //lets inform possible online user about invite
            NetOutgoingMessage outmsg = clientToDS.client.CreateMessage();
            outmsg.Write((byte)57);
            outmsg.Write(inviteID);
            clientToDS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);

        }

        //user responds to invite (accept/reject)
        void Packet37(NetIncomingMessage inmsg, UserConnection user)
        {
            if (user.pID == 0) return;

            string teamname;
            byte accRej;

            try { teamname = inmsg.ReadString(); }
            catch { return; }
            try { accRej = inmsg.ReadByte(); }
            catch { return; }

            teamname = teamname.Trim();
            if (teamname == "") return;

            if (accRej < 1) return;
            if (accRej > 2) return;

            if (accRej == 1 && user.tID > 0) return;

            //verifying done

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;
            int tID = 0;
            int plrCount = 0;
            bool inviteFound = false;

            #region lets find tID for invite by teamname
            cmd = new MySqlCommand("SELECT id FROM teams WHERE name='" + MySqlHelper.EscapeString(teamname) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            if (tID == 0)
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region lets check, that there is room in team (max 50 players)
            if (accRej == 1)
            {
                cmd = new MySqlCommand("SELECT count(id) FROM users WHERE teamID=" + tID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    plrCount = dataReader.GetInt32("count(id)");
                }
                dataReader.Close();

                if (plrCount >= 50)
                {
                    SendInfoMsg(inmsg.SenderConnection, 5);
                    mySqlConnection.Close();
                    return;
                }
            }
            #endregion

            #region lets check, that invite really exists (this is against haxers) *******
            cmd = new MySqlCommand("SELECT * FROM invites WHERE pID=" + user.pID + " AND tID=" + tID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                inviteFound = true;
            }
            dataReader.Close();

            if (!inviteFound)
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region lets delete invite
            cmd = new MySqlCommand("DELETE FROM invites WHERE pID=" + user.pID + " AND tID=" + tID, mySqlConnection);
            cmd.ExecuteNonQuery();

            //with reject, we dont need to proceed futher
            if (accRej == 2)
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region check, if player have played previously in team. if not, create new career entry

            //try find old career ID
            int oldCareerID = 0;
            cmd = new MySqlCommand("SELECT id FROM careers WHERE tID=" + tID + " and pID=" + user.pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                oldCareerID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            //old career ID found, so lets just update joined value
            if (oldCareerID > 0)
            {
                cmd = new MySqlCommand("UPDATE careers SET " +
                    "joined=NOW() " +
                    "WHERE id=" + oldCareerID
                    , mySqlConnection);
                cmd.ExecuteNonQuery();
            }

            //no previous career ID found, so lets add new
            if (oldCareerID == 0)
            {
                cmd = new MySqlCommand("INSERT INTO careers SET " +
                    "pID=" + user.pID + ", " +
                    "tID=" + tID + ", " +
                    "joined=NOW()"
                    , mySqlConnection);
                cmd.ExecuteNonQuery();
            }


            #endregion

            #region update some things... *****
            cmd = new MySqlCommand("UPDATE users SET " +
                "teamID=" + tID + ", " +
                "admas=0, " +
                "LFT=0 " +
                "WHERE id=" + user.pID
                , mySqlConnection);
            cmd.ExecuteNonQuery();
            #endregion

            user.tID = tID;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)37);
            outmsg.Write(tID);
            outmsg.Write(teamname);
            outmsg.Write((byte)0);  //admas
            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user leaves team
        void Packet38(NetIncomingMessage inmsg, UserConnection user)
        {
            if (user.pID == 0) return;
            if (user.tID == 0) return;

            int plrCount = 0;
            MySqlCommand cmd;
            MySqlDataReader dataReader;
            MySqlConnection mySqlConnection = OpenSQL();

            int season = GetSeason(mySqlConnection);

            #region get teams player count
            cmd = new MySqlCommand("SELECT count(id) FROM users WHERE teamID=" + user.tID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                plrCount = dataReader.GetInt32("count(id)");
            }
            dataReader.Close();
            #endregion

            #region get user admas
            byte admas = 0;
            cmd = new MySqlCommand("SELECT admas FROM users WHERE id=" + user.pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                admas = dataReader.GetByte("admas");
            }
            dataReader.Close();
            #endregion

            //owner cant leave team, if there is other players in team
            if (admas == 3 && plrCount > 1)
            {
                SendInfoMsg(inmsg.SenderConnection, 6);
                mySqlConnection.Close();
                return;
            }

            cmd = new MySqlCommand("UPDATE users set teamID=0, admas=0, LFT=1 WHERE id=" + user.pID, mySqlConnection);
            cmd.ExecuteNonQuery();

            //if leaving player were last player in team, delete team and invites
            if (plrCount == 1)
            {
                #region get team data & remove from league

                int location = 0;
                int division = 0;
                int _group = 0;

                cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + user.tID, mySqlConnection);
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
                        for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                            int.TryParse(arrayStr[i], out tIDs[i]);

                    }
                    dataReader.Close();

                    //remove tID 
                    for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                        if (tIDs[i] == user.tID)
                        {
                            tIDs[i] = 0;
                            break;
                        }

                    //generate new tIDs string
                    tIDstring = "";
                    for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                        tIDstring += tIDs[i] + ",";
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

                cmd = new MySqlCommand("DELETE FROM teams WHERE id=" + user.tID, mySqlConnection);
                cmd.ExecuteNonQuery();
                cmd = new MySqlCommand("DELETE FROM invites WHERE tID=" + user.tID, mySqlConnection);
                cmd.ExecuteNonQuery();

                #region delete matches (where also opponent team is deleted)
                List<int> matchIDs = new List<int>();
                List<int> matchOppTID = new List<int>();

                cmd = new MySqlCommand("SELECT id,tID0,tID1 FROM matches WHERE tID0=" + user.tID + " OR tID1=" + user.tID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    matchIDs.Add(dataReader.GetInt32("id"));
                    if (dataReader.GetInt32("tID0") == user.tID)
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

            //delete career entry, if player didnt play any matches
            cmd = new MySqlCommand("DELETE FROM careers WHERE apps=0 AND pID=" + user.pID, mySqlConnection);
            cmd.ExecuteNonQuery();

            user.tID = 0;

            SendInfoMsg(inmsg.SenderConnection, 7);
            mySqlConnection.Close();
        }

        //user changes admas status
        void Packet39(NetIncomingMessage inmsg, UserConnection user)
        {
            byte newAdmas;
            string username;

            try { newAdmas = inmsg.ReadByte(); }
            catch { return; }
            try { username = inmsg.ReadString(); }
            catch { return; }

            if (newAdmas > 3) return;
            if (username == "") return;
            if (user.pID == 0) return;
            if (user.tID == 0) return;

            //data verified

            int tID1 = 0;    //changer tID
            byte admas1 = 0;  //changer admas

            int tID2 = 0;    //user to be changed tID            
            int pID = 0;     //user to be changed pID  
            byte admas2 = 0; //user to be changed admas

            MySqlConnection mySqlConnection = OpenSQL();

            //get changers tID and admas
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + user.pID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tID1 = dataReader.GetInt32("teamID");
                admas1 = dataReader.GetByte("admas");
            }
            dataReader.Close();

            //if changer isnt in team, or changer is less than master, quit (HAX attemp)
            if (tID1 == 0 || admas1 < 2)
            {
                mySqlConnection.Close();
                return;
            }

            //get pID & tID from user, which admas is going to be changed
            cmd = new MySqlCommand("SELECT * FROM users WHERE username='" + MySqlHelper.EscapeString(username) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tID2 = dataReader.GetInt32("teamID");
                pID = dataReader.GetInt32("id");
                admas2 = dataReader.GetByte("admas");
            }
            dataReader.Close();

            //check, that both users are in same team. and other user exists
            if (tID1 != tID2 || pID == 0)
            {
                mySqlConnection.Close();
                return;
            }

            //owner cant change his own status
            if (user.pID == pID && admas1 == 3)
            {
                mySqlConnection.Close();
                return;
            }

            //master or lower cant change status to owner
            if (admas1 < 3 && newAdmas == 3)
            {
                mySqlConnection.Close();
                return;
            }

            //nobody cant change owners status
            if (admas2 == 3)
            {
                mySqlConnection.Close();
                return;
            }

            //owner passes his role to other member. owners status will be decreased to master
            if (admas1 == 3 && newAdmas == 3)
            {
                cmd = new MySqlCommand("UPDATE users SET admas=2 WHERE id=" + user.pID, mySqlConnection);
                cmd.ExecuteNonQuery();
            }

            //modify 'user to be changed'
            cmd = new MySqlCommand("UPDATE users SET admas=" + newAdmas + " WHERE id=" + pID, mySqlConnection);
            cmd.ExecuteNonQuery();
            mySqlConnection.Close();


        }

        //user is kicked from team
        void Packet40(NetIncomingMessage inmsg, UserConnection user)
        {
            string username;

            try { username = inmsg.ReadString(); }
            catch { return; }

            if (username == "") return;
            if (user.pID == 0) return;
            if (user.tID == 0) return;

            //data verified

            int pID = 0;
            int tID = 0;
            byte kickerAdmas = 0;
            byte kickedAdmas = 0;

            MySqlConnection mySqlConnection = OpenSQL();

            //lets get kickers data
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + user.pID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                kickerAdmas = dataReader.GetByte("admas");
            }
            dataReader.Close();

            //lets get data for player, who's about to get kicked
            cmd = new MySqlCommand("SELECT * FROM users WHERE username='" + MySqlHelper.EscapeString(username) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                pID = dataReader.GetInt32("id");
                tID = dataReader.GetInt32("teamID");
                kickedAdmas = dataReader.GetByte("admas");
            }
            dataReader.Close();

            if (pID == 0 || user.tID != tID || user.pID == pID || kickedAdmas == 3 || kickerAdmas < 2)
            {
                mySqlConnection.Close();
                return;
            }


            cmd = new MySqlCommand("DELETE FROM careers WHERE apps=0 AND pID=" + pID, mySqlConnection);
            cmd.ExecuteNonQuery();

            cmd = new MySqlCommand("UPDATE users SET teamID=0, admas=0 WHERE id=" + pID, mySqlConnection);
            cmd.ExecuteNonQuery();


            //modify dynamic data (user can be also in other masterserver or offline)
            UserConnection _user = users.Find(p => p.pID == pID);
            if (_user != null)
                _user.tID = 0;
            Console.WriteLine("TODO! We should send new data to kicked player, if he is online");


            mySqlConnection.Close();
        }

        //invite cancel
        void Packet41(NetIncomingMessage inmsg, UserConnection user)
        {
            string username;

            try { username = inmsg.ReadString(); }
            catch { return; }

            if (username == "") return;
            if (user.pID == 0) return;
            if (user.tID == 0) return;

            //data verified

            byte admas = 0;
            int invitePID = 0;
            int id = 0;

            MySqlConnection mySqlConnection = OpenSQL();

            //lets get invitors data
            MySqlCommand cmd = new MySqlCommand("SELECT admas FROM users WHERE id=" + user.pID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                admas = dataReader.GetByte("admas");
            }
            dataReader.Close();

            //lets get data for player, who's invite is about to get cancelled
            cmd = new MySqlCommand("SELECT id FROM users WHERE username='" + MySqlHelper.EscapeString(username) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                invitePID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            //lets check, that invite actially is in invites table
            cmd = new MySqlCommand("SELECT id FROM invites WHERE pID=" + invitePID + " AND tID=" + user.tID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                id = dataReader.GetInt32("id");
            }
            dataReader.Close();

            if (admas < 2 || invitePID == 0 || id == 0)
            {
                mySqlConnection.Close();
                return;
            }

            cmd = new MySqlCommand("DELETE FROM invites WHERE id=" + id, mySqlConnection);
            cmd.ExecuteNonQuery();


            mySqlConnection.Close();
        }

        //user requests results
        void Packet42(NetIncomingMessage inmsg)
        {
            string teamname;
            int page;

            try { teamname = inmsg.ReadString(); }
            catch { return; }
            try { page = inmsg.ReadInt32(); }
            catch { return; }

            if (teamname == "") return;
            if (page < 0) return;

            //data verified

            MySqlConnection mySqlConnection = OpenSQL();

            int maxItems = 14; //incase we change max item (prefabs) count in client side, modify this
            int startID = page * maxItems;
            bool isThereNextPage = false;
            int matchesFound = 0;
            int[] _dateTimeArr;

            //get team data
            int tID = 0;
            MySqlCommand cmd = new MySqlCommand("SELECT id FROM teams WHERE name='" + MySqlHelper.EscapeString(teamname) + "'", mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            if (tID == 0)
            {
                mySqlConnection.Close();
                return;
            }

            List<int> id = new List<int>();
            List<DateTime> time = new List<DateTime>();
            List<int> tID1 = new List<int>();
            List<int> tID2 = new List<int>();
            List<byte> score1 = new List<byte>();
            List<byte> score2 = new List<byte>();
            List<Int16> rankChange1 = new List<Int16>();
            List<Int16> rankChange2 = new List<Int16>();
            List<string> teamname1 = new List<string>();
            List<string> teamname2 = new List<string>();
            List<byte> detailsAvailable = new List<byte>();

            cmd = new MySqlCommand("SELECT " +
                "matches.id," +
                "matches.tID0," +
                "matches.tID1," +
                "matches.time," +
                "matches.score0," +
                "matches.score1," +
                "matches.detailsAvailable," +
                "matches.rankChange0," +
                "matches.rankChange1 " +
                "FROM matches " +
                "WHERE matches.tID0=" + tID + " OR matches.tID1=" + tID + " " +
                "ORDER BY matches.time DESC " +
                "LIMIT " + (maxItems + 1) + " OFFSET " + startID
                , mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                id.Add(dataReader.GetInt32("id"));
                time.Add(dataReader.GetDateTime("time"));
                tID1.Add(dataReader.GetInt32("tID0"));
                tID2.Add(dataReader.GetInt32("tID1"));
                score1.Add(dataReader.GetByte("score0"));
                score2.Add(dataReader.GetByte("score1"));
                detailsAvailable.Add(dataReader.GetByte("detailsAvailable"));
                rankChange1.Add(dataReader.GetInt16("rankChange0"));
                rankChange2.Add(dataReader.GetInt16("rankChange1"));
                teamname1.Add("");
                teamname2.Add("");

                matchesFound++;
                if (matchesFound > maxItems) isThereNextPage = true;
            }
            dataReader.Close();

            //get teamnames for teams
            for (int i = 0; i < id.Count; i++)
            {
                cmd = new MySqlCommand("SELECT name FROM teams WHERE id=" + tID1[i], mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    teamname1[i] = dataReader.GetString("name");
                }
                dataReader.Close();

                cmd = new MySqlCommand("SELECT name FROM teams WHERE id=" + tID2[i], mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    teamname2[i] = dataReader.GetString("name");
                }
                dataReader.Close();
            }

            //************************************
            //*******  write data  ***************
            //************************************

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)42);

            outmsg.Write(isThereNextPage);

            int _sendCound;
            if (isThereNextPage)
                _sendCound = maxItems;
            else
                _sendCound = id.Count;

            //player data
            outmsg.Write(_sendCound);
            for (int i = 0; i < _sendCound; i++)
            {
                outmsg.Write(id[i]);
                outmsg.Write(score1[i]);
                outmsg.Write(score2[i]);
                outmsg.Write(detailsAvailable[i]);
                outmsg.Write(rankChange1[i]);
                outmsg.Write(rankChange2[i]);
                outmsg.Write(teamname1[i]);
                outmsg.Write(teamname2[i]);

                _dateTimeArr = DateTimeToArray(time[i]);
                for (int j = 0; j < 6; j++)
                    outmsg.Write(_dateTimeArr[j]);
            }

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user request details about specific match
        void Packet43(NetIncomingMessage inmsg)
        {
            int matchID;

            try { matchID = inmsg.ReadInt32(); }
            catch { return; }

            //data verified

            int[] tID = new int[2];
            string[] teamname = new string[2];
            byte[] score = new byte[2];
            byte[] goalKicks = new byte[2];
            byte[] corners = new byte[2];
            byte[] throwIns = new byte[2];
            byte[] possession = new byte[2];
            byte[] offsidesTeam = new byte[2];
            byte[] shotsTotalTeam = new byte[2];
            byte[] shotsOnTargetTeam = new byte[2];

            string[] sArr;
            int _i;
            byte _b;
            int id = 0;

            List<int> goals = new List<int>();
            List<int> assists = new List<int>();
            List<byte> goalTime = new List<byte>();
            List<byte> teamScored = new List<byte>();

            List<int> pIDs = new List<int>();
            List<byte> tIDs = new List<byte>();
            List<string> username = new List<string>();
            List<byte> timePlayed = new List<byte>();
            List<byte> shotsTotal = new List<byte>();
            List<byte> shotsOnTarget = new List<byte>();
            List<byte> offsides = new List<byte>();
            List<int> posUp = new List<int>();
            List<int> posDown = new List<int>();
            List<int> posLeft = new List<int>();
            List<int> posRight = new List<int>();
            List<string> nation = new List<string>();

            MySqlConnection mySqlConnection = OpenSQL();

            #region get match data
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM matches WHERE id=" + matchID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                id = dataReader.GetInt32("id");

                for (int i = 0; i < 2; i++)
                {
                    tID[i] = dataReader.GetInt32("tID" + i);
                    score[i] = dataReader.GetByte("score" + i);
                    goalKicks[i] = dataReader.GetByte("goalKicks" + i);
                    corners[i] = dataReader.GetByte("corners" + i);
                    throwIns[i] = dataReader.GetByte("throwIns" + i);
                    possession[i] = dataReader.GetByte("possession" + i);
                    offsidesTeam[i] = dataReader.GetByte("offsides" + i);
                    shotsTotalTeam[i] = dataReader.GetByte("shotsTotal" + i);
                    shotsOnTargetTeam[i] = dataReader.GetByte("shotsOnTarget" + i);
                }

                #region get goals
                //if "goals" is empty, match have ended 0-0
                if (dataReader.GetString("goals") != "")
                {
                    sArr = dataReader.GetString("goals").Split(',');
                    for (int i = 0; i < sArr.Length; i++)
                    {
                        Int32.TryParse(sArr[i], out _i);
                        goals.Add(_i);
                    }
                    sArr = dataReader.GetString("assists").Split(',');
                    for (int i = 0; i < sArr.Length; i++)
                    {
                        Int32.TryParse(sArr[i], out _i);
                        assists.Add(_i);
                    }
                    sArr = dataReader.GetString("goalTime").Split(',');
                    for (int i = 0; i < sArr.Length; i++)
                    {
                        byte.TryParse(sArr[i], out _b);
                        goalTime.Add(_b);
                    }
                    sArr = dataReader.GetString("teamScored").Split(',');
                    for (int i = 0; i < sArr.Length; i++)
                    {
                        byte.TryParse(sArr[i], out _b);
                        teamScored.Add(_b);
                    }
                }
                #endregion

                #region get players
                sArr = dataReader.GetString("pIDs").Split(',');
                for (int i = 0; i < sArr.Length; i++)
                {
                    Int32.TryParse(sArr[i], out _i);
                    pIDs.Add(_i);
                    username.Add("");
                    posUp.Add(0);
                    posDown.Add(0);
                    posLeft.Add(0);
                    posRight.Add(0);
                    nation.Add("");
                }
                sArr = dataReader.GetString("tIDs").Split(',');
                for (int i = 0; i < sArr.Length; i++)
                {
                    byte.TryParse(sArr[i], out _b);
                    tIDs.Add(_b);
                }
                sArr = dataReader.GetString("offsides").Split(',');
                for (int i = 0; i < sArr.Length; i++)
                {
                    byte.TryParse(sArr[i], out _b);
                    offsides.Add(_b);
                }
                sArr = dataReader.GetString("shotsTotal").Split(',');
                for (int i = 0; i < sArr.Length; i++)
                {
                    byte.TryParse(sArr[i], out _b);
                    shotsTotal.Add(_b);
                }
                sArr = dataReader.GetString("shotsOnTarget").Split(',');
                for (int i = 0; i < sArr.Length; i++)
                {
                    byte.TryParse(sArr[i], out _b);
                    shotsOnTarget.Add(_b);
                }
                sArr = dataReader.GetString("timePlayed").Split(',');
                for (int i = 0; i < sArr.Length; i++)
                {
                    byte.TryParse(sArr[i], out _b);
                    timePlayed.Add(_b);
                }
                #endregion
            }
            dataReader.Close();

            if (id == 0)
            {
                mySqlConnection.Close();
                return;
            }

            #endregion

            #region get teamnames
            for (int i = 0; i < 2; i++)
            {
                cmd = new MySqlCommand("SELECT name FROM teams WHERE id=" + tID[i], mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    teamname[i] = dataReader.GetString("name");
                }
                dataReader.Close();
            }
            #endregion

            #region get player data
            for (int i = 0; i < pIDs.Count; i++)
            {
                cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + pIDs[i], mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    username[i] = dataReader.GetString("username");
                    posUp[i] = dataReader.GetInt32("posUp");
                    posDown[i] = dataReader.GetInt32("posDown");
                    posLeft[i] = dataReader.GetInt32("posLeft");
                    posRight[i] = dataReader.GetInt32("posRight");
                    nation[i] = dataReader.GetString("nation");
                }
                dataReader.Close();
            }
            #endregion

            //************************************
            //*******  write data  ***************
            //************************************

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)43);

            //basic data
            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(teamname[i]);
                outmsg.Write(score[i]);
                outmsg.Write(goalKicks[i]);
                outmsg.Write(corners[i]);
                outmsg.Write(throwIns[i]);
                outmsg.Write(possession[i]);
                outmsg.Write(offsidesTeam[i]);
                outmsg.Write(shotsTotalTeam[i]);
                outmsg.Write(shotsOnTargetTeam[i]);
            }

            //goal data
            outmsg.Write(goals.Count);
            for (int i = 0; i < goals.Count; i++)
            {
                outmsg.Write(goals[i]);
                outmsg.Write(assists[i]);
                outmsg.Write(goalTime[i]);
                outmsg.Write(teamScored[i]);
            }

            //player data
            outmsg.Write(pIDs.Count);
            for (int i = 0; i < pIDs.Count; i++)
            {
                outmsg.Write(username[i]);
                outmsg.Write(pIDs[i]);
                outmsg.Write(tIDs[i]);
                outmsg.Write(offsides[i]);
                outmsg.Write(shotsTotal[i]);
                outmsg.Write(shotsOnTarget[i]);
                outmsg.Write(timePlayed[i]);
                outmsg.Write(posUp[i]);
                outmsg.Write(posDown[i]);
                outmsg.Write(posLeft[i]);
                outmsg.Write(posRight[i]);
                outmsg.Write(nation[i]);
            }


            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //chat message
        void Packet45(NetIncomingMessage inmsg, UserConnection user)
        {
            byte receiverType;  //0=user, 1=team, 2=public
            string receiver;
            string message;

            try { receiverType = inmsg.ReadByte(); }
            catch { return; }
            try { receiver = inmsg.ReadString(); }
            catch { return; }
            try { message = inmsg.ReadString(); }
            catch { return; }

            if (receiverType > 2) return;
            if (receiver == "") return;
            if (message == "") return;
            if (user.pID == 0) return;

            string _testText = message.ToLower();

            if (_testText.IndexOf("netsoccer") > -1) return;
            if (_testText.IndexOf("n e t s o c c e r") > -1) return;

            NetOutgoingMessage outmsg = clientToDS.client.CreateMessage();
            outmsg.Write((byte)45);

            outmsg.Write(receiverType);
            outmsg.Write(receiver);
            outmsg.Write(message);
            outmsg.Write(user.pID);
            outmsg.Write(user.tID);

            clientToDS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);

        }

        //user requests data for challenge screen
        void Packet46(NetIncomingMessage inmsg, UserConnection user)
        {
            string oppTeamname;

            try { oppTeamname = inmsg.ReadString(); }
            catch { return; }

            if (oppTeamname == "") return;
            if (user.tID == 0) return;
            if (user.pID == 0) return;

            //data readed

            byte admas = 0;
            int oppTID = 0;
            byte[,] shirtstyle = new byte[2, 2];
            string rgbString;
            string[] arrayStr;
            byte[,] shirtRgb = new byte[2, 24];
            string[] teamnames = new string[2];
            int location = -1; //this is hosting teams location

            MySqlConnection mySqlConnection = OpenSQL();

            #region check hosting users data
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + user.pID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                admas = dataReader.GetByte("admas");
            }
            dataReader.Close();

            if (admas == 0)
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region get opp team data
            cmd = new MySqlCommand("SELECT * FROM teams WHERE name='" + MySqlHelper.EscapeString(oppTeamname) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                teamnames[1] = dataReader.GetString("name");
                oppTID = dataReader.GetInt32("id");
                rgbString = dataReader.GetString("rgb");
                arrayStr = rgbString.Split(',');
                for (int i = 0; i < 24; i++)
                    byte.TryParse(arrayStr[i], out shirtRgb[1, i]);

                for (int i = 0; i < 2; i++)
                    shirtstyle[1, i] = dataReader.GetByte("shirtstyle" + i);
            }
            dataReader.Close();

            if (oppTID == 0 || oppTID == user.tID)
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region get hosting team data
            cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + user.tID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                teamnames[0] = dataReader.GetString("name");
                location = dataReader.GetInt32("location");
                rgbString = dataReader.GetString("rgb");
                arrayStr = rgbString.Split(',');
                for (int i = 0; i < 24; i++)
                    byte.TryParse(arrayStr[i], out shirtRgb[0, i]);

                for (int i = 0; i < 2; i++)
                    shirtstyle[0, i] = dataReader.GetByte("shirtstyle" + i);
            }
            dataReader.Close();
            #endregion

            //write data

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)46);

            outmsg.Write(user.tID);
            outmsg.Write(oppTID);
            outmsg.Write((byte)location);

            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(teamnames[i]);

                for (int j = 0; j < 2; j++)
                    outmsg.Write(shirtstyle[i, j]);

                for (int j = 0; j < 24; j++)
                    outmsg.Write(shirtRgb[i, j]);
            }

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user request to start challenge
        void Packet47(NetIncomingMessage inmsg, UserConnection user)
        {

            int[] tID = new int[2];
            byte[] selectedKit = new byte[2];
            byte location;
            bool botsEnabled;
            byte maxPlayers;

            for (int i = 0; i < 2; i++)
            {
                try { tID[i] = inmsg.ReadInt32(); }
                catch { return; }
                try { selectedKit[i] = inmsg.ReadByte(); }
                catch { return; }
            }

            try { location = inmsg.ReadByte(); }
            catch { return; }
            try { botsEnabled = inmsg.ReadBoolean(); }
            catch { return; }
            try { maxPlayers = inmsg.ReadByte(); }
            catch { return; }

            if (user.pID == 0) return;
            if (user.tID == 0) return;
            if (tID[0] == tID[1]) return;
            if (tID[0] == 0 || tID[1] == 0) return;

            //data readed

            byte admas = 0;
            int hosterTID = 0;
            string teamname;

            MySqlConnection mySqlConnection = OpenSQL();

            #region check admas
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + user.pID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                admas = dataReader.GetByte("admas");
                hosterTID = dataReader.GetInt32("teamID");
            }
            dataReader.Close();

            if (admas == 0 || hosterTID != tID[0])
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region check that team exists (home)
            teamname = "";
            cmd = new MySqlCommand("SELECT name FROM teams WHERE id=" + tID[0], mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                teamname = dataReader.GetString("name");
            }
            dataReader.Close();

            if (teamname == "")
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region check that team exists (away)
            teamname = "";
            cmd = new MySqlCommand("SELECT name FROM teams WHERE id=" + tID[1], mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                teamname = dataReader.GetString("name");
            }
            dataReader.Close();

            if (teamname == "")
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            //write data

            NetOutgoingMessage outmsg = clientToDS.client.CreateMessage();
            outmsg.Write((byte)47);

            for (int i = 0; i < 2; i++)
            {
                outmsg.Write(tID[i]);
                outmsg.Write(selectedKit[i]);
            }

            outmsg.Write(location);
            outmsg.Write(botsEnabled);
            outmsg.Write(maxPlayers);

            clientToDS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();

        }

        //user modifies player (in user->settings)
        void Packet50(NetIncomingMessage inmsg, UserConnection user)
        {
            byte skin, hair, body;
            /*bool LFT;
            byte LFTByte = 0;*/

            try { skin = inmsg.ReadByte(); }
            catch { return; }
            try { hair = inmsg.ReadByte(); }
            catch { return; }
            try { body = inmsg.ReadByte(); }
            catch { return; }
            //try { LFT = inmsg.ReadBoolean(); }
            //catch { return; }

            if (skin > 1) return;
            if (skin == 0 && hair > 3) return;
            if (skin == 1 && hair > 1) return;
            if (body > 2) return;
            if (user.pID == 0) return;

            //data verified

            // if (LFT) LFTByte = 1;


            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;

            if (!IsVip(user.pID, mySqlConnection))
            {
                mySqlConnection.Close();
                return;
            }

            cmd = new MySqlCommand("UPDATE users SET " +
                "skin=" + skin + ", " +
                "hair=" + hair + ", " +
                "body=" + body + " " +
                "WHERE id=" + user.pID
                , mySqlConnection);
            cmd.ExecuteNonQuery();


            mySqlConnection.Close();


        }

        //user join without invite
        void Packet51(NetIncomingMessage inmsg, UserConnection user)
        {
            string teamname;

            try { teamname = inmsg.ReadString(); }
            catch { return; }

            if (user.pID == 0) return;
            if (user.tID > 0) return;

            //data readed & verified

            int tID = 0;
            int plrCount = 0;
            byte allowJoinWithoutInvite = 0;

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            #region lets find tID by teamname
            cmd = new MySqlCommand("SELECT * FROM teams WHERE name='" + MySqlHelper.EscapeString(teamname) + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tID = dataReader.GetInt32("id");
                allowJoinWithoutInvite = dataReader.GetByte("allowJoinWithoutInvite");
            }
            dataReader.Close();

            if (tID == 0 || allowJoinWithoutInvite == 0)
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region lets check, that there is room in team (max 50 players)
            cmd = new MySqlCommand("SELECT count(id) FROM users WHERE teamID=" + tID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                plrCount = dataReader.GetInt32("count(id)");
            }
            dataReader.Close();

            if (plrCount >= 50)
            {
                SendInfoMsg(inmsg.SenderConnection, 5);
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region delete possible invite
            cmd = new MySqlCommand("DELETE FROM invites WHERE pID=" + user.pID + " AND tID=" + tID, mySqlConnection);
            cmd.ExecuteNonQuery();
            #endregion

            #region check, if player have played previously in team. if not, create new career entry

            //try find old career ID
            int oldCareerID = 0;
            cmd = new MySqlCommand("SELECT id FROM careers WHERE tID=" + tID + " and pID=" + user.pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                oldCareerID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            //old career ID found, so lets just update joined value
            if (oldCareerID > 0)
            {
                cmd = new MySqlCommand("UPDATE careers SET " +
                    "joined=NOW() " +
                    "WHERE id=" + oldCareerID
                    , mySqlConnection);
                cmd.ExecuteNonQuery();
            }

            //no previous career ID found, so lets add new
            if (oldCareerID == 0)
            {
                cmd = new MySqlCommand("INSERT INTO careers SET " +
                    "pID=" + user.pID + ", " +
                    "tID=" + tID + ", " +
                    "joined=NOW()"
                    , mySqlConnection);
                cmd.ExecuteNonQuery();
            }

            #endregion

            #region update some things... *****
            cmd = new MySqlCommand("UPDATE users SET " +
                "teamID=" + tID + ", " +
                "admas=0, " +
                "LFT=0 " +
                "WHERE id=" + user.pID
                , mySqlConnection);
            cmd.ExecuteNonQuery();
            #endregion

            user.tID = tID;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)37);
            outmsg.Write(tID);
            outmsg.Write(teamname);
            outmsg.Write((byte)0);  //admas
            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();

        }

        //user save team settings
        void Packet53(NetIncomingMessage inmsg, UserConnection user)
        {
            byte[] shirtStyle = new byte[2];
            byte[] rgb = new byte[24];
            bool allowJoinWithoutInvite;
            byte allowJoinWithoutInviteByte = 0;

            for (int i = 0; i < 2; i++)
                try { shirtStyle[i] = inmsg.ReadByte(); }
                catch { return; }
            for (int i = 0; i < 24; i++)
                try { rgb[i] = inmsg.ReadByte(); }
                catch { return; }

            try { allowJoinWithoutInvite = inmsg.ReadBoolean(); }
            catch { return; }

            if (user.pID == 0) return;
            if (user.tID == 0) return;

            if (allowJoinWithoutInvite) allowJoinWithoutInviteByte = 1;

            //generate rgb string
            string rgbString = "";
            for (int i = 0; i < 24; i++)
                rgbString += rgb[i] + ",";
            rgbString = rgbString.TrimEnd(new char[] { ',' });

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            # region check, that user is least master
            byte admas = 0;
            cmd = new MySqlCommand("SELECT admas FROM users WHERE id=" + user.pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                admas = dataReader.GetByte("admas");
            }
            dataReader.Close();

            if (admas < 2)
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            cmd = new MySqlCommand("UPDATE teams SET " +
                "shirtstyle0=" + shirtStyle[0] + "," +
                "shirtstyle1=" + shirtStyle[1] + "," +
                "allowJoinWithoutInvite=" + allowJoinWithoutInviteByte + ", " +
                "rgb='" + rgbString + "' " +
                "WHERE id=" + user.tID
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            mySqlConnection.Close();
        }

        //user requests online player count
        void Packet55(NetIncomingMessage inmsg)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)55);
            outmsg.Write(clientToDS.totalOnlinePlayerCount);
            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
        }

        //user requests competition
        void Packet60(NetIncomingMessage inmsg, UserConnection user)
        {
            int location;  //if location is -1, user is requesting his own league
            byte division;
            UInt16 _group;

            try { location = inmsg.ReadInt32(); }
            catch { return; }
            try { division = inmsg.ReadByte(); }
            catch { return; }
            try { _group = inmsg.ReadUInt16(); }
            catch { return; }

            MySqlCommand cmd;
            MySqlDataReader dataReader;
            MySqlConnection mySqlConnection = OpenSQL();

            int season = GetSeason(mySqlConnection);

            bool isUserOwner = false;
            bool userTeamInLeague = false;
            int matchCount = 0;
            int plrCount = 0;

            #region if location is -1, user is requesting own teams league
            if (location == -1)
            {
                if (user.tID == 0)
                {
                    mySqlConnection.Close();
                    return;
                }

                cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + user.tID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    location = dataReader.GetByte("location");

                    if (dataReader["division"] != DBNull.Value)
                    {
                        division = dataReader.GetByte("division");
                        _group = dataReader.GetUInt16("_group");
                    }
                    else
                    {
                        division = 1;
                        _group = 1;
                    }
                }
                dataReader.Close();
            }
            #endregion

            #region check, if join button can be shown

            if (user.tID > 0)
            {
                cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + user.tID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    matchCount = dataReader.GetInt32("apps");
                    if (dataReader["division"] != DBNull.Value)
                        userTeamInLeague = true;
                }
                dataReader.Close();

                cmd = new MySqlCommand("SELECT count(id) FROM users WHERE teamID=" + user.tID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    plrCount = dataReader.GetInt32("count(id)");
                }
                dataReader.Close();

                //users team isnt in league, so lets check, if user is owner and join button can be showed to him
                if (!userTeamInLeague)
                {
                    cmd = new MySqlCommand("SELECT admas FROM users WHERE id=" + user.pID, mySqlConnection);
                    dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        if (dataReader.GetByte("admas") == 3)
                            isUserOwner = true;
                    }
                    dataReader.Close();
                }
            }
            #endregion

            #region get league data
            string[] arrayStr;
            string[] teamnames = new string[Form1.maxTeamsInDiv];
            int[] tIDs = new int[Form1.maxTeamsInDiv];
            byte[] apps = new byte[Form1.maxTeamsInDiv];
            byte[] wins = new byte[Form1.maxTeamsInDiv];
            byte[] draws = new byte[Form1.maxTeamsInDiv];
            byte[] losses = new byte[Form1.maxTeamsInDiv];
            UInt16[] gf = new UInt16[Form1.maxTeamsInDiv];
            UInt16[] ga = new UInt16[Form1.maxTeamsInDiv];
            Int16[] pts = new Int16[Form1.maxTeamsInDiv];
            
            //get tIDs from league
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

            for (int i = 0; i < 12; i++)
            {
                if (tIDs[i] == 0) continue;

                cmd = new MySqlCommand("SELECT name FROM teams WHERE " +
                    "id=" + tIDs[i]
                    , mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    teamnames[i] = dataReader.GetString("name");
                }
                dataReader.Close();
            }
            #endregion

            //write data

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)60);

            outmsg.Write(isUserOwner);
            outmsg.Write(userTeamInLeague);
            outmsg.Write(matchCount);
            outmsg.Write(plrCount);

            outmsg.Write(Form1.MAXDIVISION);
            outmsg.Write(location);
            outmsg.Write(division);
            outmsg.Write(_group);

            for (int i = 0; i < 12; i++)
            {
                outmsg.Write(tIDs[i]);
                outmsg.Write(teamnames[i]);
                outmsg.Write(apps[i]);
                outmsg.Write(wins[i]);
                outmsg.Write(draws[i]);
                outmsg.Write(losses[i]);
                outmsg.Write(gf[i]);
                outmsg.Write(ga[i]);
                outmsg.Write(pts[i]);
            }

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user requests competition fixtures
        void Packet61(NetIncomingMessage inmsg)
        {
            int location;  //if location is -1, user is requesting his own league
            byte division;
            UInt16 _group;

            try { location = inmsg.ReadInt32(); }
            catch { return; }
            try { division = inmsg.ReadByte(); }
            catch { return; }
            try { _group = inmsg.ReadUInt16(); }
            catch { return; }

            MySqlCommand cmd;
            MySqlDataReader dataReader;
            MySqlConnection mySqlConnection = OpenSQL();

            int season = GetSeason(mySqlConnection);

            //data readed

            DateTime serverTime = TimeNow(mySqlConnection);

            #region get results

            List<int> matchID = new List<int>();
            List<int> resultTID0 = new List<int>();
            List<int> resultTID1 = new List<int>();
            List<byte> score0 = new List<byte>();
            List<byte> score1 = new List<byte>();
            List<byte> detailsAvailable = new List<byte>();
            List<DateTime> resulttime = new List<DateTime>();

            cmd = new MySqlCommand("SELECT * FROM matches WHERE " +
                "location=" + location + " AND " +
                "division=" + division + " AND " +
                "_group=" + _group + " AND " +
                "season=" + season
                , mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                matchID.Add(dataReader.GetInt32("id"));
                resultTID0.Add(dataReader.GetInt32("tID0"));
                resultTID1.Add(dataReader.GetInt32("tID1"));
                score0.Add(dataReader.GetByte("score0"));
                score1.Add(dataReader.GetByte("score1"));
                detailsAvailable.Add(dataReader.GetByte("detailsAvailable"));
                resulttime.Add(dataReader.GetDateTime("time"));
            }
            dataReader.Close();

            #endregion

            #region get fixtures

            List<int> tID0 = new List<int>();
            List<int> tID1 = new List<int>();
            List<DateTime> time = new List<DateTime>();

            cmd = new MySqlCommand("SELECT * FROM fixtures WHERE " +
                "location=" + location + " AND " +
                "division=" + division + " AND " +
                "_group=" + _group + " AND " +
                "season=" + season
                , mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tID0.Add(dataReader.GetInt32("tID0"));
                tID1.Add(dataReader.GetInt32("tID1"));
                time.Add(dataReader.GetDateTime("time"));
            }
            dataReader.Close();

            #endregion

            //write data

            int[] _dateTimeArr;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)61);

            //server time now
            _dateTimeArr = DateTimeToArray(serverTime);
            for (int j = 0; j < 6; j++)
                outmsg.Write(_dateTimeArr[j]);

            //results
            outmsg.Write(resultTID0.Count);

            for (int i = 0; i < resultTID0.Count; i++)
            {
                outmsg.Write(matchID[i]);
                outmsg.Write(resultTID0[i]);
                outmsg.Write(resultTID1[i]);
                outmsg.Write(score0[i]);
                outmsg.Write(score1[i]);
                outmsg.Write(detailsAvailable[i]);

                _dateTimeArr = DateTimeToArray(resulttime[i]);
                for (int j = 0; j < 6; j++)
                    outmsg.Write(_dateTimeArr[j]);
            }

            //fixtures
            outmsg.Write(tID0.Count);

            for (int i = 0; i < tID0.Count; i++)
            {
                outmsg.Write(tID0[i]);
                outmsg.Write(tID1[i]);

                _dateTimeArr = DateTimeToArray(time[i]);
                for (int j = 0; j < 6; j++)
                    outmsg.Write(_dateTimeArr[j]);
            }


            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();

        }

        //user have changed logo
        void Packet65(NetIncomingMessage inmsg, UserConnection user)
        {
            if (user.pID == 0) return;

            bool isTeamLogo;
            int numberOfBytes;
            byte[] _byteArray;

            try { isTeamLogo = inmsg.ReadBoolean(); }
            catch { return; }
            try { numberOfBytes = inmsg.ReadInt32(); }
            catch { return; }

            if (numberOfBytes > 100000) return;
            if (numberOfBytes == 0) return;

            _byteArray = new byte[numberOfBytes];

            try { inmsg.SkipPadBits(); }
            catch { return; }
            try { _byteArray = inmsg.ReadBytes(numberOfBytes); }
            catch { return; }

            //data readed

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;
            byte admas = 0;

            if (isTeamLogo)
            {
                if (user.tID == 0) return;

                //only owner can change logo
                cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + user.pID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    admas = dataReader.GetByte("admas");
                }
                dataReader.Close();

                if (admas < 3)
                {
                    mySqlConnection.Close();
                    return;
                }

                cmd = new MySqlCommand("UPDATE teams SET logo=@_byteArray, logoSize=@numberOfBytes WHERE id=" + user.tID, mySqlConnection);
                cmd.Parameters.Add(new MySqlParameter("@_byteArray", _byteArray));
                cmd.Parameters.Add(new MySqlParameter("@numberOfBytes", numberOfBytes));
                cmd.ExecuteNonQuery();
            }
            else
            {
                if (!IsVip(user.pID, mySqlConnection))
                {
                    mySqlConnection.Close();
                    return;
                }

                cmd = new MySqlCommand("UPDATE users SET logo=@_byteArray, logoSize=@numberOfBytes WHERE id=" + user.pID, mySqlConnection);
                cmd.Parameters.Add(new MySqlParameter("@_byteArray", _byteArray));
                cmd.Parameters.Add(new MySqlParameter("@numberOfBytes", numberOfBytes));
                cmd.ExecuteNonQuery();
            }

            SendInfoMsg(inmsg.SenderConnection, 8);
            mySqlConnection.Close();
        }

        //user sets free text
        void Packet66(NetIncomingMessage inmsg, UserConnection user)
        {
            if (user.pID == 0) return;

            bool isTeamText;
            string text;

            try { isTeamText = inmsg.ReadBoolean(); }
            catch { return; }
            try { text = inmsg.ReadString(); }
            catch { return; }

            if (text == null) return;
            if (text.Length > 1000) return;
            if (text.Length == 0) return;

            string _testText = text.ToLower();

            if (_testText.IndexOf("netsoccer") > -1) return;
            if (_testText.IndexOf("n e t s o c c e r") > -1) return;

            //data readed

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;
            byte admas = 0;

            if (isTeamText)
            {
                if (user.tID == 0)
                {
                    mySqlConnection.Close();
                    return;
                }

                //only owner can change text
                cmd = new MySqlCommand("SELECT * FROM users WHERE id=" + user.pID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    admas = dataReader.GetByte("admas");
                }
                dataReader.Close();

                if (admas < 3)
                {
                    mySqlConnection.Close();
                    return;
                }

                cmd = new MySqlCommand("UPDATE teams SET text='" + MySqlHelper.EscapeString(text) + "' WHERE id=" + user.tID, mySqlConnection);
                cmd.ExecuteNonQuery();
            }
            else
            {
                if (!IsVip(user.pID, mySqlConnection))
                {
                    mySqlConnection.Close();
                    return;
                }

                cmd = new MySqlCommand("UPDATE users SET text='" + MySqlHelper.EscapeString(text) + "' WHERE id=" + user.pID, mySqlConnection);
                cmd.ExecuteNonQuery();
            }

            mySqlConnection.Close();
        }

        //user performs adv search players
        void Packet67(NetIncomingMessage inmsg)
        {
            string _nation = "";
            int pos = -1;
            int apps = -1;
            int LFT = -1;
            int online = -1;
            int withoutTeam = -1;

            try { _nation = inmsg.ReadString(); }
            catch { return; }
            try { pos = inmsg.ReadInt32(); }
            catch { return; }
            try { apps = inmsg.ReadInt32(); }
            catch { return; }
            try { LFT = inmsg.ReadInt32(); }
            catch { return; }
            try { online = inmsg.ReadInt32(); }
            catch { return; }
            try { withoutTeam = inmsg.ReadInt32(); }
            catch { return; }


            if (online > 2) return;

            //****  data verified

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            string queryStr = "";


            if (_nation != "")
                if (queryStr == "") queryStr = " WHERE nation='" + _nation + "'"; else queryStr += " AND nation='" + _nation + "'";

            if (pos == 0)
                if (queryStr == "") queryStr = " WHERE posDown>(posUp*1.5)"; else queryStr += " AND posDown>(posUp*1.5)";
            if (pos == 1)
                if (queryStr == "") queryStr = " WHERE (posUp*1.5)>posDown"; else queryStr += " AND (posUp*1.5)>posDown";

            if (online > -1)
            {
                if (online == 0)
                    if (queryStr == "") queryStr = " WHERE DATE_SUB(CURDATE(),INTERVAL 1 DAY) <= users.lastlogin"; else queryStr += " AND DATE_SUB(CURDATE(),INTERVAL 1 DAY) <= users.lastlogin";
                if (online == 1)
                    if (queryStr == "") queryStr = " WHERE DATE_SUB(CURDATE(),INTERVAL 7 DAY) <= users.lastlogin"; else queryStr += " AND DATE_SUB(CURDATE(),INTERVAL 7 DAY) <= users.lastlogin";
                if (online == 2)
                    if (queryStr == "") queryStr = " WHERE DATE_SUB(CURDATE(),INTERVAL 30 DAY) <= users.lastlogin"; else queryStr += " AND DATE_SUB(CURDATE(),INTERVAL 30 DAY) <= users.lastlogin";
            }

            if (LFT > -1)
                if (queryStr == "") queryStr = " WHERE LFT=1"; else queryStr += " AND LFT=1";

            if (withoutTeam > -1)
                if (queryStr == "") queryStr = " WHERE teamID=0"; else queryStr += " AND teamID=0";

            if (apps > -1)
                if (queryStr == "") queryStr = " WHERE careerApps>=" + apps; else queryStr += " AND careerApps>=" + apps;

            queryStr = "SELECT " +
                "users.username," +
                "users.nation," +
                "users.posUp," +
                "users.posDown," +
                "users.posLeft," +
                "users.posRight," +
                "users.careerApps," +
                "users.careerGoals," +
                "users.careerAsts," +
                "users.careerTeamGoals," +
                "users.practiseGoals," +
                "users.practiseAssists," +
                "users.practiseTeamGoals," +
                "teams.name	" +
                "FROM users " +
                "LEFT JOIN teams ON teams.id = users.teamID " +
                queryStr +
                " LIMIT 20";

            List<string> usernames = new List<string>();
            List<string> nation = new List<string>();
            List<int> posUP = new List<int>();
            List<int> posDown = new List<int>();
            List<int> posLeft = new List<int>();
            List<int> posRight = new List<int>();
            List<string> plrTeamname = new List<string>();
            List<int> careerApps = new List<int>();
            List<int> careerGoals = new List<int>();
            List<int> careerAsts = new List<int>();
            List<int> careerTeamGoals = new List<int>();
            List<int> practiseGoals = new List<int>();
            List<int> practiseAssists = new List<int>();
            List<int> practiseTeamGoals = new List<int>();

            cmd = new MySqlCommand(queryStr, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                usernames.Add(dataReader.GetString("username"));
                nation.Add(dataReader.GetString("nation"));
                posUP.Add(dataReader.GetInt32("posUP"));
                posDown.Add(dataReader.GetInt32("posDown"));
                posLeft.Add(dataReader.GetInt32("posLeft"));
                posRight.Add(dataReader.GetInt32("posRight"));
                if (dataReader["name"] != DBNull.Value)
                    plrTeamname.Add(dataReader.GetString("name"));
                else
                    plrTeamname.Add("");
                careerApps.Add(dataReader.GetInt32("careerApps"));
                careerGoals.Add(dataReader.GetInt32("careerGoals"));
                careerAsts.Add(dataReader.GetInt32("careerAsts"));
                careerTeamGoals.Add(dataReader.GetInt32("careerTeamGoals"));

                practiseGoals.Add(dataReader.GetInt32("practiseGoals"));
                practiseAssists.Add(dataReader.GetInt32("practiseAssists"));
                practiseTeamGoals.Add(dataReader.GetInt32("practiseTeamGoals"));
            }
            dataReader.Close();

            //**********************************
            //*********** write data  **********
            //**********************************

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)32);

            outmsg.Write(false);  //foundExactUser
            outmsg.Write(false);  //foundExactTeam

            //players
            outmsg.Write(usernames.Count);

            for (int i = 0; i < usernames.Count; i++)
            {
                outmsg.Write(usernames[i]);
                outmsg.Write(nation[i]);
                outmsg.Write(posUP[i]);
                outmsg.Write(posDown[i]);
                outmsg.Write(posLeft[i]);
                outmsg.Write(posRight[i]);
                outmsg.Write(plrTeamname[i]);
                outmsg.Write(careerApps[i]);
                outmsg.Write(careerGoals[i]);
                outmsg.Write(careerAsts[i]);
                outmsg.Write(careerTeamGoals[i]);
                outmsg.Write(practiseGoals[i]);
                outmsg.Write(practiseAssists[i]);
                outmsg.Write(practiseTeamGoals[i]);
            }

            //teams
            outmsg.Write(0);

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user performs adv search teams
        void Packet68(NetIncomingMessage inmsg)
        {
            int _location;
            int lastActive;
            int allowJoin;
            int matchLeast;
            int rankLeast;

            try { _location = inmsg.ReadInt32(); }
            catch { return; }
            try { lastActive = inmsg.ReadInt32(); }
            catch { return; }
            try { allowJoin = inmsg.ReadInt32(); }
            catch { return; }
            try { matchLeast = inmsg.ReadInt32(); }
            catch { return; }
            try { rankLeast = inmsg.ReadInt32(); }
            catch { return; }

            if (lastActive > 2) return;
            //data readed

            //return;  //temporary disabled

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            string queryStr = "";

            if (_location > -1)
                if (queryStr == "") queryStr = " WHERE location=" + _location; else queryStr += " AND location" + _location;

            if (lastActive > -1)
            {
                if (lastActive == 0)
                    if (queryStr == "") queryStr = " WHERE DATE_SUB(CURDATE(),INTERVAL 1 DAY) <= teams.lastLogin"; else queryStr += " AND DATE_SUB(CURDATE(),INTERVAL 1 DAY) <= teams.lastLogin";
                if (lastActive == 1)
                    if (queryStr == "") queryStr = " WHERE DATE_SUB(CURDATE(),INTERVAL 7 DAY) <= teams.lastLogin"; else queryStr += " AND DATE_SUB(CURDATE(),INTERVAL 7 DAY) <= teams.lastLogin";
                if (lastActive == 2)
                    if (queryStr == "") queryStr = " WHERE DATE_SUB(CURDATE(),INTERVAL 30 DAY) <= teams.lastLogin"; else queryStr += " AND DATE_SUB(CURDATE(),INTERVAL 30 DAY) <= teams.lastLogin";
            }

            if (allowJoin > -1)
                if (queryStr == "") queryStr = " WHERE allowJoinWithoutInvite=1"; else queryStr += " AND allowJoinWithoutInvite=1";

            if (matchLeast > -1)
                if (queryStr == "") queryStr = " WHERE apps>=" + matchLeast; else queryStr += " AND apps>=" + matchLeast;
            if (rankLeast > -1)
                if (queryStr == "") queryStr = " WHERE rank>=" + rankLeast; else queryStr += " AND rank>=" + rankLeast;

            queryStr = "SELECT " +
                "id," +
                "name," +
                "location," +
                "rank," +
                "apps," +
                "wins," +
                "draws," +
                "losses," +
                "plrCount," +
                "allowJoinWithoutInvite " +
                "FROM teams " +
                queryStr +
                " LIMIT 20";

            List<int> tID = new List<int>();
            List<string> teamnames = new List<string>();
            List<int> TPCount = new List<int>();
            List<int> location = new List<int>();
            List<int> rank = new List<int>();
            List<int> apps = new List<int>();
            List<int> wins = new List<int>();
            List<int> draws = new List<int>();
            List<int> losses = new List<int>();

            cmd = new MySqlCommand(queryStr, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tID.Add(dataReader.GetInt32("id"));
                teamnames.Add(dataReader.GetString("name"));
                TPCount.Add(dataReader.GetByte("plrCount"));
                location.Add(dataReader.GetInt32("location"));
                rank.Add(dataReader.GetInt32("rank"));
                apps.Add(dataReader.GetInt32("apps"));
                wins.Add(dataReader.GetInt32("wins"));
                draws.Add(dataReader.GetInt32("draws"));
                losses.Add(dataReader.GetInt32("losses"));
            }
            dataReader.Close();

            //**********************************
            //*********** write data  **********
            //**********************************

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)32);

            outmsg.Write(false);
            outmsg.Write(false);

            //players
            outmsg.Write(0);

            //teams
            outmsg.Write(teamnames.Count);

            for (int i = 0; i < teamnames.Count; i++)
            {
                outmsg.Write(teamnames[i]);
                outmsg.Write(location[i]);
                outmsg.Write(rank[i]);
                outmsg.Write(TPCount[i]);
                outmsg.Write(apps[i]);
                outmsg.Write(wins[i]);
                outmsg.Write(draws[i]);
                outmsg.Write(losses[i]);
            }

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();

        }

        //team wants to join to league
        void Packet69(NetIncomingMessage inmsg, UserConnection user)
        {
            if (user.tID == 0) return;

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            int season = GetSeason(mySqlConnection);

            #region check, that user is owner
            byte admas = 0;

            cmd = new MySqlCommand("SELECT admas FROM users WHERE id=" + user.pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                admas = dataReader.GetByte("admas");
            }
            dataReader.Close();

            if (admas != 3)
            {
                mySqlConnection.Close();
                return;
            }
            #endregion

            #region check, that team isnt already in league and get location

            int location = -1;
            bool isTeamAlreadyInLeague = false;
            int apps = 0;

            cmd = new MySqlCommand("SELECT * FROM teams WHERE id=" + user.tID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                location = dataReader.GetByte("location");
                apps = dataReader.GetInt32("apps");

                if (dataReader["division"] != DBNull.Value)
                    isTeamAlreadyInLeague = true;
            }
            dataReader.Close();

            if (isTeamAlreadyInLeague || location == -1 || apps < 20)
            {
                mySqlConnection.Close();
                return;
            }

            #endregion

            int tID = user.tID;

            #region insert team to league

            int resDiv = GetDivForTeam(mySqlConnection, tID, location);

            //whole league is full
            if (resDiv == 0)
            {
                mySqlConnection.Close();
                Console.WriteLine("LEAGUE IS FULL!!!");
                return;
            }

            int resGroup = GetGroupForTeam(mySqlConnection, tID, location, resDiv);

            //update teams div and group
            cmd = new MySqlCommand("UPDATE teams SET " +
                "division=" + resDiv + ", " +
                "_group=" + resGroup + " " +
                "WHERE id=" + tID
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            string tIDstring = "";
            string[] arrayStr;
            int[] tIDs = new int[Form1.maxTeamsInDiv];

            //get tIDs from league
            cmd = new MySqlCommand("SELECT tIDs FROM league WHERE " +
                "location=" + location + " AND " +
                "division=" + resDiv + " AND " +
                "_group=" + resGroup + " AND " +
                "season=" + season
                , mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                tIDstring = dataReader.GetString("tIDs");
                arrayStr = tIDstring.Split(',');
                for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                    int.TryParse(arrayStr[i], out tIDs[i]);

            }
            dataReader.Close();

            //find empty spot
            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                if (tIDs[i] == 0)
                {
                    tIDs[i] = tID;
                    break;
                }

            //generate new tIDs string
            tIDstring = "";
            for (int i = 0; i < Form1.maxTeamsInDiv; i++)
                tIDstring += tIDs[i] + ",";
            tIDstring = tIDstring.TrimEnd(new char[] { ',' });

            //update tIDs to league
            cmd = new MySqlCommand("UPDATE league SET tIDs='" + tIDstring + "' WHERE " +
                "location=" + location + " AND " +
                "division=" + resDiv + " AND " +
                "_group=" + resGroup + " AND " +
                "season=" + season
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            #endregion

            mySqlConnection.Close();

            SendInfoMsg(inmsg.SenderConnection, 9);
        }

        //user modifies player (in play->settings)
        void Packet70(NetIncomingMessage inmsg, UserConnection user)
        {
            byte skin, hair, body;

            try { skin = inmsg.ReadByte(); }
            catch { return; }
            try { hair = inmsg.ReadByte(); }
            catch { return; }
            try { body = inmsg.ReadByte(); }
            catch { return; }

            if (skin > 1) return;
            if (skin == 0 && hair > 3) return;
            if (skin == 1 && hair > 1) return;
            if (body > 2) return;
            if (user.pID == 0) return;

            //data verified

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;

            if (!IsVip(user.pID, mySqlConnection))
            {
                mySqlConnection.Close();
                return;
            }

            cmd = new MySqlCommand("UPDATE users SET " +
                "skin=" + skin + ", " +
                "hair=" + hair + ", " +
                "body=" + body + " " +
                "WHERE id=" + user.pID
                , mySqlConnection);
            cmd.ExecuteNonQuery();


            mySqlConnection.Close();

        }

        //user have completed reward video or FB liked
        void Packet71(NetIncomingMessage inmsg, UserConnection user)
        {
            bool isFBLike;

            try { isFBLike = inmsg.ReadBoolean(); }
            catch { return; }

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            //deny, that reward video will be added many times
            if (IsVip(user.pID, mySqlConnection))
            {
                mySqlConnection.Close();
                return;
            }

            //check, if fb like already added
            if (isFBLike)
            {
                byte FBLikeAlreadyAdded = 0;
                cmd = new MySqlCommand("SELECT FBlike FROM users WHERE id=" + user.pID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    FBLikeAlreadyAdded = dataReader.GetByte("FBlike");
                }
                dataReader.Close();

                if (FBLikeAlreadyAdded == 1)
                {
                    mySqlConnection.Close();
                    return;
                }
            }

            if (isFBLike)
            {
                AddVip(user.pID, mySqlConnection, 0, 0, 120);
                cmd = new MySqlCommand("UPDATE users set FBlike=1 WHERE id=" + user.pID, mySqlConnection);
                cmd.ExecuteNonQuery();
            }
            else
                AddVip(user.pID, mySqlConnection, 0, 0, 30);

            DateTime vipExpire = new DateTime();

            //get current vip
            cmd = new MySqlCommand("SELECT vipExpire FROM users WHERE id=" + user.pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                vipExpire = dataReader.GetDateTime("vipExpire");
            }
            dataReader.Close();

            //send data

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)71);

            int[] _vipExpireArr = DateTimeToArray(vipExpire);
            for (int i = 0; i < 6; i++)
                outmsg.Write(_vipExpireArr[i]);

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //user changes LFT status
        void Packet72(NetIncomingMessage inmsg, UserConnection user)
        {
            bool LFT;
            byte LFTByte = 0;

            try { LFT = inmsg.ReadBoolean(); }
            catch { return; }

            if (user.pID == 0) return;

            //data verified

            if (LFT) LFTByte = 1;


            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;

            cmd = new MySqlCommand("UPDATE users SET " +
                "LFT=" + LFTByte + " " +
                "WHERE id=" + user.pID
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            mySqlConnection.Close();

        }

        //user sends his version and platform to masterserver
        void Packet73(NetIncomingMessage inmsg, UserConnection user)
        {
            byte platform = 0;
            int version = 0;

            try { platform = inmsg.ReadByte(); }
            catch { return; }
            try { version = inmsg.ReadInt32(); }
            catch { return; }

            user.platform = platform;
            user.version = version;
        }

        //user have completed payment
        void Packet74(NetIncomingMessage inmsg, UserConnection user)
        {
            string strDays;
            int days = 0;

            try { strDays = inmsg.ReadString(); }
            catch { return; }

            if (strDays == "") return;
            if (strDays == "7" || strDays == "vip_7") days = 7;
            if (strDays == "30" || strDays == "vip_30") days = 30;
            if (strDays == "90" || strDays == "vip_90") days = 90;
            if (days == 0) return;

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            AddVip(user.pID, mySqlConnection, days, 0, 0);

            DateTime vipExpire = new DateTime();

            //get current vip
            cmd = new MySqlCommand("SELECT vipExpire FROM users WHERE id=" + user.pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                vipExpire = dataReader.GetDateTime("vipExpire");
            }
            dataReader.Close();

            //send data

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)71);

            int[] _vipExpireArr = DateTimeToArray(vipExpire);
            for (int i = 0; i < 6; i++)
                outmsg.Write(_vipExpireArr[i]);

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();
        }

        //misc click (send plrs from nations)
        void Packet76(NetIncomingMessage inmsg)
        {
            int nationCount = 0;

            for (int i = 0; i < Form1.nationData.Count; i++)
                if (Form1.nationData[i].plrCount > 0)
                    nationCount++;

            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)76);

            outmsg.Write(nationCount);
            for (int i = 0; i < Form1.nationData.Count; i++)
                if (Form1.nationData[i].plrCount > 0)
                {
                    outmsg.Write(Form1.nationData[i].countryCode);
                    outmsg.Write(Form1.nationData[i].plrCount);
                }


            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
        }

        //user sends his version and platform to masterserver and requests username (steam)
        void Packet77(NetIncomingMessage inmsg, UserConnection user)
        {
            byte platform = 0;
            int version = 0;
            ulong steamID = 0;

            try { platform = inmsg.ReadByte(); }
            catch { return; }
            try { version = inmsg.ReadInt32(); }
            catch { return; }
            try { steamID = inmsg.ReadUInt64(); }
            catch { return; }

            user.platform = platform;
            user.version = version;

            byte permaBanned = 0;
            byte banCount = 0;
            DateTime bannedTime = new DateTime();
            int bannedMins = 0;

            List<string> usernames = new List<string>();

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM users WHERE steamID=" + steamID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                usernames.Add(dataReader.GetString("username"));
                permaBanned = dataReader.GetByte("permaBanned");
                banCount = dataReader.GetByte("banCount");
                if (banCount > 0 && banCount < 4)
                    bannedTime = dataReader.GetDateTime("bannedTime");
            }
            dataReader.Close();

            //check, if still banned
            if (permaBanned == 0 && banCount > 0 && banCount < 4)
            {
                DateTime dateTimeNow = TimeNow(mySqlConnection);

                int result = DateTime.Compare(bannedTime, dateTimeNow);

                //ban still active
                if (result > 0)
                    bannedMins = Convert.ToInt32((bannedTime - dateTimeNow).TotalMinutes);
                else
                    bannedMins = 0;
            }

            //permaBanned = 0;//temp!!!
            //bannedMins = 0; //temp!!!

            if (permaBanned == 1 || bannedMins > 0)
            {
                InformAboutBanned(permaBanned, bannedMins, inmsg.SenderConnection);
                mySqlConnection.Close();
                return;
            }

            //send data
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)77);

            outmsg.Write(usernames.Count);

            for (int i = 0; i < usernames.Count; i++)
                outmsg.Write(usernames[i]);

            server.SendMessage(outmsg, inmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();

        }

        //user creates username (steam)
        void Packet78(NetIncomingMessage inmsg, UserConnection user)
        {
            if (user.pID > 0) return;

            string username = "";
            ulong steamID = 0;
            byte platform;

            try { username = inmsg.ReadString(); }
            catch { return; }
            try { steamID = inmsg.ReadUInt64(); }
            catch { return; }
            try { platform = inmsg.ReadByte(); }
            catch { return; }

            if (username.Length > 15) return;
            if (username == "") return;
            //****  data verified  ****

            username = username.Trim();

            MySqlConnection mySqlConnection = OpenSQL();

            int pID = 0;
            MySqlCommand cmd = new MySqlCommand("SELECT id FROM users WHERE username='" + MySqlHelper.EscapeString(username) + "'", mySqlConnection);

            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                pID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            //username already taken
            if (pID > 0)
            {
                SendInfoMsg(inmsg.SenderConnection, 1);
                mySqlConnection.Close();
                return;
            }

            cmd = new MySqlCommand("INSERT INTO users SET " +
                "username=binary'" + MySqlHelper.EscapeString(username) + "', " +
                "password='', " +
                "steamID=" + steamID + ", " +
                "created=NOW()" + ", " +
                "vipExpire=NOW()"
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            LoginThread loginThread = new LoginThread(username, "", steamID, platform, inmsg.SenderConnection, user, mySqlConnection, clientToDS, this);
            loginThread.thread.Start();
            //Login2(username, "", steamID, platform, inmsg.SenderConnection, user, mySqlConnection);
        }

        //user logins with steam username
        void Packet79(NetIncomingMessage inmsg, UserConnection user)
        {
            string username = "";
            ulong steamID = 0;
            byte platform;
            int version;

            try { username = inmsg.ReadString(); }  //username
            catch { return; }
            try { steamID = inmsg.ReadUInt64(); }
            catch { return; }
            try { platform = inmsg.ReadByte(); }
            catch { return; }
            try { version = inmsg.ReadInt32(); }
            catch { return; }

            if (username == "") return;
            if (username.Length > 15) return;
            //****  data verified  ****

            #region lets check, if banned
            byte permaBanned = 0;
            byte banCount = 0;
            DateTime bannedTime = new DateTime();
            int bannedMins = 0;

            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM users WHERE steamID=" + steamID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                permaBanned = dataReader.GetByte("permaBanned");
                banCount = dataReader.GetByte("banCount");
                if (banCount > 0 && banCount < 4)
                    bannedTime = dataReader.GetDateTime("bannedTime");
            }
            dataReader.Close();

            //check, if still banned
            if (permaBanned == 0 && banCount > 0 && banCount < 4)
            {
                DateTime dateTimeNow = TimeNow(mySqlConnection);

                int result = DateTime.Compare(bannedTime, dateTimeNow);

                //ban still active
                if (result > 0)
                    bannedMins = Convert.ToInt32((bannedTime - dateTimeNow).TotalMinutes);
                else
                    bannedMins = 0;
            }

            if (permaBanned == 1 || bannedMins > 0)
            {
                InformAboutBanned(permaBanned, bannedMins, inmsg.SenderConnection);
                mySqlConnection.Close();
                return;
            }
            #endregion

            user.version = version;
            username = username.Trim();

            LoginThread loginThread = new LoginThread(username, "", steamID, platform, inmsg.SenderConnection, user, null, clientToDS, this);
            loginThread.thread.Start();

            //Login(username, "", steamID, platform, inmsg.SenderConnection, user, null);

        }

        //client start purchase process (steam)
        void Packet80(NetIncomingMessage inmsg, UserConnection user)
        {
            ulong steamID = 0;
            string strDays;
            bool ok = false;
            byte days = 0;
            string language;

            try { steamID = inmsg.ReadUInt64(); }
            catch { return; }
            try { strDays = inmsg.ReadString(); }
            catch { return; }
            try { language = inmsg.ReadString(); }
            catch { return; }

            if (steamID == 0) return;
            if (strDays == "") return;
            if (strDays == "7" || strDays == "30" || strDays == "90") ok = true;
            if (!ok) return;

            days = Convert.ToByte(strDays);

            MySqlConnection mySqlConnection = OpenSQL();

            MySqlCommand cmd = new MySqlCommand("INSERT INTO steampurchases SET " +
                "days=" + days + ", " +
                "steamid=" + steamID + ", " +
                "language='" + language + "', " +
                "pID=" + user.pID
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            ulong orderid = (ulong)cmd.LastInsertedId;

            mySqlConnection.Close();

            SteamRequest steamRequest = new SteamRequest();
            steamRequest.steamID = steamID;
            steamRequest.orderid = orderid;
            steamRequest.days = days;
            steamRequest.language = language;

            steamRequest.thread.Start();

        }

        //user sends finalize steam purchase data
        void Packet81(NetIncomingMessage inmsg, UserConnection user)
        {
            uint m_unAppID;
            ulong m_ulOrderID;
            byte m_bAuthorized;

            try { m_unAppID = inmsg.ReadUInt32(); }
            catch { return; }
            try { m_ulOrderID = inmsg.ReadUInt64(); }
            catch { return; }
            try { m_bAuthorized = inmsg.ReadByte(); }
            catch { return; }

            SteamRequest steamRequest = new SteamRequest();
            steamRequest.isFinalize = true;
            steamRequest.orderid = m_ulOrderID;

            steamRequest.thread.Start();
        }

        // /info msg
        void Packet85(NetIncomingMessage inmsg, UserConnection user)
        {
            string username;

            try { username = inmsg.ReadString(); }
            catch { return; }

            if (username == "") return;

            //data readed

            MySqlConnection mySqlConnection = OpenSQL();

            int pID = 0;
            MySqlCommand cmd = new MySqlCommand("SELECT id FROM users WHERE username='" + MySqlHelper.EscapeString(username) + "'", mySqlConnection);

            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                pID = dataReader.GetInt32("id");
            }
            dataReader.Close();

            mySqlConnection.Close();

            if (pID == 0)
            {
                SendInfoMsg(inmsg.SenderConnection, 10);
                return;
            }

            long remoteUniqueIdentifier = inmsg.SenderConnection.RemoteUniqueIdentifier;

            NetOutgoingMessage outmsg = clientToDS.client.CreateMessage();
            outmsg.Write((byte)85);
            outmsg.Write(pID);
            outmsg.Write(username);
            outmsg.Write(remoteUniqueIdentifier);

            clientToDS.client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered, 0);

        }

        void InformAboutBanned(byte permaBanned, int bannedMins, NetConnection conn)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)88);
            outmsg.Write(permaBanned);
            outmsg.Write(bannedMins);

            server.SendMessage(outmsg, conn, NetDeliveryMethod.ReliableOrdered, 0);
        }

        void Login(string username, string password, ulong steamID, byte platform, NetConnection connection, UserConnection user, MySqlConnection mySqlConnection)
        {
            double[] _times = new double[10];

            if (user.pID > 0)
            {
                if (mySqlConnection != null)
                    mySqlConnection.Close();
                return;
            }


            if (mySqlConnection == null)
                mySqlConnection = OpenSQL();

            int pID = -1;
            int teamID = 0;
            DateTime vipExpire = new DateTime();
            byte admas = 0;
            string currentNation = "";
            int _unlimitedVip = 0;
            string teamname = "";
            int inviteCount = 0;
            bool selectFlag = false;

            MySqlCommand cmd;

            if (steamID > 0)
            {
                string MD5Password = MD5Hash(password);
                cmd = new MySqlCommand("SELECT * FROM users WHERE username=binary'"
                    + MySqlHelper.EscapeString(username) + "' AND steamID=" + steamID, mySqlConnection);
            }
            else
            {
                string MD5Password = MD5Hash(password);
                cmd = new MySqlCommand("SELECT * FROM users WHERE username=binary'"
                    + MySqlHelper.EscapeString(username) + "' AND password=binary'"
                    + MySqlHelper.EscapeString(MD5Password) + "'", mySqlConnection);
            }

            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                pID = dataReader.GetInt32("id");
                teamID = dataReader.GetInt32("teamID");
                vipExpire = dataReader.GetDateTime("vipExpire");
                admas = dataReader.GetByte("admas");
                currentNation = dataReader.GetString("nation");
                _unlimitedVip = dataReader.GetByte("unlimitedVip");
            }
            dataReader.Close();

            //invalid username or password
            if (pID == -1)
            {
                SendInfoMsg(connection, 2);
                mySqlConnection.Close();
                return;
            }

            //if users vip have expired, lets reset his attribs
            if (!IsVip(pID, mySqlConnection))
            {
                cmd = new MySqlCommand("UPDATE users SET " +
                    "body=1," +
                    "skin=0," +
                    "hair=1," +
                    "number=0," +
                    "shoe='0,0,0' " +
                    "WHERE id=" + pID
                    , mySqlConnection);
                cmd.ExecuteNonQuery();
            }

            //lets inform team members, that user have logged in
            //if (teamID > 0) Proc.informTeamMembersLogin(teamID, username, connection);

            //if players has team, lets get data
            if (teamID > 0)
            {
                cmd = new MySqlCommand("SELECT * FROM teams where id=" + teamID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    teamname = dataReader.GetString("name");
                }
                dataReader.Close();

                //lets update teams lastLogin and platform
                cmd = new MySqlCommand("UPDATE teams SET " +
                    "lastLogin=NOW()" + " " +
                    "WHERE id=" + teamID
                    , mySqlConnection);
                cmd.ExecuteNonQuery();
            }

            //get invite count
            cmd = new MySqlCommand("SELECT count(id) FROM invites where pID=" + pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                inviteCount = dataReader.GetInt32("count(id)");
            }
            dataReader.Close();

            int registeredUsers = 0;
            int registeredTeams = 0;

            //total registered users
            cmd = new MySqlCommand("SELECT count(id) FROM users", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                registeredUsers = dataReader.GetInt32("count(id)");
            }
            dataReader.Close();

            //total registered teams
            cmd = new MySqlCommand("SELECT count(id) FROM teams", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                registeredTeams = dataReader.GetInt32("count(id)");
            }
            dataReader.Close();


            int uniqueID = GetUniqueID(mySqlConnection);
            //get ip data            
            IPData ipData = new IPData(connection.RemoteEndPoint.Address.ToString(), mySqlConnection);

            //if (ipData.nation == 0 && currentNation < 1) selectFlag = true;

            /////////////////
            //tää on luultavasti aika sekasin, ku muutin nation int->string
            //if ((ipData.nation > 0 && ipData.nation < 231) || ipData.nation == -1)
            cmd = new MySqlCommand("UPDATE users SET " +
                "platform=" + platform + ", " +
                "lastlogin=NOW()" + ", " +
                "uniqueID=" + uniqueID + ", " +
                "nation='" + ipData.nameShort + "', " +
                "IP='" + connection.RemoteEndPoint.Address.ToString() + "' " +
                "WHERE id=" + pID
                , mySqlConnection);
            /*else
                cmd = new MySqlCommand("UPDATE users SET " +
                    "timestamp=" + Time() + ", " +
                    "uniqueID=" + uniqueID + ", " +
                    "IP='" + connection.RemoteEndPoint.Address.ToString() + "' " +
                    "WHERE id=" + pID
                    , mySqlConnection);*/

            cmd.ExecuteNonQuery();
            //////////////////

            //lets insert login data
            /*cmd = new MySqlCommand("INSERT INTO logins SET " +
                "pID=" + pID + ", " +
                "timestamp=" + Proc.time() + ", " +
                "username='" + MySqlHelper.EscapeString(username) + "', " +
                "IP='" + connection.RemoteEndPoint.Address.ToString() + "' "
                , Vars.mySqlConnection);
            cmd.ExecuteNonQuery();*/

            user.username = username;
            user.pID = pID;
            user.tID = teamID;

            int nationCount = 0;

            //send data to client
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)6);

            outmsg.Write(clientToDS.totalOnlinePlayerCount);
            outmsg.Write(registeredUsers);
            outmsg.Write(registeredTeams);
            outmsg.Write(pID);
            outmsg.Write(teamID);
            outmsg.Write(uniqueID);

            int[] _vipExpireArr = DateTimeToArray(vipExpire);
            for (int i = 0; i < 6; i++)
                outmsg.Write(_vipExpireArr[i]);

            //servertime (mySQL)
            DateTime dateTimeNow = TimeNow(mySqlConnection);
            int[] _dateTimeNowArr = DateTimeToArray(dateTimeNow);
            for (int i = 0; i < 6; i++)
                outmsg.Write(_dateTimeNowArr[i]);

            outmsg.Write(inviteCount);
            //outmsg.Write(PlayersByNation.registeredUsers);
            //outmsg.Write(PlayersByNation.registeredTeams);
            outmsg.Write(admas);
            //outmsg.Write(selectFlag);
            outmsg.Write(username);
            outmsg.Write(teamname);

            if (_unlimitedVip == 1)
                outmsg.Write(true);
            else
                outmsg.Write(false);

            server.SendMessage(outmsg, connection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();

        }

        public void SendInfoMsg(NetConnection connection, byte infoType)
        {
            NetOutgoingMessage outmsg = server.CreateMessage();
            outmsg.Write((byte)5);
            outmsg.Write(infoType);
            server.SendMessage(outmsg, connection, NetDeliveryMethod.ReliableOrdered, 0);
        }

        int GetDivForTeam(MySqlConnection mySqlConnection, int tID, int location)
        {
            int resDiv = 0;

            MySqlDataReader dataReader;
            MySqlCommand cmd;

            int groupCount;
            int count = 0;
            int _maxTeamsInDiv = 0;

            for (int div = Form1.MAXDIVISION; div > 0; div--)
            {
                groupCount = 1;

                for (int i = 0; i < div - 1; i++)
                    groupCount *= 2;

                _maxTeamsInDiv = groupCount * Form1.maxTeamsInDiv;

                cmd = new MySqlCommand("SELECT count(id) FROM teams WHERE " +
                    "location=" + location + " AND " +
                    "division=" + div
                    , mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    count = dataReader.GetInt32("count(id)");
                }
                dataReader.Close();

                if (count == 0)
                    resDiv = div;

                if (count == _maxTeamsInDiv)
                    return resDiv;

                if (count < _maxTeamsInDiv && count > 0)
                    return div;

            }

            return resDiv;
        }

        int GetGroupForTeam(MySqlConnection mySqlConnection, int tID, int location, int div)
        {
            MySqlDataReader dataReader;
            MySqlCommand cmd;

            int groupCount = 1;
            int count = 0;

            for (int i = 0; i < div - 1; i++)
                groupCount *= 2;

            for (int curGroup = 1; curGroup < groupCount + 1; curGroup++)
            {
                cmd = new MySqlCommand("SELECT count(id) FROM teams WHERE " +
                    "location=" + location + " AND " +
                    "division=" + div + " AND " +
                    "_group=" + curGroup
                    , mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    count = dataReader.GetInt32("count(id)");
                }
                dataReader.Close();

                if (count < Form1.maxTeamsInDiv)
                    return curGroup;
            }


            return 0;
        }

        public void LoadPlrsByNation()
        {
            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            string country_code = "";
            int plrcount = 0;

            //lets find oldest 'lastUpdated' nation
            cmd = new MySqlCommand("SELECT * FROM plrsbynation", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                country_code = dataReader.GetString("country_code");
                plrcount = dataReader.GetInt32("plrcount");

                for (int i = 0; i < Form1.nationData.Count; i++)
                    if (Form1.nationData[i].countryCode == country_code)
                    {
                        Form1.nationData[i].plrCount = plrcount;
                        break;
                    }

            }
            dataReader.Close();

            mySqlConnection.Close();
        }

        public void AddVip2(int pID, int days, int hours, int minutes)
        {
            AddVip(pID, null, days, hours, minutes);
        }

        public void CountPlrsByNation()
        {
            MySqlConnection mySqlConnection = OpenSQL();
            MySqlCommand cmd;
            MySqlDataReader dataReader;

            string country_code = "";
            int plrcount = 0;

            //lets find oldest 'lastUpdated' nation
            cmd = new MySqlCommand("SELECT * FROM plrsbynation ORDER BY lastUpdated LIMIT 1;", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                country_code = dataReader.GetString("country_code");
            }
            dataReader.Close();

            if (country_code == "")
            {
                mySqlConnection.Close();
                return;
            }

            //lets count users
            cmd = new MySqlCommand("SELECT count(id) FROM users WHERE nation='" + country_code + "'", mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                plrcount = dataReader.GetInt32("count(id)");
                //Form1.nationData[i].plrCount = dataReader.GetInt32("count(id)");
            }
            dataReader.Close();

            for (int i = 0; i < Form1.nationData.Count; i++)
                if (Form1.nationData[i].countryCode == country_code)
                {
                    Form1.nationData[i].plrCount = plrcount;
                    break;
                }

            cmd = new MySqlCommand("UPDATE plrsbynation SET plrcount=" + plrcount + ",lastUpdated=NOW() WHERE country_code='" + country_code + "'", mySqlConnection);
            cmd.ExecuteNonQuery();

            mySqlConnection.Close();
        }

    }
}
