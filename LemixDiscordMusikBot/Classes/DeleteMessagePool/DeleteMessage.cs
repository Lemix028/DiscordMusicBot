using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace LemixDiscordMusikBot
{
    public class DeleteMessage
    {
        public DiscordChannel Channel { get; }
        public DiscordMessage Message { get; }
        public DateTimeOffset DateTime { get; }
        public DeleteMessage(DiscordChannel chn, DiscordMessage msg)
        {
            this.Channel = chn;
            this.Message = msg;
            this.DateTime = DateTimeOffset.Now;
        }
    }
}
