using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LemixDiscordMusikBot.Classes.Structures
{
    [Serializable]
    public struct LyricsStruct
    {
        
            [JsonProperty("lyrics")]
            public string Lyrics { get; set; }
    }
}
