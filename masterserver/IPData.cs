using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

public struct NationData
{
    public string countryCode;    //FI
    public string countryName;    //Finland
    public string country3Letter; //FIN
}

namespace MasterServer
{
    class IPData
    {
        public string nameShort = "";
        public string nameLong = "";

        public IPData(string IP, MySqlConnection mySqlConnection)
        {
            string[] IPArrayStr = IP.Split('.');    //explode
            UInt32[] IPArray = new UInt32[4];

            for (int i = 0; i < 4; i++)
                UInt32.TryParse(IPArrayStr[i], out IPArray[i]);

            UInt32 IPCode = (IPArray[0] * 16777216) +
                (IPArray[1] * 65536) +
                (IPArray[2] * 256) +
                IPArray[3];

            MySqlCommand cmd = new MySqlCommand("SELECT country_code,country_name FROM ip2c WHERE " + IPCode + " BETWEEN begin_ip_num AND end_ip_num", mySqlConnection);

            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                nameShort = dataReader.GetString("country_code");
                nameLong = dataReader.GetString("country_name");
            }
            dataReader.Close();
        }


    }
}
