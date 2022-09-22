using DSharpPlus;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LemixDiscordMusikBot.Classes
{
    public static class Variables
    {
        public static Dictionary<int, EventWaitHandle> WaitForLavalinkConnect = new Dictionary<int, EventWaitHandle>();
        public static string MysqlConnectionString = "";
        public static ILogger<BaseDiscordClient> Logger = null;
        public static DiscordShardedClient DiscordShardedClient;
    }
}
