using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    class RoomData
    {
        public string servername;
        public string IP;
        public int uniqueID;
        public byte maxPlayers;
        public byte roomType;
        public byte time;
        public byte roomState;
        public bool botsEnabled;
        public byte location;
        public string natCode; //FI
        public User[,] users;
        public byte[] score;
        public int[] tID;
        public string[] teamnames;

        public RoomData(string servername, string IP, int uniqueID, byte maxPlayers, byte roomType, byte time,byte roomState, bool botsEnabled, byte location, string natCode, int[,] pIDs, int[,] tIDs, byte[] score, int[] tID, string[] teamnames)
        {
            this.servername = servername;
            this.IP = IP;
            this.uniqueID = uniqueID;
            this.maxPlayers = maxPlayers;
            this.roomType = roomType;
            this.time = time;
            this.roomState = roomState;
            this.botsEnabled = botsEnabled;
            this.location = location;
            this.natCode = natCode;

            this.users = new User[2, maxPlayers];
            this.score = new byte[2];
            this.tID = new int[2];
            this.teamnames = new string[2];

            for (int i = 0; i < 2; i++)
            {
                this.score[i] = score[i];
                this.tID[i] = tID[i];
                this.teamnames[i] = teamnames[i];
            }

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    users[i, j] = new User(pIDs[i, j], tIDs[i, j]);

        }

        public byte GetPlayerCount()
        {
            byte count = 0;

            for (int i = 0; i < 2; i++)
                for (int j = 0; j < maxPlayers; j++)
                    if (users[i, j].pID > 0)
                        count++;

            return count;
        }

    }
}
