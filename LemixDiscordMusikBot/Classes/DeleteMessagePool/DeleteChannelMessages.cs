using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace LemixDiscordMusikBot
{
    class DeleteChannelMessages
    {
        public DiscordChannel Channel { get; }
        public List<DiscordMessage> Messages = new List<DiscordMessage>();
        public DeleteChannelMessages(DiscordChannel chn, DiscordMessage msg)
        {
            this.Channel = chn;
            Messages.Add(msg);
        }
    }
}
