using System;
using System.Collections.Generic;
using System.Text;

namespace LemixDiscordMusikBot
{
    class SystemUsageItem
    {
        public int Ping { set; get; }
        public double DiscordBotCPU { set; get; }
        public double DiscordBotRAM { set; get; }
        public double LavalinkCPU { set; get; }
        public long LavalinkRAM { set; get; }

        public SystemUsageItem(int ping, double discordBotCPU, double discordBotRAM, double lavalinkCPU, long lavalinkRAM)
        {
            Ping = ping;
            DiscordBotCPU = discordBotCPU;
            DiscordBotRAM = discordBotRAM;
            LavalinkCPU = lavalinkCPU;
            LavalinkRAM = lavalinkRAM;
        }
    }
}
