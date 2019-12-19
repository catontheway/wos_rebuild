using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Lidgren.Network;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Net;
using Newtonsoft.Json.Linq;

namespace MasterServer
{
    public class Form1
    {
        public static int version = 42;
        public static bool isLocal = false;
        public static int MAXDIVISION = 6;
        public static int maxTeamsInDiv = 12;

        ClientToBS clientToBS;
        ClientToDS clientToDS;
        ServerForGS serverForGS;
        ServerForU serverForU;
        public static List<NationData> nationData = new List<NationData>();
        public static bool Timer1Enabled = true;
        public static bool Timer2Enabled = false;
        public static bool Timer3Enabled = false;

        public Form1()
        {
            if (IsLocalhost()) isLocal = true;

            GenerateNationData();

            clientToBS = new ClientToBS();
            clientToDS = new ClientToDS();
            serverForGS = new ServerForGS();
            serverForU = new ServerForU();

            clientToDS.clientToBS = clientToBS;
            clientToBS.clientToDS = clientToDS;
            clientToDS.serverForGS = serverForGS;
            serverForGS.clientToDS = clientToDS;
            serverForU.clientToDS = clientToDS;
            serverForU.clientToBS = clientToBS;
            clientToDS.serverForU = serverForU;
            clientToBS.serverForGS = serverForGS;
            clientToBS.serverForU = serverForU;

            serverForU.LoadPlrsByNation();

            Timer timer1 = new Timer(TimerCallback1, null, 0, 10000);
            Timer timer2 = new Timer(TimerCallback2, null, 0, 3000);
            Timer timer3 = new Timer(TimerCallback3, null, 0, 3000);
        }

        void TimerCallback1(Object o)
        {
            if (!Timer1Enabled) return;

            //timeout gameservers
            lock (serverForGS.gameServers)
            {
                for (int i = serverForGS.gameServers.Count - 1; i >= 0; --i)
                {
                    serverForGS.gameServers[i].timeout++;

                    if (serverForGS.gameServers[i].timeout >= 3)
                    {
                        clientToDS.InformAboutGSDisconnect(serverForGS.gameServers[i].netConnection.RemoteEndPoint.Address.ToString());
                        serverForGS.gameServers.RemoveAt(i);
                        break;
                    }
                }
            }


            //inform BS about connections count
            if (clientToBS.client.ConnectionStatus == NetConnectionStatus.Connected && clientToDS.client.ConnectionStatus == NetConnectionStatus.Connected)
            {
                int connectionsCount = serverForU.server.ConnectionsCount + serverForGS.server.ConnectionsCount;
                clientToBS.SendConnectionCountToBS(connectionsCount);
            }

            clientToDS.SendLobbyUsersToDS();
        }

        void TimerCallback2(Object o)
        {
            if (!Timer2Enabled) return;

            //this handles clientToBS reconnecting
            Console.WriteLine("reconnecting to clientToBS...");
            Timer2Enabled = false;
            clientToBS.client.Connect(clientToBS.ipToBalanceServer, 14241);
        }

        void TimerCallback3(Object o)
        {
            if (!Timer3Enabled) return;

            //this handles clientToDS reconnecting
            Console.WriteLine("reconnecting to clientToDS...");
            Timer3Enabled = false;
            clientToDS.client.Connect(clientToDS.ipToDatabaseServer, 14246);
        }

        private bool IsLocalhost()
        {
            return File.Exists(Environment.CurrentDirectory + $"\\local.txt");
        }


        void GenerateNationData()
        {
            //3 letter codes:
            //http://en.wikipedia.org/wiki/List_of_FIFA_country_codes

            nationData.Add(new NationData("AD", "Andorra", "AND"));
            nationData.Add(new NationData("AE", "United Arab Emirates", "UAE"));
            nationData.Add(new NationData("AF", "Afghanistan", "AFG"));
            nationData.Add(new NationData("AG", "Antigua & Barbuda", "ATG"));
            nationData.Add(new NationData("AI", "Anguilla", "AIA"));
            nationData.Add(new NationData("AL", "Albania", "ALB"));
            nationData.Add(new NationData("AM", "Armenia", "ARM"));
            nationData.Add(new NationData("AO", "Angola", "ANG"));
            nationData.Add(new NationData("AR", "Argentina", "ARG"));
            nationData.Add(new NationData("AS", "American Samoa", "ASA"));
            nationData.Add(new NationData("AT", "Austria", "AUT"));
            nationData.Add(new NationData("AU", "Australia", "AUS"));
            nationData.Add(new NationData("AW", "Aruba", "ARU"));
            nationData.Add(new NationData("AZ", "Azerbaijan", "AZE"));
            nationData.Add(new NationData("BA", "Bosnia-Herzegovina", "BIH"));
            nationData.Add(new NationData("BB", "Barbados", "BRB"));
            nationData.Add(new NationData("BD", "Bangladesh", "BAN"));
            nationData.Add(new NationData("BE", "Belgium", "BEL"));
            nationData.Add(new NationData("BF", "Burkina Faso", "BFA"));
            nationData.Add(new NationData("BG", "Bulgaria", "BUL"));
            nationData.Add(new NationData("BH", "Bahrain", "BHR"));
            nationData.Add(new NationData("BI", "Burundi", "BDI"));
            nationData.Add(new NationData("BJ", "Benin", "BEN"));
            nationData.Add(new NationData("BM", "Bermuda", "BER"));
            nationData.Add(new NationData("BN", "Brunei", "BRU"));
            nationData.Add(new NationData("BO", "Bolivia", "BOL"));
            nationData.Add(new NationData("BR", "Brazil", "BRA"));
            nationData.Add(new NationData("BS", "Bahamas", "BAH"));
            nationData.Add(new NationData("BT", "Bhutan", "BHU"));
            nationData.Add(new NationData("BW", "Botswana", "BOT"));
            nationData.Add(new NationData("BY", "Belarus", "BLR"));
            nationData.Add(new NationData("BZ", "Belize", "BLZ"));
            nationData.Add(new NationData("CA", "Canada", "CAN"));
            nationData.Add(new NationData("CD", "DR Congo", "COD"));
            nationData.Add(new NationData("CF", "Central African Republic", "CTA"));
            nationData.Add(new NationData("CG", "Congo", "CGO"));
            nationData.Add(new NationData("CH", "Switzerland", "SUI"));
            nationData.Add(new NationData("CI", "Ivory Coast", "CIV"));
            nationData.Add(new NationData("CK", "Cook Islands", "COK"));
            nationData.Add(new NationData("CL", "Chile", "CHI"));
            nationData.Add(new NationData("CM", "Cameroon", "CMR"));
            nationData.Add(new NationData("CN", "China", "CHN"));
            nationData.Add(new NationData("CO", "Colombia", "COL"));
            nationData.Add(new NationData("CR", "Costa Rica", "CRC"));
            nationData.Add(new NationData("CU", "Cuba", "CUB"));
            nationData.Add(new NationData("CV", "Cape Verde", "CPV"));
            nationData.Add(new NationData("CW", "Curacao", "CUW"));
            nationData.Add(new NationData("CY", "Cyprus", "CYP"));
            nationData.Add(new NationData("CZ", "Czech Republic", "CZE"));
            nationData.Add(new NationData("DE", "Germany", "GER"));
            nationData.Add(new NationData("DJ", "Djibouti", "DJI"));
            nationData.Add(new NationData("DK", "Denmark", "DEN"));
            nationData.Add(new NationData("DM", "Dominica", "DMA"));
            nationData.Add(new NationData("DO", "Dominican Republic", "DOM"));
            nationData.Add(new NationData("DZ", "Algeria", "ALG"));
            nationData.Add(new NationData("EC", "Ecuador", "ECU"));
            nationData.Add(new NationData("EE", "Estonia", "EST"));
            nationData.Add(new NationData("EG", "Egypt", "EGY"));
            nationData.Add(new NationData("ER", "Eritrea", "ERI"));
            nationData.Add(new NationData("ES", "Spain", "ESP"));
            nationData.Add(new NationData("ET", "Ethiopia", "ETH"));
            nationData.Add(new NationData("FI", "Finland", "FIN"));
            nationData.Add(new NationData("FJ", "Fiji", "FIJ"));
            nationData.Add(new NationData("FO", "Faroe Islands", "FRO"));
            nationData.Add(new NationData("FR", "France", "FRA"));
            nationData.Add(new NationData("GA", "Gabon", "GAB"));
            nationData.Add(new NationData("GB", "United Kingdom", "GBR"));
            nationData.Add(new NationData("GD", "Grenada", "GRN"));
            nationData.Add(new NationData("GE", "Georgia", "GEO"));
            nationData.Add(new NationData("GH", "Ghana", "GHA"));
            nationData.Add(new NationData("GI", "Gibraltar", "GIB"));
            nationData.Add(new NationData("GM", "Gambia", "GAM"));
            nationData.Add(new NationData("GN", "Guinea", "GUI"));
            nationData.Add(new NationData("GQ", "Equatorial Guinea", "EQG"));
            nationData.Add(new NationData("GR", "Greece", "GRE"));
            nationData.Add(new NationData("GT", "Guatemala", "GUA"));
            nationData.Add(new NationData("GU", "Guam", "GUM"));
            nationData.Add(new NationData("GW", "Guinea-Bissau", "GNB"));
            nationData.Add(new NationData("GY", "Guyana", "GUY"));
            nationData.Add(new NationData("HK", "Hong Kong", "HKG"));
            nationData.Add(new NationData("HN", "Honduras", "HON"));
            nationData.Add(new NationData("HR", "Croatia", "CRO"));
            nationData.Add(new NationData("HT", "Haiti", "HAI"));
            nationData.Add(new NationData("HU", "Hungary", "HUN"));
            nationData.Add(new NationData("ID", "Indonesia", "IDN"));
            nationData.Add(new NationData("IE", "Republic of Ireland", "IRL"));
            nationData.Add(new NationData("IL", "Israel", "ISR"));
            nationData.Add(new NationData("IN", "India", "IND"));
            nationData.Add(new NationData("IQ", "Iraq", "IRQ"));
            nationData.Add(new NationData("IR", "Iran", "IRN"));
            nationData.Add(new NationData("IS", "Iceland", "ISL"));
            nationData.Add(new NationData("IT", "Italy", "ITA"));
            nationData.Add(new NationData("JM", "Jamaica", "JAM"));
            nationData.Add(new NationData("JO", "Jordan", "JOR"));
            nationData.Add(new NationData("JP", "Japan", "JPN"));
            nationData.Add(new NationData("KE", "Kenya", "KEN"));
            nationData.Add(new NationData("KG", "Kyrgyzstan", "KGZ"));
            nationData.Add(new NationData("KH", "Cambodia", "CAM"));
            nationData.Add(new NationData("KI", "Kiribati", "KIR"));
            nationData.Add(new NationData("KM", "Comoros", "COM"));
            nationData.Add(new NationData("KN", "St Kitts & Nevis", "SKN"));
            nationData.Add(new NationData("KP", "North Korea", "PRK"));
            nationData.Add(new NationData("KR", "South Korea", "KOR"));
            nationData.Add(new NationData("KW", "Kuwait", "KUW"));
            nationData.Add(new NationData("KY", "Cayman Islands", "CAY"));
            nationData.Add(new NationData("KZ", "Kazakhstan", "KAZ"));
            nationData.Add(new NationData("LA", "Laos", "LAO"));
            nationData.Add(new NationData("LB", "Lebanon", "LIB"));
            nationData.Add(new NationData("LC", "St Lucia", "LCA"));
            nationData.Add(new NationData("LI", "Liechtenstein", "LIE"));
            nationData.Add(new NationData("LK", "Sri Lanka", "SRI"));
            nationData.Add(new NationData("LR", "Liberia", "LBR"));
            nationData.Add(new NationData("LS", "Lesotho", "LES"));
            nationData.Add(new NationData("LT", "Lithuania", "LTU"));
            nationData.Add(new NationData("LU", "Luxembourg", "LUX"));
            nationData.Add(new NationData("LV", "Latvia", "LVA"));
            nationData.Add(new NationData("LY", "Libya", "LBY"));
            nationData.Add(new NationData("MA", "Morocco", "MAR"));
            nationData.Add(new NationData("MD", "Moldova", "MDA"));
            nationData.Add(new NationData("ME", "Montenegro", "MNE"));
            nationData.Add(new NationData("MG", "Madagascar", "MAD"));
            nationData.Add(new NationData("MK", "Macedonia", "MKD"));
            nationData.Add(new NationData("ML", "Mali", "MLI"));
            nationData.Add(new NationData("MM", "Myanmar", "MYA"));
            nationData.Add(new NationData("MN", "Mongolia", "MNG"));
            nationData.Add(new NationData("MO", "Macau", "MAC"));
            nationData.Add(new NationData("MP", "Northern Mariana Islands", "NMI"));
            nationData.Add(new NationData("MQ", "Martinique", "MTQ"));
            nationData.Add(new NationData("MR", "Mauritania", "MTN"));
            nationData.Add(new NationData("MS", "Montserrat", "MSR"));
            nationData.Add(new NationData("MT", "Malta", "MLT"));
            nationData.Add(new NationData("MU", "Mauritius", "MRI"));
            nationData.Add(new NationData("MV", "Maldives", "MDV"));
            nationData.Add(new NationData("MW", "Malawi", "MWI"));
            nationData.Add(new NationData("MX", "Mexico", "MEX"));
            nationData.Add(new NationData("MY", "Malaysia", "MAS"));
            nationData.Add(new NationData("MZ", "Mozambique", "MOZ"));
            nationData.Add(new NationData("NA", "Namibia", "NAM"));
            nationData.Add(new NationData("NC", "New Caledonia", "NCL"));
            nationData.Add(new NationData("NE", "Niger", "NIG"));
            nationData.Add(new NationData("NG", "Nigeria", "NGA"));
            nationData.Add(new NationData("NI", "Nicaragua", "NCA"));
            nationData.Add(new NationData("NL", "Holland", "NED"));
            nationData.Add(new NationData("NO", "Norway", "NOR"));
            nationData.Add(new NationData("NP", "Nepal", "NEP"));
            nationData.Add(new NationData("NU", "Niue", "NIU"));
            nationData.Add(new NationData("NZ", "New Zealand", "NZL"));
            nationData.Add(new NationData("OM", "Oman", "OMA"));
            nationData.Add(new NationData("PA", "Panama", "PAN"));
            nationData.Add(new NationData("PE", "Peru", "PER"));
            nationData.Add(new NationData("PG", "Papua New Guinea", "PNG"));
            nationData.Add(new NationData("PH", "Philippines", "PHI"));
            nationData.Add(new NationData("PK", "Pakistan", "PAK"));
            nationData.Add(new NationData("PL", "Poland", "POL"));
            nationData.Add(new NationData("PR", "Puerto Rico", "PUR"));
            nationData.Add(new NationData("PS", "Palestine", "PLE"));
            nationData.Add(new NationData("PT", "Portugal", "POR"));
            nationData.Add(new NationData("PW", "Palau", "PLW"));
            nationData.Add(new NationData("PY", "Paraguay", "PAR"));
            nationData.Add(new NationData("QA", "Qatar", "QAT"));
            nationData.Add(new NationData("RE", "Reunion", "REU"));
            nationData.Add(new NationData("RO", "Romania", "ROU"));
            nationData.Add(new NationData("RS", "Serbia", "SRB"));
            nationData.Add(new NationData("RU", "Russia", "RUS"));
            nationData.Add(new NationData("RW", "Rwanda", "RWA"));
            nationData.Add(new NationData("SA", "Saudi Arabia", "KSA"));
            nationData.Add(new NationData("SB", "Solomon Islands", "SOL"));
            nationData.Add(new NationData("SC", "Seychelles", "SEY"));
            nationData.Add(new NationData("SD", "Sudan", "SDN"));
            nationData.Add(new NationData("SE", "Sweden", "SWE"));
            nationData.Add(new NationData("SG", "Singapore", "SIN"));
            nationData.Add(new NationData("SI", "Slovenia", "SVN"));
            nationData.Add(new NationData("SK", "Slovakia", "SVK"));
            nationData.Add(new NationData("SL", "Sierra Leone", "SLE"));
            nationData.Add(new NationData("SM", "San Marino", "SMR"));
            nationData.Add(new NationData("SN", "Senegal", "SEN"));
            nationData.Add(new NationData("SO", "Somalia", "SOM"));
            nationData.Add(new NationData("SR", "Suriname", "SUR"));
            nationData.Add(new NationData("SS", "South Sudan", "SSD"));
            nationData.Add(new NationData("ST", "Sao Tome & Principe", "STP"));
            nationData.Add(new NationData("SV", "El Salvador", "SLV"));
            nationData.Add(new NationData("SY", "Syria", "SYR"));
            nationData.Add(new NationData("SZ", "Swaziland", "SWZ"));
            nationData.Add(new NationData("TC", "Turks & Caicos Islands", "TCA"));
            nationData.Add(new NationData("TD", "Chad", "CHA"));
            nationData.Add(new NationData("TG", "Togo", "TOG"));
            nationData.Add(new NationData("TH", "Thailand", "THA"));
            nationData.Add(new NationData("TJ", "Tajikistan", "TJK"));
            nationData.Add(new NationData("TL", "East Timor", "TLS"));
            nationData.Add(new NationData("TM", "Turkmenistan", "TKM"));
            nationData.Add(new NationData("TN", "Tunisia", "TUN"));
            nationData.Add(new NationData("TO", "Tonga", "TGA"));
            nationData.Add(new NationData("TR", "Turkey", "TUR"));
            nationData.Add(new NationData("TT", "Trinidad & Tobago", "TRI"));
            nationData.Add(new NationData("TV", "Tuvalu", "TUV"));
            nationData.Add(new NationData("TW", "Taiwan", "TPE"));
            nationData.Add(new NationData("TZ", "Tanzania", "TAN"));
            nationData.Add(new NationData("UA", "Ukraine", "UKR"));
            nationData.Add(new NationData("UG", "Uganda", "UGA"));
            nationData.Add(new NationData("US", "United States", "USA"));
            nationData.Add(new NationData("UY", "Uruguay", "URU"));
            nationData.Add(new NationData("UZ", "Uzbekistan", "UZB"));
            nationData.Add(new NationData("VC", "St Vincent & the Grenadines", "VIN"));
            nationData.Add(new NationData("VE", "Venezuela", "VEN"));
            nationData.Add(new NationData("VG", "British Virgin Islands", "VGB"));

            nationData.Add(new NationData("VI", "U.S. Virgin Islands", "VIR"));
            nationData.Add(new NationData("VN", "Vietnam", "VIE"));
            nationData.Add(new NationData("VU", "Vanuatu", "VAN"));
            nationData.Add(new NationData("WS", "Samoa", "SAM"));
            nationData.Add(new NationData("YE", "Yemen", "YEM"));
            nationData.Add(new NationData("ZA", "South Africa", "RSA"));
            nationData.Add(new NationData("ZM", "Zambia", "ZAM"));
            nationData.Add(new NationData("ZW", "Zimbabwe", "ZIM"));

            //specials (A1 and A2 are reserved)
            nationData.Add(new NationData("A3", "England", "ENG"));
            nationData.Add(new NationData("A4", "Scotland", "SCO"));
            nationData.Add(new NationData("A5", "Wales", "WAL"));
            nationData.Add(new NationData("A6", "Northern Ireland", "NIR"));

        }

        void UpdateTeamToLeague(MySqlConnection mySqlConnection, int division, int _group, int tID)
        {
            MySqlCommand cmd = new MySqlCommand("UPDATE teams SET division=" + division + ", _group=" + _group + " WHERE id=" + tID, mySqlConnection);
            cmd.ExecuteNonQuery();
        }

        int[] GetDivForTeam2(MySqlConnection mySqlConnection, int tID, int location)
        {
            MySqlDataReader dataReader;
            MySqlCommand cmd;
            int count = 0;
            int groupCount = 1;

            int[] divGroup = new int[2];

            for (int div = 1; div < (MAXDIVISION + 1); div++)
            {
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

                    if (count < maxTeamsInDiv)
                    {
                        divGroup[0] = div;
                        divGroup[1] = curGroup;
                        return divGroup;
                    }
                }

                groupCount *= 2;
            }



            divGroup[0] = 1;
            divGroup[1] = 1;

            return divGroup;
        }


        int GetTotalGroupCount()
        {
            int count = 0;
            int groupCount = 1;

            for (int div = 1; div < (MAXDIVISION + 1); div++)
            {
                for (int curGroup = 1; curGroup < groupCount + 1; curGroup++)
                {
                    count++;
                }
                groupCount *= 2;
            }

            return count;
        }

        int[] GetDivAndGroupByID(int id, bool goForward)
        {
            int[] res = new int[2];

            int count = 0;
            int groupCount = 1;

            for (int div = 1; div < (MAXDIVISION + 1); div++)
            {
                for (int curGroup = 1; curGroup < groupCount + 1; curGroup++)
                {
                    count++;
                    if (count == id)
                    {
                        res[0] = div;
                        res[1] = curGroup;
                        return res;
                    }

                }
                groupCount *= 2;
            }

            return res;
        }

        void GenerateFixture(int tID0, int tID1, DateTime dateTime, int season, int location, int division, int _group, MySqlConnection mySqlConnection)
        {
            //for south america, add 4 hours
            if (location == 1)
            {
                dateTime = dateTime.AddHours(4);
                //dateTime = dateTime.AddMinutes(30);
            }
            //for noth america
            if (location == 2)
            {
                dateTime = dateTime.AddHours(7);
                dateTime = dateTime.AddMinutes(30);
            }
            //for asia
            if (location == 3)
            {
                dateTime = dateTime.AddHours(-7); //tää pitäs olla ok
            }

            MySqlCommand cmd;

            cmd = new MySqlCommand("INSERT INTO fixtures SET " +
                "tID0=" + tID0 + ", " +
                "tID1=" + tID1 + ", " +
                "time='" + dateTime.ToString("yyyy-MM-dd HH:mm:ss") + "', " +
                "season=" + season + ", " +
                "location=" + location + ", " +
                "division=" + division + ", " +
                "_group=" + _group
                , mySqlConnection);
            cmd.ExecuteNonQuery();
        }

        public static int[] GetTeamsToFixture(int round, int matchID, bool doSwap)
        {
            int[] res = new int[2];
            int t;

            #region round 1
            if (round == 1)
            {
                if (matchID == 0)
                {
                    res[0] = 5;
                    res[1] = 4;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 10;
                    res[1] = 3;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 0;
                    res[1] = 8;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 9;
                    res[1] = 11;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 6;
                    res[1] = 2;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 7;
                    res[1] = 1;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            #region round 2
            if (round == 2)
            {
                if (matchID == 0)
                {
                    res[0] = 3;
                    res[1] = 5;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 8;
                    res[1] = 4;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 11;
                    res[1] = 10;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 2;
                    res[1] = 0;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 1;
                    res[1] = 9;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 7;
                    res[1] = 6;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            #region round 3
            if (round == 3)
            {
                if (matchID == 0)
                {
                    res[0] = 5;
                    res[1] = 8;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 3;
                    res[1] = 11;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 4;
                    res[1] = 2;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 10;
                    res[1] = 1;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 0;
                    res[1] = 7;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 9;
                    res[1] = 6;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            #region round 4
            if (round == 4)
            {
                if (matchID == 0)
                {
                    res[0] = 11;
                    res[1] = 5;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 2;
                    res[1] = 8;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 1;
                    res[1] = 3;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 7;
                    res[1] = 4;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 6;
                    res[1] = 10;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 9;
                    res[1] = 0;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            #region round 5
            if (round == 5)
            {
                if (matchID == 0)
                {
                    res[0] = 5;
                    res[1] = 2;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 11;
                    res[1] = 1;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 8;
                    res[1] = 7;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 3;
                    res[1] = 6;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 4;
                    res[1] = 9;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 10;
                    res[1] = 0;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            #region round 6
            if (round == 6)
            {
                if (matchID == 0)
                {
                    res[0] = 1;
                    res[1] = 5;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 7;
                    res[1] = 2;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 6;
                    res[1] = 11;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 9;
                    res[1] = 8;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 0;
                    res[1] = 3;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 10;
                    res[1] = 4;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            #region round 7
            if (round == 7)
            {
                if (matchID == 0)
                {
                    res[0] = 5;
                    res[1] = 7;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 1;
                    res[1] = 6;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 2;
                    res[1] = 9;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 11;
                    res[1] = 0;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 8;
                    res[1] = 10;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 3;
                    res[1] = 4;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            #region round 8
            if (round == 8)
            {
                if (matchID == 0)
                {
                    res[0] = 6;
                    res[1] = 5;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 9;
                    res[1] = 7;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 0;
                    res[1] = 1;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 10;
                    res[1] = 2;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 4;
                    res[1] = 11;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 3;
                    res[1] = 8;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            #region round 9
            if (round == 9)
            {
                if (matchID == 0)
                {
                    res[0] = 5;
                    res[1] = 9;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 6;
                    res[1] = 0;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 7;
                    res[1] = 10;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 1;
                    res[1] = 4;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 2;
                    res[1] = 3;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 11;
                    res[1] = 8;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            #region round 10
            if (round == 10)
            {
                if (matchID == 0)
                {
                    res[0] = 0;
                    res[1] = 5;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 10;
                    res[1] = 9;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 4;
                    res[1] = 6;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 3;
                    res[1] = 7;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 8;
                    res[1] = 1;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 11;
                    res[1] = 2;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            #region round 11
            if (round == 11)
            {
                if (matchID == 0)
                {
                    res[0] = 5;
                    res[1] = 10;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 1)
                {
                    res[0] = 0;
                    res[1] = 4;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 2)
                {
                    res[0] = 9;
                    res[1] = 3;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 3)
                {
                    res[0] = 6;
                    res[1] = 8;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 4)
                {
                    res[0] = 7;
                    res[1] = 11;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
                if (matchID == 5)
                {
                    res[0] = 1;
                    res[1] = 2;
                    if (doSwap)
                    {
                        t = res[1];
                        res[1] = res[0];
                        res[0] = t;
                    }
                    return res;
                }
            }
            #endregion

            return res;
        }

        private void timer4_Tick(object sender, EventArgs e)
        {
            serverForU.CountPlrsByNation();
        }


        private void button6_Click(object sender, EventArgs e)
        {

        }

        private void timer5_Tick(object sender, EventArgs e)
        {
            //send timeout message to BS
            if (clientToBS.client.ConnectionStatus == NetConnectionStatus.Connected && clientToDS.client.ConnectionStatus == NetConnectionStatus.Connected)
            {
                clientToBS.SendTimeoutMSG();
            }
        }





        int GetGroupContainingFewestTeams(int location, int division)
        {
            return 0;
        }

        string GetTeamname(MySqlConnection mySqlConnection, int tID)
        {
            if (tID == 0) return "bot team";

            string teamname = "";

            MySqlCommand cmd = new MySqlCommand("SELECT name FROM teams WHERE id=" + tID, mySqlConnection);

            MySqlDataReader dataReader = cmd.ExecuteReader();
            while (dataReader.Read())
            {
                teamname = dataReader.GetString("name");
            }
            dataReader.Close();

            return teamname;
        }


    }
}
