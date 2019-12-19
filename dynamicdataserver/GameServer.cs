using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseServer
{
    enum RoomType
    {
        Public,
        Challenge
    }

    enum RoomState
    {
        ReadyScreen,
        Running,
        //StatsScreen
    }

    class GameServer
    {
        public string servername;
        public int plrCount;  //NOTE!!! this value contains player count (also spectators) in whole server (so it maybe in example 165)
        public int tempPlrCount;
        public byte location;
        public string natCode; //FI
        public string IP;
        public int uniqueID;
        public byte maxPlayers;
        public RoomType roomType;
        public byte time;
        public RoomState roomState;//
        public bool botsEnabled;
        public bool officiallyStarted;
        public byte[] score;
        public int[] tID;  //DB id, if challenge
        public string[] teamnames;
        public User[,] users;
        public List<User> spectators = new List<User>();

        public GameServer(string servername, int plrCount, byte location, string natCode, string IP, int uniqueID, byte maxPlayers, RoomType roomType, byte time, RoomState roomState, bool botsEnabled, bool officiallyStarted, int[,] pIDs, int[,] tIDs, int[] tID, byte[] score, string[] teamnames, List<User> spectators)
        {
            this.servername = servername;
            this.plrCount = plrCount;
            this.location = location;
            this.natCode = natCode;
            this.IP = IP;
            this.uniqueID = uniqueID;
            this.maxPlayers = maxPlayers;
            this.roomType = roomType;
            this.time = time;
            this.roomState = roomState;
            this.botsEnabled = botsEnabled;
            this.officiallyStarted = officiallyStarted;
            this.spectators = spectators;

            this.users = new User[2, maxPlayers];
            this.tID = new int[2];
            this.score = new byte[2];
            this.teamnames = new string[2];

            for (int i = 0; i < 2; i++)
            {
                this.tID[i] = tID[i];
                this.score[i] = score[i];
                this.teamnames[i] = teamnames[i];
            }

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j] = new User(pIDs[i, j], tIDs[i, j]);


        }

        public int GetUserCount()
        {
            int count = 0;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].pID > 0)
                        count++;

            return count;

        }


    }
}
