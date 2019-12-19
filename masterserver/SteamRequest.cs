using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using Newtonsoft.Json.Linq;
using System.IO;
using MySql.Data.MySqlClient;
using Lidgren.Network;

namespace MasterServer
{
    class SteamRequest : BaseStuff
    {
        public Thread thread;

        public ulong steamID;
        public ulong orderid;
        public byte days;
        public string language;
        public bool isFinalize = false;

        string country;
        string currency;
        string status;
        ulong transid;

        bool validUserInfo = false;

        bool sandbox = false;
        string _interface = "ISteamMicroTxn";

        public SteamRequest()
        {
            if (sandbox) _interface = "ISteamMicroTxnSandbox";
            thread = new Thread(new ThreadStart(DoRequest));            
        }

        public void DoRequest()
        {
            if (!isFinalize)
            {
                GetUserInfo();
                if (validUserInfo)
                    InitTxn();
            }
            else
            {
                FinalizePayment();
            }

        }

        void GetUserInfo()
        {

            string respString = "";
            string result = "";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.steampowered.com/" + _interface + "/GetUserInfo/v2/?key=2257489614291AFFAA0D33C9E3A88BF4&appid=393410&steamid=" + steamID);
            //V0001
            HttpWebResponse response = null;

            try
            {
                response = (HttpWebResponse)request.GetResponse();
                //respString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                //response.Close();
            }
            catch (WebException exp)
            {
                return;
            }

            respString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            response.Close();

            if (respString != "")
            {

                JToken token = JObject.Parse(respString);

                try
                {
                    result = (String)token.SelectToken("response").SelectToken("result");
                }
                catch (Exception exp)
                {
                    return;
                }

                if (result == "OK")
                {
                    try
                    {
                        country = (String)token.SelectToken("response").SelectToken("params").SelectToken("country");
                        currency = (String)token.SelectToken("response").SelectToken("params").SelectToken("currency");
                        status = (String)token.SelectToken("response").SelectToken("params").SelectToken("status");
                        validUserInfo = true;
                    }
                    catch (Exception exp)
                    {
                        return;
                    }
                }
                else Console.WriteLine("A1 " + respString);
            }

        }

        void InitTxn()
        {
            string respString = "";
            string result = "";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.steampowered.com/" + _interface + "/InitTxn/V3/");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";

            currency = "USD";

            string post = "key=2257489614291AFFAA0D33C9E3A88BF4" +
                "&appid=393410" +
                "&orderid=" + orderid +
                "&steamid=" + steamID +
                "&itemcount=1" +
                "&language=" + LanguageToISO(language) +
                "&currency=" + currency +
                "&itemid[0]=" + days +
                "&qty[0]=1" +
                "&amount[0]=" + GetPriceInCents() +
                "&description[0]=" + GetDescription();

            byte[] postBytes = Encoding.UTF8.GetBytes(post);
            request.ContentLength = postBytes.Length;

            Stream requestStream = request.GetRequestStream();
            requestStream.Write(postBytes, 0, postBytes.Length);
            requestStream.Close();

            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                respString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                response.Close();
            }
            catch (WebException exp)
            {
                return;
            }

            if (respString != "")
            {

                JToken token = JObject.Parse(respString);

                try
                {
                    result = (String)token.SelectToken("response").SelectToken("result");
                }
                catch (Exception exp)
                {
                    return;
                }

                if (result == "OK")
                {
                    try
                    {
                        transid = (ulong)token.SelectToken("response").SelectToken("params").SelectToken("transid");
                    }
                    catch (Exception exp)
                    {
                        return;
                    }

                    MySqlConnection mySqlConnection = OpenSQL();

                    MySqlCommand cmd = new MySqlCommand("UPDATE steampurchases SET " +
                        "country='" + country + "', " +
                        "currency='" + currency + "', " +
                        "transid=" + transid + " " +
                        "WHERE orderid=" + orderid
                        , mySqlConnection);
                    cmd.ExecuteNonQuery();

                    mySqlConnection.Close();
                }
                else Console.WriteLine("A2 " + respString);

            }

        }

        void FinalizePayment()
        {
            string respString = "";
            string result = "";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.steampowered.com/" + _interface + "/FinalizeTxn/V2/");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";

            string post = "key=2257489614291AFFAA0D33C9E3A88BF4" +
                "&appid=393410" +
                "&orderid=" + orderid;

            byte[] postBytes = Encoding.UTF8.GetBytes(post);
            request.ContentLength = postBytes.Length;

            Stream requestStream = request.GetRequestStream();
            requestStream.Write(postBytes, 0, postBytes.Length);
            requestStream.Close();

            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                respString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                response.Close();
            }
            catch (WebException exp)
            {
                return;
            }

            if (respString != "")
            {

                JToken token = JObject.Parse(respString);

                try
                {
                    result = (String)token.SelectToken("response").SelectToken("result");
                }
                catch (Exception exp)
                {
                    return;
                }

                if (result == "OK")
                {
                    try
                    {
                        transid = (ulong)token.SelectToken("response").SelectToken("params").SelectToken("transid");
                        orderid = (ulong)token.SelectToken("response").SelectToken("params").SelectToken("orderid");
                    }
                    catch (Exception exp)
                    {
                        return;
                    }

                    MySqlConnection mySqlConnection = OpenSQL();

                    byte completed = 0;
                    byte days = 0;
                    int pID = 0;

                    MySqlCommand cmd = new MySqlCommand("SELECT * FROM steampurchases where orderid=" + orderid + " AND transid=" + transid, mySqlConnection);
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    while (dataReader.Read())
                    {
                        completed = dataReader.GetByte("completed");
                        days = dataReader.GetByte("days");
                        pID = dataReader.GetInt32("pID");
                    }
                    dataReader.Close();

                    if (pID > 0 && completed == 0 && days > 0)
                    {
                        AddVip(pID, mySqlConnection, days, 0, 0);

                        cmd = new MySqlCommand("UPDATE steampurchases SET completed=1 WHERE orderid=" + orderid + " AND transid=" + transid, mySqlConnection);
                        cmd.ExecuteNonQuery();
                    }

                    mySqlConnection.Close();
                }
                else Console.WriteLine("A3 " + respString);

            }

        }

        int GetPriceInCents()
        {
            if (days == 7) return 79;    //      0.71€
            if (days == 30) return 279;  //2.5€  2.51€
            if (days == 90) return 749;  //6.75€ 6.74€
            //6 months                    12.00€

            return 0;
        }

        string GetDescription()
        {
            if (days == 7) return "Vip 7 days";
            if (days == 30) return "Vip 30 days";
            if (days == 90) return "Vip 90 days";

            return "";
        }

        string LanguageToISO(string language)
        {

            //if (language == "brazilian") return "";
            if (language == "bulgarian") return "bg";

            if (language == "czech") return "cs";
            if (language == "danish") return "da";
            if (language == "dutch") return "nl";
            if (language == "english") return "en";
            if (language == "finnish") return "fi";

            if (language == "french") return "fr";
            if (language == "german") return "de";
            if (language == "greek") return "el";
            if (language == "hungarian") return "hu";
            if (language == "italian") return "it";

            if (language == "japanese") return "ja";
            if (language == "koreana") return "ko";
            if (language == "korean") return "ko";
            if (language == "norwegian") return "no";
            if (language == "polish") return "pl";
            if (language == "portuguese") return "pt";
            if (language == "romanian") return "ro";
            if (language == "russian") return "ru";
            //if (language == "schinese") return "";
            if (language == "spanish") return "es";

            if (language == "swedish") return "sv";
            //if (language == "tchinese") return "";
            if (language == "thai") return "th";
            if (language == "turkish") return "tr";
            if (language == "ukrainian") return "uk";
            if (language == "vietnamese") return "vi";

            return "en";






        }

    }
}
