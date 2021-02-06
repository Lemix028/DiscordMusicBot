using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using LemixDiscordMusikBot.Commands;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LemixDiscordMusikBot.Classes.Database
{
    public static class Data
    {
        #region Check Methods
        public static bool CheckGuildIdExists(ulong GuildId)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            connection.Close();
                            Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in CheckGuildIdExists --> Return {true}");
                            return true;
                        }
                        else
                        {
                            connection.Close();
                            Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in CheckGuildIdExists --> Return {false}");
                            return false;
                        }

                    }


                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckGuildIdExists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckGuildIdExists: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool CheckBotChannelExists(ulong GuildId)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in CheckBotChannelExists --> Return {!(reader.GetUInt64("BotChannel") <= 0)}");
                            if (!(reader.GetUInt64("BotChannel") <= 0))
                            {
                                connection.Close(); return true;
                            }
                            else
                            {
                                connection.Close(); return false;
                            }

                        }

                    }


                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckBotChannelExists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckBotChannelExists: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool CheckBotChannelMainMessageExists(ulong GuildId)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);               
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in CheckBotChannelMainMessageExists --> Return {!(reader.GetUInt64("BotChannelMainMessage") <= 0)}");
                            if (!(reader.GetUInt64("BotChannelMainMessage") <= 0))
                            {
                                connection.Close(); return true;
                            }
                            else
                            {
                                connection.Close(); return false;
                            }
                        }


                    }


                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckBotChannelMainMessageExists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckBotChannelMainMessageExists: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool CheckBotChannelBannerMessageExists(ulong GuildId)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in CheckBotChannelBannerMessageExists --> Return {!(reader.GetUInt64("BotChannelBannerMessage") <= 0)}");
                            if (!(reader.GetUInt64("BotChannelBannerMessage") <= 0))
                            {
                                connection.Close(); return true;
                            }
                            else
                            {
                                connection.Close(); return false;
                            }
                        }

                    }


                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckBotChannelBannerMessageExists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckBotChannelBannerMessageExists: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool CheckFavoritesTracksListsExists(ulong GuildId)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in CheckFavoritesTracksListsExists --> Return {!reader.IsDBNull(reader.GetOrdinal("FavoritesTracksList"))}");
                            if (!reader.IsDBNull(reader.GetOrdinal("FavoritesTracksList")))
                            {
                                connection.Close(); return true;
                            }
                            else
                            {
                                connection.Close(); return false;
                            }
                        }

                    }


                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckFavoritesTracksListsExists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckFavoritesTracksListsExists: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool CheckGuildRolesExists(ulong GuildId)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in CheckGuildRolesExists --> Return {!reader.IsDBNull(reader.GetOrdinal("GuildRoles"))}");
                            if (!reader.IsDBNull(reader.GetOrdinal("GuildRoles")))
                            {
                                connection.Close(); return true;
                            }
                            else
                            {
                                connection.Close(); return false;
                            }
                        }

                    }


                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckGuildRolesExists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" CheckGuildRolesExists: {e.StackTrace}");
                }
            }
            return false;
        }
        #endregion

        #region Set Methods
        public static bool SetBotChannelId(ulong GuildId, ulong BotChannelId)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE data SET BotChannel=@botid WHERE GuildId=@gid;";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@botid", BotChannelId);
                    int ret = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in SetBotChannelId --> Return {ret} updated");
                    if (ret <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetBotChannelId: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetBotChannelId: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool SetAFKState(ulong GuildId, bool State)
        {
            int s;
            if (State == true)
                s = 1;
            else
                s = 0;
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE data SET AFKState=@state WHERE GuildId=@gid;";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@state", s);
                    int ret = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in SetAFKState --> Return {ret} updated");
                    if (ret <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetAFKState: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetAFKState: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool SetBotChannelMainMessageId(ulong GuildId, ulong BotChannelMainId)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE data SET BotChannelMainMessage=@BotChannelMainId WHERE GuildId=@gid;";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@BotChannelMainId", BotChannelMainId);
                    int ret = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in SetBotChannelMainMessageId --> Return {ret} updated");
                    if (ret <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetBotChannelMainMessageId: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetBotChannelMainMessageId: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool SetBotChannelBannerMessageId(ulong GuildId, ulong BotChannelBannerId)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE data SET BotChannelBannerMessage=@BotChannelBannerId WHERE GuildId=@gid;";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@BotChannelBannerId", BotChannelBannerId);
                    int ret = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in SetBotChannelBannerMessageId --> Return {ret} updated");
                    if (ret <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetBotChannelBannerMessageId: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetBotChannelBannerMessageId: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool SetGuildRoles(ulong GuildId, List<Tuple<DiscordRole, Lava.role>> roles)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE data SET GuildRoles=@roles WHERE GuildId=@gid;";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@roles", JsonConvert.SerializeObject(roles));
                    int ret = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in SetGuildRoles --> Return {ret} updated");
                    if (ret <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetGuildRoles: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetGuildRoles: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool SetFavoritesTracksList(ulong GuildId, List<LavalinkTrack> tracks)
        {

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE data SET FavoritesTracksList=@tracks WHERE GuildId=@gid;";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@tracks", JsonConvert.SerializeObject(tracks));
                    int ret = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in SetFavoritesTracksList --> Return {ret} updated");
                    if (ret <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetFavoritesTracksList: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" SetFavoritesTracksList: {e.StackTrace}");
                }
            }
            return false;
        }
        #endregion

        #region Get Methods
        public static ulong GetBotChannelId(ulong GuildId)
        {
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();

                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    ulong ret = 0;
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret = reader.GetUInt64("BotChannel");
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in GetBotChannelId --> Return \"{ret}\"");
                    return ret;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetBotChannelId: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetBotChannelId: {e.StackTrace}");
                }
            }
            return 0;
        }
        public static ulong GetBotChannelMainMessageId(ulong GuildId)
        {
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    ulong ret = 0;
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret = reader.GetUInt64("BotChannelMainMessage");
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in GetBotChannelMainMessageId --> Return \"{ret}\"");
                    return ret;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetBotChannelMainMessageId: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetBotChannelMainMessageId: {e.StackTrace}");
                }
            }
            return 0;
        }
        public static ulong GetBotChannelBannerMessageId(ulong GuildId)
        {
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();

                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    ulong ret = 0;
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret = reader.GetUInt64("BotChannelBannerMessage");
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in GetBotChannelBannerMessageId --> Return \"{ret}\"");
                    return ret;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetBotChannelBannerMessageId: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetBotChannelBannerMessageId: {e.StackTrace}");
                }
            }
            return 0;
        }
        public static bool GetAfkState(ulong GuildId)
        {
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();

                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    Int16 ret = 0;
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret = reader.GetInt16("AFKState");
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in GetAfkState --> Return \"{ret}\"");
                    if (ret == 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetAfkState: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetAfkState: {e.StackTrace}");
                }
            }
            return false;
        }
        public static List<Tuple<DiscordRole, Lava.role>> GetGuildRoles(ulong GuildId)
        {
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();

                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    List<Tuple<DiscordRole, Lava.role>> ret = null;
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret = JsonConvert.DeserializeObject<List<Tuple<DiscordRole, Lava.role>>>(reader.GetString("GuildRoles"));
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in GetGuildRoles --> Return List with {ret.Count} Objects");
                    return ret;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetGuildRoles: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetGuildRoles: {e.StackTrace}");
                }
            }
            return null;
        }
        public static List<LavalinkTrack> GetFavoritesTracksLists(ulong GuildId)
        {
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    List<LavalinkTrack> ret = null;
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret = JsonConvert.DeserializeObject<List<LavalinkTrack>>(reader.GetString("FavoritesTracksList"));
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in GetFavoritesTracksLists --> Return List with {ret.Count} Objects");
                    return ret;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetFavoritesTracksLists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetFavoritesTracksLists: {e.StackTrace}");
                }
            }
            return null;
        }
        public static List<ulong> GetAllGuildsIds()
        {
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();

                    command.CommandText = "SELECT * FROM data";
                    List<ulong> ret = new List<ulong>();
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret.Add(reader.GetUInt64("GuildId"));
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in GetAllGuildsIds --> Return List with {ret.Count} Objects");
                    return ret;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetAllGuildsIds: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" GetAllGuildsIds: {e.StackTrace}");
                }
            }
            return null;
        }
        #endregion

        #region Delete Methods
        public static bool DeleteGuild(ulong GuildId)
        {
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();

                    command.CommandText = "DELETE FROM data WHERE GuildId=@id";
                    command.Parameters.AddWithValue("@id", GuildId);
                    int ret = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in DeleteGuild --> Return {ret} deleted ");
                    if (ret <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" DeleteGuild: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" DeleteGuild: {e.StackTrace}");
                }
            }
            return false;
        }
        #endregion

        #region Add Methods
        public static bool AddNewGuild(ulong GuildId, string Prefix)
        {
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "INSERT INTO data (GuildId, Prefix) VALUES (@gid, @Prefix);";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@Prefix", Prefix);
                    int retval = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in AddNewGuild --> Return {retval} added");
                    if (retval <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" AddNewGuild: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" AddNewGuild: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool AddFavoritesTracksLists(ulong GuildId, LavalinkTrack track)
        {
            List<LavalinkTrack> ret = null;
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret = JsonConvert.DeserializeObject<List<LavalinkTrack>>(reader.GetString("FavoritesTracksList"));
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in AddFavoritesTracksLists --> Return List with {ret.Count} Objects");
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" AddFavoritesTracksLists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" AddFavoritesTracksLists: {e.StackTrace}");
                }
            }
            if (ret == null)
                return false;
            ret.Add(track);
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE data SET FavoritesTracksList=@tracks WHERE GuildId=@gid;";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@tracks", JsonConvert.SerializeObject(ret));
                    int retval = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in AddFavoritesTracksLists --> Return {retval} updated");
                    if (retval <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" AddFavoritesTracksLists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" AddFavoritesTracksLists: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool AddGuildRole(ulong GuildId, Tuple<DiscordRole, Lava.role> role)
        {
            List<Tuple<DiscordRole, Lava.role>> ret = null;
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret = JsonConvert.DeserializeObject<List<Tuple<DiscordRole, Lava.role>>>(reader.GetString("GuildRoles"));
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in AddGuildRole --> Return List with {ret.Count} Objects");
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" AddGuildRole: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" AddGuildRole: {e.StackTrace}");
                }
            }
            if (ret == null)
                return false;
            ret.Add(role);
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE data SET GuildRoles=@GuildRoles WHERE GuildId=@gid;";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@GuildRoles", JsonConvert.SerializeObject(ret));
                    int retval = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in AddGuildRole --> Return {retval} updated");
                    if (retval <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" AddGuildRole: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" AddGuildRole: {e.StackTrace}");
                }
            }
            return false;
        }
        #endregion

        #region Remove Methods
        public static bool RemoveFavoritesTracksLists(ulong GuildId, LavalinkTrack track)
        {
            List<LavalinkTrack> ret = null;
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret = JsonConvert.DeserializeObject<List<LavalinkTrack>>(reader.GetString("FavoritesTracksList"));
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in RemoveFavoritesTracksLists --> Return List with {ret.Count} Objects");
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" RemoveFavoritesTracksLists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" RemoveFavoritesTracksLists: {e.StackTrace}");
                }
            }
            if (ret == null)
                return false;

            if (ret.RemoveAll(x => x.Identifier == track.Identifier) <= 0) return false;

            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE data SET FavoritesTracksList=@tracks WHERE GuildId=@gid;";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@tracks", JsonConvert.SerializeObject(ret));
                    int retval = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in RemoveFavoritesTracksLists --> Return {retval} updated");
                    if (retval <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" RemoveFavoritesTracksLists: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" RemoveFavoritesTracksLists: {e.StackTrace}");
                }
            }
            return false;
        }
        public static bool RemoveGuildRole(ulong GuildId, Tuple<DiscordRole, Lava.role> role)
        {
            List<Tuple<DiscordRole, Lava.role>> ret = null;
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM data WHERE GuildId=@gid LIMIT 1";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            ret = JsonConvert.DeserializeObject<List<Tuple<DiscordRole, Lava.role>>>(reader.GetString("GuildRoles"));
                        }
                    }
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in RemoveGuildRole --> Return List with {ret.Count} Objects");
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" RemoveGuildRole: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" RemoveGuildRole: {e.StackTrace}");
                }
            }
            if (ret == null)
                return false;
            ret.Remove(role);
            using (MySqlConnection connection = new MySqlConnection(Variables.MysqlConnectionString))
            {
                try
                {
                    connection.Open();
                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = "UPDATE data SET GuildRoles=@GuildRoles WHERE GuildId=@gid;";
                    command.Parameters.AddWithValue("@gid", GuildId);
                    command.Parameters.AddWithValue("@GuildRoles", JsonConvert.SerializeObject(ret));
                    int retval = command.ExecuteNonQuery();
                    connection.Close();
                    Variables.Logger.LogDebug(new EventId(8001, "Data"), $"Execute \"{command.CommandText}\" in AddGuildRole --> Return {retval} updated");
                    if (retval <= 0)
                        return false;
                    else
                        return true;
                }
                catch (Exception e)
                {
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" RemoveGuildRole: {e.Message}");
                    Variables.Logger.LogError(new EventId(7799, "DataDB"), $" RemoveGuildRole: {e.StackTrace}");
                }
            }
            return false;
        }
        #endregion
    }
}
