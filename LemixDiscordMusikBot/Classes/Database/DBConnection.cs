using DSharpPlus;
using LemixDiscordMusikBot.Classes;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Data;

namespace LemixDiscordMusikBot
{
    class DBConnection
    {
        private MySqlConnection conn;
        public string server;
        public string database;
        public string uid;
        private string pwd;
        public int port;
        private ILogger<BaseDiscordClient> logger;

        public DBConnection(ILogger<BaseDiscordClient> logger, Config configJson)
        {
            Init(logger, configJson);
        }

         string Truncate(string value, int maxLength)
        {
            String s;
            if (string.IsNullOrEmpty(value)) return value;
            s = value.Length <= maxLength ? value : value.Substring(0, maxLength);
            if(!(value.Length <= maxLength))
                s = s + "...";
            return s;
        }

        private void Init(ILogger<BaseDiscordClient> logger, Config configJson)
        {
            try {
                this.server = configJson.DatabaseHostname;
                this.database = configJson.DatabaseDbName;
                this.uid = configJson.DatabaseUid;
                this.pwd = configJson.DatabasePassword;
                this.port = configJson.DatabasePort;
                Variables.MysqlConnectionString = "SERVER=" + server + ";" + "PORT=" + port + ";" + "DATABASE=" + database + ";" + "UID=" + uid + ";" + "PASSWORD=" + pwd + ";";
                this.conn = new MySqlConnection("SERVER=" + server + ";" + "PORT=" + port + ";" + "DATABASE=" + database + ";" + "UID=" + uid + ";" + "PASSWORD=" + pwd + ";");
                this.logger = logger;
                logger.LogInformation(new EventId(7777, "Database"), $"Database initialized. [{uid}@{server}:{port}]");
                if (conn.State != ConnectionState.Open)
                    if (!Connect())
                        Disconnect();
            }
            catch(Exception e)
            {
                logger.LogCritical(new EventId(7777, "Database"), $"Database error. {e}");
            }
            
        }

        private bool Connect()
        {
            try
            {
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }
                else
                {
                    conn.Close();
                    conn.Open();
                }  
                return true;
            }
            catch (MySqlException ex)
            {

                logger.LogCritical(new EventId(7772, "Database"), $"{ex.Message}");
                Environment.Exit(0);
                return false;

            }
        }


        public bool Disconnect()
        {
            try
            {
                conn.Close();
                return true;
            }
            catch (MySqlException ex)
            {
                logger.LogCritical(new EventId(7773, "Database"), $"{ex.Message}");
                Environment.Exit(0);
                return false;
            }

        }


        public void Execute(string query)
        {
            if (this.Connect() == true)
            {
                try
                {
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    var rowsaffected = cmd.ExecuteNonQuery();
                    if (rowsaffected == -1)
                        logger.LogDebug(new EventId(7778, "Database"), $"\"{Truncate(cmd.CommandText, 100)}\"");   
                    else
                        logger.LogDebug(new EventId(7778, "Database"), $"\"{Truncate(cmd.CommandText, 100)}\" affected {rowsaffected} rows");
                    
                }
                catch(Exception e)
                {
                    logger.LogCritical(new EventId(7778, "Database"), $"Sql query errored {e.Message} (statement: {query})");
                }

                this.Disconnect();


            }

        }
        public MySqlDataReader Query(string query)
        {
            if (this.Connect() == true)
            {
                try
                {
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    logger.LogDebug(new EventId(7779, "Database"), $"\"{Truncate(cmd.CommandText, 100)}\"");
                    return dataReader;
                    
                }
                catch (Exception e)
                {
                    logger.LogCritical(new EventId(7779, "Database"), $"Sql query errored {e.Message} (statement: {query})");
                }

                
            }
            this.Disconnect();
            return null;
        }



    }

   
}
