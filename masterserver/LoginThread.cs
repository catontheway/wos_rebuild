using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Lidgren.Network;
using MySql.Data.MySqlClient;

namespace MasterServer
{
    class LoginThread : BaseStuff
    {
        public Thread thread;

        public string username;
        public string password;
        public ulong steamID;
        public byte platform;
        public NetConnection connection;
        public UserConnection user;
        public MySqlConnection mySqlConnection;
        public ClientToDS clientToDS;
        public ServerForU serverForU;

        public LoginThread(string username, string password, ulong steamID, byte platform, NetConnection connection, UserConnection user, MySqlConnection mySqlConnection, ClientToDS clientToDS, ServerForU serverForU)
        {
            this.username = username;
            this.password = password;
            this.steamID = steamID;
            this.platform = platform;
            this.connection = connection;
            this.user = user;
            this.mySqlConnection = mySqlConnection;
            this.clientToDS = clientToDS;
            this.serverForU = serverForU;

            thread = new Thread(new ThreadStart(DoLogin));
        }

        public void DoLogin()
        {
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
                //cmd = new MySqlCommand("SELECT * FROM users WHERE username=binary'"
                 //   + MySqlHelper.EscapeString(username) +"'", mySqlConnection);
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
                if (!dataReader.IsDBNull(0))
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
                serverForU.SendInfoMsg(connection, 2);
                mySqlConnection.Close();
                return;
            }

            //if users vip have expired, lets reset his attribs
            if (!IsVip(pID, mySqlConnection))
            {
                cmd = new MySqlCommand("UPDATE users SET " +
                    "text=''," +
                    "logo=null," +
                    "logoSize=0," +
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

            //check, if user have team,but team doesnt exists (becouse of crash happened earlier, and same time user have leave team)
            //so lets reset teamID
            if (teamID > 0)
            {
                bool teamnameFound = false;

                cmd = new MySqlCommand("SELECT name FROM teams where id=" + teamID, mySqlConnection);
                dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    teamnameFound = true;
                }
                dataReader.Close();

                //team doesnt exists, so lets reset user->teamID
                if (!teamnameFound)
                {
                    cmd = new MySqlCommand("UPDATE users set teamID=0, admas=0, LFT=1 WHERE id=" + pID, mySqlConnection);
                    cmd.ExecuteNonQuery();
                    teamID = 0;
                    admas = 0;
                }

            }

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
            NetOutgoingMessage outmsg = serverForU.server.CreateMessage();
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

            serverForU.server.SendMessage(outmsg, connection, NetDeliveryMethod.ReliableOrdered, 0);
            mySqlConnection.Close();



        }

    }
}
