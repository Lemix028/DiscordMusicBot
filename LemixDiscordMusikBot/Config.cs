using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;


namespace LemixDiscordMusikBot
{
    public struct Config
    {
        [JsonProperty("token")]
        public String Token { get; private set; }
        [JsonProperty("prefix")]
        public String[] Prefix { get; private set; }
        [JsonProperty("LavalinkServerIP")]
        public String LavalinkServerIP { get; private set; }
        [JsonProperty("LavalinkServerPort")]
        public int LavalinkServerPort { get; private set; }
        [JsonProperty("LavalinkServerPassword")]
        public String LavalinkServerPassword { get; private set; }
        [JsonProperty("DatabaseHostname")]
        public String DatabaseHostname { get; private set; }
        [JsonProperty("DatabaseDbName")]
        public String DatabaseDbName { get; private set; }
        [JsonProperty("DatabaseUid")]
        public String DatabaseUid { get; private set; }
        [JsonProperty("DatabasePassword")]
        public String DatabasePassword { get; private set; }
        [JsonProperty("DatabasePort")]
        public int DatabasePort { get; private set; }
        [JsonProperty("StatusItems")]
        public StatusItem[] StatusItems { get; private set; }
        [JsonProperty("StatusRefreshTimer")]
        public int StatusRefreshTimer { get; private set; }
        [JsonProperty("NoSongPicture")]
        public String NoSongPicture { get; private set; }
        [JsonProperty("DefaultVolume")]
        public int DefaultVolume { get; private set; }

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
