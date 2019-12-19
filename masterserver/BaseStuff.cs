using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;

namespace MasterServer
{
    class BaseStuff
    {
        public static string mysqlIP = "178.62.197.176";// "178.62.197.176";  "188.166.100.71"
        public static string mysqlPW = "SetSomePassword";

        public BaseStuff()
        {
            if (Form1.isLocal)
            {
                mysqlIP = "localhost";
                mysqlPW = "Radix@123";
            }
        }

        protected int GetSeason(MySqlConnection mySqlConnection)
        {
            bool closeConnection = false;

            if (mySqlConnection == null)
            {
                mySqlConnection = OpenSQL();
                closeConnection = true;
            }

            int _season = 0;

            MySqlCommand cmd = new MySqlCommand("SELECT season FROM nssettings", mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                _season = dataReader.GetInt32("season");
            }
            dataReader.Close();

            if (closeConnection) mySqlConnection.Close();

            return _season;
        }

        protected bool IsVip(int pID, MySqlConnection mySqlConnection)
        {
            bool closeConnection = false;

            if (mySqlConnection == null)
            {
                mySqlConnection = OpenSQL();
                closeConnection = true;
            }

            DateTime vipExpire = new DateTime();
            DateTime now = new DateTime();

            if (IsUnlimitedVip(pID, mySqlConnection)) return true;

            MySqlCommand cmd = new MySqlCommand("SELECT vipExpire,NOW() FROM users where id=" + pID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                vipExpire = dataReader.GetDateTime("vipExpire");
                now = dataReader.GetDateTime("NOW()");
            }
            dataReader.Close();

            if (closeConnection) mySqlConnection.Close();

            int result = DateTime.Compare(now, vipExpire);
            if (result > 0)
                return false;
            else
                return true;

        }

        protected MySqlConnection OpenSQL()
        {
            MySqlConnection mySqlConnection = null;

            try
            {
                mySqlConnection = new MySqlConnection("Server=" + mysqlIP + ";Database=wos_banco;Uid=root;Pwd=" + mysqlPW);
                mySqlConnection.Open();
            }
            catch (MySqlException exception)
            {
                Console.WriteLine("-------- " + exception.ToString());
            }

            return mySqlConnection;
        }

        bool IsUnlimitedVip(int pID, MySqlConnection mySqlConnection)
        {
            bool closeConnection = false;

            if (mySqlConnection == null)
            {
                mySqlConnection = OpenSQL();
                closeConnection = true;
            }

            byte unlimitedVip = 0;

            MySqlCommand cmd = new MySqlCommand("SELECT unlimitedVip FROM users where id=" + pID, mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
               // unlimitedVip = dataReader.GetByte("unlimitedVip");
            }
            dataReader.Close();

            if (closeConnection) mySqlConnection.Close();

            if (unlimitedVip == 1)
                return true;
            else
                return false;
        }

        protected void AddVip(int pID, MySqlConnection mySqlConnection, int days, int hours, int minutes)
        {
            bool closeConnection = false;

            if (mySqlConnection == null)
            {
                mySqlConnection = OpenSQL();
                closeConnection = true;
            }

            MySqlCommand cmd;
            MySqlDataReader dataReader;

            DateTime now = TimeNow(mySqlConnection);
            DateTime vipExpire = new DateTime();
            bool found = false;

            //get current vip
            cmd = new MySqlCommand("SELECT vipExpire FROM users WHERE id=" + pID, mySqlConnection);
            dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                vipExpire = dataReader.GetDateTime("vipExpire");
                found = true;
            }
            dataReader.Close();

            //just in case...
            if (!found)
            {
                mySqlConnection.Close();
                return;
            }

            int result = DateTime.Compare(now, vipExpire);
            //vip have expired, so lets set vipExpire date to now 
            if (result > 0)
                vipExpire = now;

            //add vip time
            vipExpire = vipExpire.AddDays(days);
            vipExpire = vipExpire.AddHours(hours);
            vipExpire = vipExpire.AddMinutes(minutes);

            cmd = new MySqlCommand("UPDATE users SET " +
                "vipExpire='" + vipExpire.ToString("yyyy-MM-dd HH:mm:ss") + "' " +
                "WHERE id=" + pID
                , mySqlConnection);
            cmd.ExecuteNonQuery();

            if (closeConnection) mySqlConnection.Close();

        }

        protected DateTime TimeNow(MySqlConnection mySqlConnection)
        {
            if (mySqlConnection == null)
                mySqlConnection = OpenSQL();

            DateTime dateTime = new DateTime();

            MySqlCommand cmd = new MySqlCommand("SELECT now()", mySqlConnection);
            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                dateTime = dataReader.GetDateTime("NOW()");
            }
            dataReader.Close();

            return dateTime;
        }

        protected void AddText(string s)
        {
            Console.WriteLine(s);
        }

        protected int[] DateTimeToArray(DateTime dateTime)
        {
            int[] res = new int[6];

            res[0] = dateTime.Year;
            res[1] = dateTime.Month;
            res[2] = dateTime.Day;
            res[3] = dateTime.Hour;
            res[4] = dateTime.Minute;
            res[5] = dateTime.Second;

            return res;
        }

        protected string MD5Hash(string text)
        {
            MD5 md5 = new MD5CryptoServiceProvider();

            string salt = "d47b19810faf86d35e88b660247331cc";

            //compute hash from the bytes of text
            md5.ComputeHash(ASCIIEncoding.ASCII.GetBytes(text + salt));

            //get hash result after compute it
            byte[] result = md5.Hash;

            StringBuilder strBuilder = new StringBuilder();
            for (int i = 0; i < result.Length; i++)
            {
                //change it into 2 hexadecimal digits
                //for each byte
                strBuilder.Append(result[i].ToString("x2"));
            }
            return strBuilder.ToString();
        }

        protected int GetUniqueID(MySqlConnection mySqlConnection)
        {
            if (mySqlConnection == null)
                mySqlConnection = OpenSQL();

            while (true)
            {
                bool isInvidualID = true;

                Random random = new Random();
                int uniqueID = random.Next(int.MinValue, int.MaxValue);

                MySqlCommand cmd = new MySqlCommand("SELECT id FROM users WHERE uniqueID=" + uniqueID, mySqlConnection);
                MySqlDataReader dataReader = cmd.ExecuteReader();
                while (dataReader.Read())
                {
                    isInvidualID = false;
                }
                dataReader.Close();

                if (isInvidualID) return uniqueID;
            }
        }

    }
}
