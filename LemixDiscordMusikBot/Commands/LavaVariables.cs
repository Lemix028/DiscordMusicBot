using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace LemixDiscordMusikBot.Commands
{
    [Serializable]
    public struct LavaVariables
    {

        [JsonProperty("BotChannels")]
        public Dictionary<ulong, ulong> BotChannels { get; set; }
        [JsonProperty("CheckAFKStates")]
        public Dictionary<ulong, Boolean> CheckAFKStates { get; set; }
        [JsonProperty("AnnounceStates")]
        public Dictionary<ulong, Boolean> AnnounceStates { get; set; }
        [JsonProperty("BotChannelBannerMessages")]
        public Dictionary<ulong, ulong> BotChannelBannerMessages { get; set; }
        [JsonProperty("BotChannelMainMessages")]
        public Dictionary<ulong, ulong> BotChannelMainMessages { get; set; }
        [JsonProperty("TrackLoadPlaylists")]
        public Dictionary<ulong, List<LavalinkTrack>> TrackLoadPlaylists { get; set; }
        [JsonProperty("FavoritesTracksLists")]
        public Dictionary<ulong, List<LavalinkTrack>> FavoritesTracksLists { get; set; }
        [JsonProperty("GuildRoles")]
        public Dictionary<ulong, List<Tuple<DiscordRole, Lava.role>>> GuildRoles { get; set; }

    }
}
