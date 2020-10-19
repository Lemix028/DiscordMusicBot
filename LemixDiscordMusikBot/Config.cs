using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;


namespace LemixDiscordMusikBot
{
    public struct Config
    {
        [JsonProperty("token")]
        public String Token { get;  set; }
        [JsonProperty("prefix")]
        public String[] Prefix { get;  set; }
        [JsonProperty("BotUsername")]
        public String BotUsername { get; set; }
        [JsonProperty("LavalinkServerIP")]
        public String LavalinkServerIP { get;  set; }
        [JsonProperty("LavalinkServerPort")]
        public int LavalinkServerPort { get;  set; }
        [JsonProperty("LavalinkServerPassword")]
        public String LavalinkServerPassword { get;  set; }
        [JsonProperty("DatabaseHostname")]
        public String DatabaseHostname { get;  set; }
        [JsonProperty("DatabaseDbName")]
        public String DatabaseDbName { get;  set; }
        [JsonProperty("DatabaseUid")]
        public String DatabaseUid { get;  set; }
        [JsonProperty("DatabasePassword")]
        public String DatabasePassword { get;  set; }
        [JsonProperty("DatabasePort")]
        public int DatabasePort { get;  set; }
        [JsonProperty("StatusItems")]
        public StatusItem[] StatusItems { get;  set; }
        [JsonProperty("StatusRefreshTimer")]
        public int StatusRefreshTimer { get;  set; }
        [JsonProperty("NoSongPicture")]
        public String NoSongPicture { get;  set; }
        [JsonProperty("DefaultVolume")]
        public int DefaultVolume { get;  set; }

    }
    public struct StatusItem
    {
        [JsonProperty("Text")]
        public String Text { get; set; }
        [JsonProperty("Activity")]
        public ActivityType Activity { get; set; }
        [JsonProperty("StatusType")]
        public UserStatus StatusType { get; set; }
    }
}
