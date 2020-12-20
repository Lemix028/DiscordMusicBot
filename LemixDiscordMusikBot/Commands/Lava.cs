using AngleSharp.Common;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
using DSharpPlus.Net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Interactivity;
using System.Timers;
using System.Data;
using System.Reflection;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Globalization;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using System.Diagnostics;
using System.Management;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;
using DSharpPlus.Interactivity.Extensions;

namespace LemixDiscordMusikBot.Commands
{

    public class Lava : BaseCommandModule
    {
        private LavalinkNodeConnection Lavalink { get; set; }
        private Dictionary<ulong, LavalinkGuildConnection> VoiceConnections = new Dictionary<ulong, LavalinkGuildConnection>();
        private Dictionary<LavalinkGuildConnection, int> Volumes = new Dictionary<LavalinkGuildConnection, int>();
        private Dictionary<ulong, DateTimeOffset> AFKTimeOffsets = new Dictionary<ulong, DateTimeOffset>();
        private Dictionary<ulong, ulong> BotChannels = new Dictionary<ulong, ulong>();
        private Dictionary<ulong, Boolean> CheckAFKStates = new Dictionary<ulong, Boolean>();
        private Dictionary<ulong, Boolean> AnnounceStates = new Dictionary<ulong, Boolean>();
        private Dictionary<ulong, Boolean> IsPausedStates = new Dictionary<ulong, Boolean>();
        private Dictionary<ulong, ulong> BotChannelBannerMessages = new Dictionary<ulong, ulong>();
        private Dictionary<ulong, ulong> BotChannelMainMessages = new Dictionary<ulong, ulong>();
        private Dictionary<ulong, DeleteMessage> DeletePool = new Dictionary<ulong, DeleteMessage>();
        private Dictionary<ulong, List<LavalinkTrack>> FavoritesTracksLists = new Dictionary<ulong, List<LavalinkTrack>>();
        private Dictionary<ulong, loopmode> Loopmodes = new Dictionary<ulong, loopmode>();
        private Dictionary<ulong, Boolean> Cooldown = new Dictionary<ulong, Boolean>();
        private Dictionary<ulong, Boolean> ReactionCooldown = new Dictionary<ulong, Boolean>();
        private Dictionary<ulong, Boolean> VoteSkip = new Dictionary<ulong, Boolean>();
        private Dictionary<ulong, List<Tuple<DiscordRole, role>>> GuildRoles = new Dictionary<ulong, List<Tuple<DiscordRole, role>>>();
        private Dictionary<DateTime, SystemUsageItem> SystemUsageLog = new Dictionary<DateTime, SystemUsageItem>();

       

        private Config configJson;
        private ConnectionEndpoint conEndPoint;
        private DBConnection db;
        int CommandCooldown = 1000; // ms
        int LastHourStatistic;

        public Dictionary<ulong, List<LavalinkTrack>> TrackLoadPlaylists = new Dictionary<ulong, List<LavalinkTrack>>();
        public Dictionary<ulong, int> CurrentSong = new Dictionary<ulong, int>();

        enum loopmode
        {
            off,
            loopqueue,
            loopsong
        };
        public enum role
        {
            admin,
            dj,
            everyone
        };


        int AFKCheckInterval = 1; // In minutes

        public Lava()
        {
            var json = string.Empty;
          //  AppDomain.CurrentDomain.ProcessExit += OnProgramExit;
            Console.CancelKeyPress += OnProgramExit;
            try
            {
                using (var fs = File.OpenRead("config.json"))
                using (StreamReader sr = new StreamReader(fs, new UTF8Encoding(false)))
                    json = sr.ReadToEnd();

                configJson = JsonConvert.DeserializeObject<Config>(json);
                conEndPoint.Hostname = configJson.LavalinkServerIP;
                conEndPoint.Port = configJson.LavalinkServerPort;
            }
            catch
            { }
            
            Timer AFKtimer = new Timer();
            AFKtimer.Interval = 100;
            AFKtimer.Elapsed += CheckIsAFK;
            AFKtimer.Start();
        }

        private void OnProgramExit(object sender, EventArgs e)
        {
            SaveJsonToDatabase();
            Environment.Exit(0);
        }

        private void DeleteAllMsgInPool(object sender, ElapsedEventArgs e, CommandContext ctx)
        {
              List<DeleteChannelMessages> ToDeleteMessages = new List<DeleteChannelMessages>();
            if (DeletePool.Count != 0)
            {
                foreach (DeleteMessage entry in DeletePool.Values.ToList())
                {
                    if (!(entry.DateTime.AddSeconds(7) <= DateTime.Now))
                        continue;
                    if(ToDeleteMessages.Count == 0)
                    {
                        ToDeleteMessages.Add(new DeleteChannelMessages(entry.Channel, entry.Message));
                        DeletePool.Remove(entry.Message.Id);
                        continue;
                    }
                    else
                    {
                        foreach (DeleteChannelMessages dcm in ToDeleteMessages.ToList())
                        {
                            if (dcm.Channel == entry.Channel)
                            {
                                dcm.Messages.Add(entry.Message);
                                DeletePool.Remove(entry.Message.Id);
                                continue;
                            }
                            else
                            {
                                ToDeleteMessages.Add(new DeleteChannelMessages(entry.Channel, entry.Message));
                                DeletePool.Remove(entry.Message.Id);
                                continue;
                            }
                        }
                    }

                }
            }
            if (ToDeleteMessages.Count == 0)
                return;
            foreach (DeleteChannelMessages dcm in ToDeleteMessages)
            {
                try
                {
                    dcm.Channel.DeleteMessagesAsync(dcm.Messages.AsEnumerable());
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            }
            ToDeleteMessages.Clear();
        }

        private void SaveVariables(object sender, ElapsedEventArgs e)
        {
            SaveJsonToDatabase();
          //  WriteToJsonFile();
        }

        private void ReadVariables()
        {
            try
            {
                
                var Content = ReadJsonFromDatabase();
                //  var Content = ReadFromJsonFile();
                
                if(Content.BotChannels != null)
                {
                    foreach (KeyValuePair<ulong, ulong> entry in Content.BotChannels)
                    {
                        BotChannels.Add(entry.Key, entry.Value);
                    }
                }
                if (Content.CheckAFKStates != null)
                {
                    foreach (KeyValuePair<ulong, Boolean> entry in Content.CheckAFKStates)
                    {
                        CheckAFKStates.Add(entry.Key, entry.Value);
                    }
                }

                if (Content.AnnounceStates != null)
                {
                    foreach (KeyValuePair<ulong, Boolean> entry in Content.AnnounceStates)
                    {
                        AnnounceStates.Add(entry.Key, entry.Value);
                    }
                }
                if (Content.BotChannelBannerMessages != null)
                {
                    foreach (KeyValuePair<ulong, ulong> entry in Content.BotChannelBannerMessages)
                    {
                        BotChannelBannerMessages.Add(entry.Key, entry.Value);
                    }
                }
                if (Content.BotChannelMainMessages != null)
                {
                    foreach (KeyValuePair<ulong, ulong> entry in Content.BotChannelMainMessages)
                    {
                        BotChannelMainMessages.Add(entry.Key, entry.Value);
                    }
                }
                if (Content.FavoritesTracksLists != null)
                {
                    foreach (KeyValuePair<ulong, List<LavalinkTrack>> entry in Content.FavoritesTracksLists)
                    {
                        FavoritesTracksLists.Add(entry.Key, entry.Value);
                    }
                }
                if (Content.GuildRoles != null)
                {
                    foreach (KeyValuePair<ulong, List<Tuple<DiscordRole, role>>> entry in Content.GuildRoles)
                    {
                        GuildRoles.Add(entry.Key, entry.Value);
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }


        }

        [Command("init"), Description("4")]
        public async Task GetNodeConnection(CommandContext ctx)
        {
            //Init Database
            db = new DBConnection(ctx.Client.Logger, configJson);
            ReadVariables();
            System.Timers.Timer SaveTimer = new System.Timers.Timer();
            SaveTimer.Interval = 60000;
            SaveTimer.Elapsed += SaveVariables;
            SaveTimer.Start();
            this.Lavalink = ctx.Client.GetLavalink().GetNodeConnection(conEndPoint);
            if (this.Lavalink == null)
            {
                ctx.Client.Logger.LogError(new EventId(7777, "InitCommand"), $"Lavalink Node is NULL waiting 10 seconds for retry");
                await Task.Delay(10000);
                this.Lavalink = ctx.Client.GetLavalink().GetNodeConnection(conEndPoint);
                if (this.Lavalink == null)
                {

                    ctx.Client.Logger.LogError(new EventId(7777, "InitCommand"), $"Lavalink Node is NULL cancel program");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Press any Key to close the Window");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
                else
                {
                    this.Lavalink.PlaybackFinished += async (s, e) => { await VoiceConnection_PlaybackFinished(s, e); };;
                    this.Lavalink.Disconnected += async (s, e) => { await Lavalink_Disconnected(s, e); };
                    ctx.Client.VoiceStateUpdated += async (s, e) => { await VoiceStateUpdate(s, e); }; ;
                    ctx.Client.GuildDeleted += async (s, e) => { await OnGuildDeleted(s, e); }; ;
                    ctx.CommandsNext.CommandExecuted += async (s, e) => { await OnCommandExecuted(s, e); }; ;
                    ctx.CommandsNext.CommandErrored += async (s, e) => { await OnCommandErrored(s, e); }; ;
                    ctx.Client.MessageReactionAdded += async (s, e) => { await OnMessageReactionAdded(s, e); }; ;
                    ctx.Client.MessageCreated += async (s, e) => { await OnMessageCreated(s, e); }; ;


                }
            }
            else
            {
                this.Lavalink.PlaybackFinished += async (s, e) => { await VoiceConnection_PlaybackFinished(s, e); }; ;
                this.Lavalink.Disconnected += async (s, e) => { await Lavalink_Disconnected(s, e); };
                ctx.Client.VoiceStateUpdated += async (s, e) => { await VoiceStateUpdate(s, e); }; ;
                ctx.Client.GuildDeleted += async (s, e) => { await OnGuildDeleted(s, e); }; ;
                ctx.CommandsNext.CommandExecuted += async (s, e) => { await OnCommandExecuted(s, e); }; ;
                ctx.CommandsNext.CommandErrored += async (s, e) => { await OnCommandErrored(s, e); }; ;
                ctx.Client.MessageReactionAdded += async (s, e) => { await OnMessageReactionAdded(s, e); }; ;
                ctx.Client.MessageCreated += async (s, e) => { await OnMessageCreated(s, e); }; ;
            }

            Timer PoolDelete = new Timer(5000);
            PoolDelete.Elapsed += (sender, e) => DeleteAllMsgInPool(sender, e, ctx);
            PoolDelete.Start();

            foreach (KeyValuePair<ulong, ulong> entry in BotChannels)
            {

                await Task.Factory.StartNew(() => BotChannel(ctx, entry.Key));
            }
            var StatisticTimer = new Timer(1000);
            LastHourStatistic = DateTime.Now.Hour;
            StatisticTimer.Elapsed += (sender, e) => Statistic(sender, e, ctx, LastHourStatistic); 
            StatisticTimer.Start();
            var SystemUsageTimer = new Timer(1000);
            SystemUsageTimer.Elapsed += (sender, e) => SystemUsageGetterAsync(sender, e, ctx, LastHourStatistic);
            SystemUsageTimer.Start();

            ctx.CommandsNext.UnregisterCommands(ctx.Command);
        }

        private async void SystemUsageGetterAsync(object sender, ElapsedEventArgs e, CommandContext ctx, int lastHourStatistic)
        {
            SystemUsage currentSystemUsage = await GetUsageAsync();
            var LavaStats = this.Lavalink.Statistics;

            DateTime LogDate = DateTime.Now;
            int Ping = ctx.Client.Ping;
            double DiscordBotCPU = currentSystemUsage.getCPU();
            double DiscordBotRAM = currentSystemUsage.getRAM();
            double LavalinkCPU = LavaStats.CpuLavalinkLoad;
            long LavalinkRAM = LavaStats.RamUsed;

            SystemUsageLog.Add(LogDate, new SystemUsageItem(Ping, DiscordBotCPU, DiscordBotRAM, LavalinkCPU, LavalinkRAM));
        }

        private async void Statistic(object sender, ElapsedEventArgs e, CommandContext ctx, int lastHour)
        {
            if (lastHour < DateTime.Now.Hour || (lastHour == 23 && DateTime.Now.Hour == 0))
            {
                LastHourStatistic = DateTime.Now.Hour;
                SystemUsage currentSystemUsage = await GetUsageAsync();
                var LavaStats = this.Lavalink.Statistics;

                DateTime LogDate = DateTime.Now;
                int GuildCount = ctx.Client.Guilds.Count;
                int ActiveShards = ctx.Client.ShardCount;
                TimeSpan DiscordBotUptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                TimeSpan LavalinkUptime = LavaStats.Uptime;
                int LavalinkPlayersTotal = LavaStats.TotalPlayers;
                int LavalinkPlayersActive = LavaStats.ActivePlayers;
                List<KeyValuePair<DateTime, SystemUsageItem>> TempSystemUsageLog = SystemUsageLog.ToList();
                SystemUsageLog.Clear();
                List<int> PingItems = new List<int>();
                List<double> DiscordBotCPUItems = new List<double>();
                List<double> DiscordBotRAMItems = new List<double>();
                List<double> LavalinkCPUItems = new List<double>();
                List<long> LavalinkRAMItems = new List<long>();
                foreach (KeyValuePair<DateTime, SystemUsageItem> entry in TempSystemUsageLog.ToList())
                {
                    if((LogDate - TimeSpan.FromHours(1)) < entry.Key)
                    {
                        PingItems.Add(entry.Value.Ping);
                        DiscordBotCPUItems.Add(entry.Value.DiscordBotCPU);
                        DiscordBotRAMItems.Add(entry.Value.DiscordBotRAM);
                        LavalinkCPUItems.Add(entry.Value.LavalinkCPU);
                        LavalinkRAMItems.Add(entry.Value.LavalinkRAM);
                    }
                }
                
            double PingAverage = 0;
            double DiscordBotCPUAverage = 0;
            double DiscordBotRAMItemsAverage = 0;
            double LavalinkCPUItemsAverage = 0;
            double LavalinkRAMItemsAverage = 0;
            try
            {
                 PingAverage = Math.Round(PingItems.Average(), 2);
                 DiscordBotCPUAverage = Math.Round(DiscordBotCPUItems.Average(), 2);
                 DiscordBotRAMItemsAverage = Math.Round(DiscordBotRAMItems.Average(), 2);
                 LavalinkCPUItemsAverage = Math.Round(LavalinkCPUItems.Average(), 2);
                 LavalinkRAMItemsAverage = Math.Round(LavalinkRAMItems.Average(), 2);
            }
            catch { }

                db.Execute(@$"INSERT INTO dbot_statistic_log(log_date, guilds_count, ping, active_shards, dbot_cpu, dbot_ram, dbot_uptime, lavalink_cpu, lavalink_ram, lavalink_uptime, lavalink_players_total, lavalink_players_active) VALUES ('{LogDate.ToString("yyyy-MM-dd HH:mm:ss")}',{GuildCount},{PingAverage.ToString().Replace(",",".")},{ActiveShards},{DiscordBotCPUAverage.ToString().Replace(",", ".")},{DiscordBotRAMItemsAverage.ToString().Replace(",", ".")},'{String.Format("{0:00}:{1:00}:{2:00}", Math.Floor(DiscordBotUptime.TotalHours), DiscordBotUptime.Minutes, DiscordBotUptime.Seconds)}',{LavalinkCPUItemsAverage.ToString().Replace(",",".")},{SizeToString(Convert.ToInt64(LavalinkRAMItemsAverage), false).ToString().Replace(",", ".")},'{String.Format("{0:00}:{1:00}:{2:00}", Math.Floor(LavalinkUptime.TotalHours), LavalinkUptime.Minutes, LavalinkUptime.Seconds)}',{LavalinkPlayersTotal},{LavalinkPlayersActive});");
                PingItems.Clear();
                DiscordBotCPUItems.Clear();
                DiscordBotRAMItems.Clear();
                LavalinkCPUItems.Clear();
                LavalinkRAMItems.Clear();

              }
        }

        //Add all messages in Botchannel for delete
        private Task OnMessageCreated(DiscordClient s, MessageCreateEventArgs e) {
            if (e.Guild != null)
            {
                if (!BotChannels.ContainsKey(e.Guild.Id))
                    return Task.CompletedTask;
            } else
            {
               // s.Logger.LogDebug(new EventId(8898), "Guild is null");
                return Task.CompletedTask;
            }

            if (e.Author.Id == s.CurrentUser.Id || e.Channel.Id != BotChannels[e.Guild.Id])
                return Task.CompletedTask;
            if(DeletePool != null)
                if(!DeletePool.ContainsKey(e.Message.Id))
                    DeletePool.Add(e.Message.Id, new DeleteMessage(e.Channel, e.Message));
            return Task.CompletedTask;
        }

        private async Task OnMessageReactionAdded(DiscordClient sender, MessageReactionAddEventArgs e)
        {
                if (!BotChannelMainMessages.ContainsKey(e.Guild.Id))
                    return;
                if (e.Message.Id == BotChannelMainMessages[e.Guild.Id] && e.User.Id != sender.CurrentUser.Id)
                {
                    if (!ReactionCooldown.ContainsKey(e.Guild.Id))
                        return;
                    await Task.Delay(500);
                    if (ReactionCooldown.ContainsKey(e.Guild.Id))
                        ReactionCooldown[e.Guild.Id] = false;

                }
                

            return;
        }

        private async Task<Task> OnCommandErrored(CommandsNextExtension s, CommandErrorEventArgs e)
        {
            var UnkownCommandEmbed = new DiscordEmbedBuilder
            {
                Title = $"Command not found!",
                Description = $"{e.Context.Message.Content}",
                Color = DiscordColor.Gray
            };
            var UnkownEmbed = new DiscordEmbedBuilder
            {
                Title = $"Unkown Error!",
                Description = $"If this problem persists please report it to support\n Use: {e.Context.Prefix}support",
                Color = DiscordColor.Red
            };
            if (e.Exception is DSharpPlus.CommandsNext.Exceptions.CommandNotFoundException)
            {
                var msg = await e.Context.Channel.SendMessageAsync(embed: UnkownCommandEmbed);
                if (!DeletePool.ContainsKey(msg.Id))
                    DeletePool.Add(msg.Id, new DeleteMessage(e.Context.Channel, msg));
            }
            else
            {
                s.Client.Logger.LogError(new EventId(7776, "UnkownError"), $"Unknown command error has occurred! (Command: {e.Context.Message.Content} Exception: {e.Exception})");
                var msg = await e.Context.Channel.SendMessageAsync(embed: UnkownEmbed);
                if (!DeletePool.ContainsKey(msg.Id))
                    DeletePool.Add(msg.Id, new DeleteMessage(e.Context.Channel, msg));
            }
            if(e.Context.Guild == null)
                return Task.CompletedTask;
            if (!Cooldown.ContainsKey(e.Context.Guild.Id))
                return Task.CompletedTask;
          //  await Task.Delay(CommandCooldown);
            if (Cooldown.ContainsKey(e.Context.Guild.Id))
                Cooldown[e.Context.Guild.Id] = false;
            return Task.CompletedTask;
        }

        private async Task<Task> OnCommandExecuted(CommandsNextExtension s, CommandExecutionEventArgs e)
        {
            try  
            {
                if (e != null)
                    if (e?.Context?.Guild != null)
                    {
                        if (!Cooldown.ContainsKey(e.Context.Guild.Id)) { return Task.CompletedTask;  }
                        await Task.Delay(CommandCooldown);
                        if (Cooldown.ContainsKey(e.Context.Guild.Id))
                            Cooldown[e.Context.Guild.Id] = false;
                    }
               
            }
            catch (Exception e1)
            {
                Console.WriteLine(e1);
            }
            return Task.CompletedTask;
        }

        private Task OnGuildDeleted(DiscordClient s, GuildDeleteEventArgs e)
        {
            try
            {
             //   Volumes.Remove(VoiceConnections[e.Guild.Id]);
                VoiceConnections.Remove(e.Guild.Id);
                BotChannels.Remove(e.Guild.Id);
                AFKTimeOffsets.Remove(e.Guild.Id);
                IsPausedStates.Remove(e.Guild.Id);
                TrackLoadPlaylists.Remove(e.Guild.Id);
                Loopmodes.Remove(e.Guild.Id);
                CheckAFKStates.Remove(e.Guild.Id);
                AnnounceStates.Remove(e.Guild.Id);
                BotChannelBannerMessages.Remove(e.Guild.Id);
                BotChannelMainMessages.Remove(e.Guild.Id);
                FavoritesTracksLists.Remove(e.Guild.Id);
                return Task.CompletedTask;
            }
            catch (Exception e1)
            {
                Console.WriteLine(e1);
            }

            return Task.CompletedTask;
    }

        private async Task VoiceStateUpdate(DiscordClient s, VoiceStateUpdateEventArgs e)
        {
            if(e.User.Id == s.CurrentUser.Id)
            {
                if (!VoiceConnections.TryGetValue(e.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                    return;
                if (e.Before.Channel?.Id != VoiceConnection.Channel?.Id && e.Before.Channel?.Id != null)
                {
                    try
                    {
                        var chn = e.Guild.GetChannel(BotChannels[e.Guild.Id]);
                        var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[e.Guild.Id]);
                        var reader = db.Query($"SELECT Prefix FROM data WHERE GuildId IN ({e.Guild.Id})");
                        string prefix = String.Empty;
                        while (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                if (reader.GetString(0) != String.Empty)
                                    prefix = reader.GetString(0);
                            }

                            await reader.NextResultAsync();
                        }
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {prefix}"); // PREFIX
                        if(VoiceConnection.IsConnected)
                            await VoiceConnection.DisconnectAsync();
                        VoiceConnections.Remove(e.Guild.Id);
                        Volumes.Remove(VoiceConnection);
                        AFKTimeOffsets.Remove(e.Guild.Id);
                        IsPausedStates.Remove(e.Guild.Id);
                        TrackLoadPlaylists[e.Guild.Id].Clear();
                        Loopmodes.Remove(e.Guild.Id);
                    }
                    catch (Exception e1){
                        Console.WriteLine(e1);
                    }

                }
            }
            return;
        }
        public async Task BotChannel(CommandContext ctx, ulong GuildId)
        {
            try
            {
                if (!BotChannels.ContainsKey(GuildId))
                    return;
                var chnid = BotChannels[GuildId];
                DiscordChannel chn = null;
                
                try { chn = await ctx.Client.GetChannelAsync(chnid); } catch { return; }
                var interactivity = ctx.Client.GetInteractivity();
                DiscordMessage BannerMsg;
                DiscordMessage MainMsg;
                FileStream fs;

                DiscordEmoji PlayPause = DiscordEmoji.FromName(ctx.Client, ":play_pause:");
                DiscordEmoji Stop = DiscordEmoji.FromName(ctx.Client, ":stop_button:");
                DiscordEmoji NextTrack = DiscordEmoji.FromName(ctx.Client, ":track_next:");
                DiscordEmoji Loop = DiscordEmoji.FromName(ctx.Client, ":arrows_counterclockwise:");
                DiscordEmoji TwistedArrows = DiscordEmoji.FromName(ctx.Client, ":twisted_rightwards_arrows:");
                DiscordEmoji Star = DiscordEmoji.FromName(ctx.Client, ":star:");
                DiscordEmoji Crossed = DiscordEmoji.FromName(ctx.Client, ":x:");
                DiscordEmoji l_char = DiscordEmoji.FromName(ctx.Client, ":regional_indicator_l:");

                if (!BotChannelBannerMessages.ContainsKey(GuildId))
                {
                    // Need final rework maybe integrated
                    fs = File.OpenRead(System.Environment.CurrentDirectory + @"/pics/banner.png");
                    BannerMsg = await chn.SendFileAsync(fs);
                    BotChannelBannerMessages.Add(GuildId, BannerMsg.Id);
                }
                else
                {
                    try { BannerMsg = await chn.GetMessageAsync(BotChannelBannerMessages[GuildId]); } catch { return; }
                   
                }

                if (!BotChannelMainMessages.ContainsKey(GuildId))
                {
                    var Mainembed = new DiscordEmbedBuilder
                    {
                        Title = "No song playing currently",
                        Description = "[Discord](https://discord.gg/JbPfCTA) | [Invite](https://discord.com/oauth2/authorize?client_id=696739313307746334&permissions=3271760&scope=bot)",
                        Color = DiscordColor.Orange
                    };
                    Mainembed.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
                    Mainembed.WithImageUrl(configJson.NoSongPicture);
                    MainMsg = await chn.SendMessageAsync(embed: Mainembed);
                    await MainMsg.CreateReactionAsync(PlayPause);
                    await MainMsg.CreateReactionAsync(Stop);
                    await MainMsg.CreateReactionAsync(NextTrack);
                    await MainMsg.CreateReactionAsync(Loop);
                    await MainMsg.CreateReactionAsync(TwistedArrows);
                    await MainMsg.CreateReactionAsync(Star);
                    await MainMsg.CreateReactionAsync(Crossed);
                    await MainMsg.CreateReactionAsync(l_char);

                    BotChannelMainMessages.Add(GuildId, MainMsg.Id);
                }
                else
                {
                    MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[GuildId]);
                }
                //   if (restore)
                //return;
                while (true)
                {
                    await Task.Delay(500);
                    var result = await interactivity.WaitForReactionAsync(x => x.Message == MainMsg && x.User != ctx.Client.CurrentUser);
                    if (result.Result == null)
                        continue;
                    if (CheckHasReactionCooldown(GuildId))
                    {
                     //   SendReactionCooldown(chn);
                        continue;
                    }
                    await MainMsg.DeleteReactionAsync(result.Result.Emoji, result.Result.User);
                    if (result.Result.Emoji == PlayPause)
                    {
                        if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                            continue;

                        if (TrackLoadPlaylists[GuildId].Count == 0)
                            continue;
                        if (!IsPausedStates[GuildId])
                        {
                            await VoiceConnection.PauseAsync();
                            IsPausedStates[GuildId] = !IsPausedStates[GuildId];
                            var track = TrackLoadPlaylists[GuildId].First();
                            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Paused at `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(GuildId)}{GetFavoriteMessage(GuildId, track)}");
                        }
                        else if (IsPausedStates[GuildId])
                        {
                            await VoiceConnection.ResumeAsync();
                            IsPausedStates[GuildId] = !IsPausedStates[GuildId];
                            var track = TrackLoadPlaylists[GuildId].First();
                            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(GuildId)}{GetFavoriteMessage(GuildId, track)}");
                        }

                    }
                    else if (result.Result.Emoji == NextTrack)
                    {

                        if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                            continue;
                        if (await CheckIsBotChannelAndMessagesExits(ctx, GuildId))
                            continue;
                        if (TrackLoadPlaylists[GuildId].Count <= 1)
                        {
                            result.Result.Guild.Members.TryGetValue(result.Result.User.Id, out DiscordMember member);
                            if (CheckHasCooldown(ctx, GuildId))
                            {
                                SendCooldownAsync(ctx);
                                continue;
                            }
                            if (CheckHasPermission(ctx, role.everyone, member, GuildId))
                                continue;
                     
                            if (member.VoiceState?.Channel != VoiceConnection.Channel || member.VoiceState?.Channel == null)
                            {
                                continue;
                            }
                            TrackLoadPlaylists[GuildId].Clear();
                            await VoiceConnection.StopAsync().ConfigureAwait(false);
                            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {ctx.Prefix}"); // PREFIX
                            continue;
                        }
                        TrackLoadPlaylists[GuildId].RemoveAt(0);
                        var track = TrackLoadPlaylists[GuildId].First();
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(GuildId)}{GetFavoriteMessage(GuildId, track)}");
                        await VoiceConnection.PauseAsync();
                        await VoiceConnection.PlayAsync(track);
                    }
                    else if (result.Result.Emoji == Stop)
                    {
                        if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                            continue;

                        TrackLoadPlaylists[GuildId].Clear();
                        await VoiceConnection.StopAsync();
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {ctx.Prefix}"); // PREFIX

                    }
                    else if (result.Result.Emoji == Loop)
                    {
                        if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                            continue;

                        if (TrackLoadPlaylists[GuildId].Count == 0)
                            continue;

                        if (Loopmodes[GuildId] == loopmode.off)
                        {
                            Loopmodes[GuildId] = loopmode.loopqueue;
                        }
                        else if (Loopmodes[GuildId] == loopmode.loopqueue)
                        {
                            Loopmodes[GuildId] = loopmode.loopsong;
                        }
                        else if (Loopmodes[GuildId] == loopmode.loopsong)
                        {
                            Loopmodes[GuildId] = loopmode.off;
                        }
                        
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{TrackLoadPlaylists[GuildId].First().Title}`", ImageUrl: getThumbnail(TrackLoadPlaylists[GuildId].First()), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(GuildId)}{GetFavoriteMessage(GuildId, TrackLoadPlaylists[GuildId].First())}"); 

                    }
                    else if (result.Result.Emoji == TwistedArrows)
                    {
                        if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                            continue;
                        if (TrackLoadPlaylists[GuildId].Count == 0)
                            continue;

                        await VoiceConnection.PauseAsync();
                        Utils.Shuffle(TrackLoadPlaylists[GuildId]);
                        var track = TrackLoadPlaylists[GuildId].First();
                        await VoiceConnection.PlayAsync(track);
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(GuildId)}{GetFavoriteMessage(GuildId, track)}");
                    }
                    else if (result.Result.Emoji == Star)
                    {
                        if (TrackLoadPlaylists[GuildId].Count == 0)
                            continue;
                        if (!FavoritesTracksLists.ContainsKey(GuildId))
                            continue;
                        if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                            continue;

                        Boolean abort = false;
                        LavalinkTrack track = TrackLoadPlaylists[GuildId].First();
                        foreach (LavalinkTrack entry in FavoritesTracksLists[GuildId])
                        {
                            if (entry.Identifier == track.Identifier)
                            {
                                var errmsg = await chn.SendMessageAsync("Already favorite.");
                                DeletePool.Add(errmsg.Id, new DeleteMessage(chn, errmsg));
                                abort = true;
                            }
                        }
                        if (abort)
                            continue;
                        FavoritesTracksLists[GuildId].Add(track);
                        var msg = await chn.SendMessageAsync("Successfully added.");
                        DeletePool.Add(msg.Id, new DeleteMessage(chn, msg));
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(GuildId)}{GetFavoriteMessage(GuildId, track)}");

                    }
                    else if (result.Result.Emoji == Crossed)
                    {

                        if (TrackLoadPlaylists[GuildId].Count == 0)
                            continue;
                        if (!FavoritesTracksLists.ContainsKey(GuildId))
                            continue;

                        if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                            continue;

                        Boolean abort = false;
                        LavalinkTrack track = TrackLoadPlaylists[GuildId].First();
                        foreach (LavalinkTrack entry in FavoritesTracksLists[GuildId].ToList())
                        {
                            if (entry.Identifier == track.Identifier)
                            {
                                FavoritesTracksLists[GuildId].Remove(entry);
                                var msg = await chn.SendMessageAsync("Successfully removed.");

                                DeletePool.Add(msg.Id, new DeleteMessage(chn, msg));
                                await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetFavoriteMessage(GuildId, track)}");
                                abort = true;
                            }
                        }
                        if (abort)
                            continue;
                        var errmsg = await chn.SendMessageAsync("Not at favorites.");
                        DeletePool.Add(errmsg.Id, new DeleteMessage(chn, errmsg));



                    }
                    else if (result.Result.Emoji == l_char)
                    {                    
                            DiscordChannel tchn = null;
                            if (!FavoritesTracksLists.ContainsKey(GuildId))
                                continue;
                            if (FavoritesTracksLists[GuildId].Count == 0)
                                continue;
                            var guild = ctx.Client.GetGuildAsync(GuildId);
                            if(result.Result == null)
                            {
                                var NoVoiceConnectionEmbed = new DiscordEmbedBuilder
                                {
                                    Description = "Guild not found.",
                                    Color = DiscordColor.Red
                                };
                                DiscordMessage msg1 = await chn.SendMessageAsync(embed: NoVoiceConnectionEmbed);
                                DeletePool.Add(msg1.Id, new DeleteMessage(chn, msg1));
                                continue;
                            }

                            foreach (DiscordChannel entry in guild.Result.Channels.Values)
                            {
                                if (entry.Users.Contains(result.Result.User) && entry.Type == ChannelType.Voice)
                                {
                                    tchn = entry;
                                    break;
                                }
                            }
                            if (tchn == null)
                            {
                                var NoVoiceConnectionEmbed = new DiscordEmbedBuilder
                                {
                                    Description = "You need to be in an Voicechannel!",
                                    Color = DiscordColor.Orange
                                };
                                DiscordMessage msg1 = await chn.SendMessageAsync(embed: NoVoiceConnectionEmbed);
                                DeletePool.Add(msg1.Id, new DeleteMessage(chn, msg1));
                                continue;
                            } 
                            else
                            {
                                await JoinAsync(ctx, tchn, GuildId);
                            }
                            
                            if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                            {
                                var NoVoiceConnectionEmbed = new DiscordEmbedBuilder
                                {
                                    Description = "The bot must already be in a voice channel.",
                                    Color = DiscordColor.Orange
                                };
                                DiscordMessage msg1 = await chn.SendMessageAsync(embed: NoVoiceConnectionEmbed);
                                DeletePool.Add(msg1.Id, new DeleteMessage(chn, msg1));
                                continue;
                            }
                                
                                
                            TrackLoadPlaylists[GuildId].Clear();
                            LavalinkLoadResult trackload;
                            foreach (LavalinkTrack entry in FavoritesTracksLists[GuildId].ToList())
                            {

                                try
                                {
                                    trackload = await Lavalink.Rest.GetTracksAsync(entry.Uri);
                                    TrackLoadPlaylists[GuildId].Add(trackload.Tracks.First());
                                }
                                catch
                                {
                                    var errmsg = await ctx.Channel.SendMessageAsync($"Song {entry?.Title} cannot be found and will be removed!");
                                    FavoritesTracksLists[GuildId].Remove(entry);
                                    DeletePool.Add(errmsg.Id, new DeleteMessage(ctx.Channel, errmsg));
                                }

                            }
                            var track = TrackLoadPlaylists[GuildId].First();
                            await VoiceConnection.PlayAsync(track);
                            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetFavoriteMessage(GuildId, track)}");
                    }
                }


            }
            catch (Exception e)
            {

                ctx.Client.Logger.LogCritical(new EventId(7780, "Botchannel"), e.ToString());
            }
        }
        private async Task VoiceConnection_PlaybackFinished(LavalinkGuildConnection s, TrackFinishEventArgs e)
        {
            if (e.Reason == TrackEndReason.Stopped)
                return;
            if (e.Reason != TrackEndReason.Finished)
                return;
            LavalinkGuildConnection VoiceConnection;
            VoiceConnections.TryGetValue(e.Player.Guild.Id, out VoiceConnection);
            var oldtrack = TrackLoadPlaylists[e.Player.Guild.Id].First();
            if (Loopmodes[e.Player.Guild.Id] == loopmode.loopqueue)
            {
                TrackLoadPlaylists[e.Player.Guild.Id].Add(oldtrack);
            }
            if (Loopmodes[e.Player.Guild.Id] != loopmode.loopsong)
                TrackLoadPlaylists[e.Player.Guild.Id].RemoveAt(0);
            if (Loopmodes[e.Player.Guild.Id] == loopmode.loopsong)
            {
                await VoiceConnection.PlayAsync(TrackLoadPlaylists[e.Player.Guild.Id].First());
            }

            if (!(TrackLoadPlaylists[e.Player.Guild.Id].Count <= 0))
            {
                var chn = e.Player.Guild.GetChannel(BotChannels[e.Player.Guild.Id]);
                var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[e.Player.Guild.Id]);
                var track = TrackLoadPlaylists[e.Player.Guild.Id].GetItemByIndex(CurrentSong[e.Player.Guild.Id]);

                await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(e.Player.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(e.Player.Guild.Id)}{GetFavoriteMessage(e.Player.Guild.Id, track)}");

                await VoiceConnection.PlayAsync(track);
            }
            else
            {
                var chn = e.Player.Guild.GetChannel(BotChannels[e.Player.Guild.Id]);
                var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[e.Player.Guild.Id]);
                var reader = db.Query($"SELECT Prefix FROM data WHERE GuildId IN ({e.Player.Guild.Id})");
                string prefix = String.Empty;
                while (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        if (reader.GetString(0) != String.Empty)
                            prefix = reader.GetString(0);
                    }

                    await reader.NextResultAsync();
                }
                await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {prefix}"); // PREFIX
                CurrentSong[e.Player.Guild.Id] = 0;
            }

        }

        private Task Lavalink_Disconnected(LavalinkNodeConnection s, NodeDisconnectedEventArgs e)
        {
            this.Lavalink = null;
            this.VoiceConnections.Clear();
            return Task.CompletedTask;
        }

        private void CheckIsAFK(object sender, ElapsedEventArgs e)
        {
            foreach (KeyValuePair<ulong, LavalinkGuildConnection> VoicePair in VoiceConnections)
            {
                LavalinkGuildConnection VoiceConnection = VoicePair.Value;
                DateTimeOffset dto;
                if(AFKTimeOffsets.TryGetValue(VoicePair.Key, out dto))
                {
                    if (VoiceConnection != null)
                    {
                        if (CheckAFKStates[VoicePair.Key] == false)
                            return;

                        if (VoiceConnection.CurrentState.LastUpdate == new DateTimeOffset())
                        {
                            if (dto == new DateTimeOffset())
                            {
                                AFKTimeOffsets[VoicePair.Key] = DateTimeOffset.Now;
                            }
                            else
                            {
                                if (dto.AddMinutes(AFKCheckInterval) <= DateTimeOffset.Now)
                                {
                                    AFKTimeOffsets[VoicePair.Key] = DateTimeOffset.Now;
                                    Volumes.Remove(VoiceConnection);
                                    VoiceConnection.DisconnectAsync();
                                    VoiceConnections.Remove(VoicePair.Key);
                                    TrackLoadPlaylists[VoicePair.Key].Clear();
                                    IsPausedStates.Remove(VoicePair.Key);
                                    Loopmodes.Remove(VoicePair.Key);

                                }
                            }
                        }
                        else
                        {
                            AFKTimeOffsets[VoicePair.Key] = DateTimeOffset.Now;
                            if (VoiceConnection.CurrentState.LastUpdate.AddMinutes(AFKCheckInterval).UtcDateTime <= DateTimeOffset.Now.UtcDateTime)
                            {
                                Volumes.Remove(VoiceConnection);
                                VoiceConnection.DisconnectAsync();
                                VoiceConnections.Remove(VoicePair.Key);
                                TrackLoadPlaylists[VoicePair.Key].Clear();
                                IsPausedStates.Remove(VoicePair.Key);
                                Loopmodes.Remove(VoicePair.Key);

                            }
                        }

                    }
                }
                
                
            }



        }

        /*
        [Command("connect"), Description("Connects to Lavalink")]
        public async Task ConnectAsync(CommandContext ctx, string hostname, int port, string password)
        {
            if (this.Lavalink != null)
                return;

            var lava = ctx.Client.GetLavalink();
            if (lava == null)
            {
                await ctx.RespondAsync("Lavalink is not enabled.").ConfigureAwait(false);
                return;
            }

            this.Lavalink = await lava.ConnectAsync(new LavalinkConfiguration
            {
                RestEndpoint = new ConnectionEndpoint(hostname, port),
                SocketEndpoint = new ConnectionEndpoint(hostname, port),
                Password = password
            }).ConfigureAwait(false);
            this.Lavalink.Disconnected += this.Lavalink_Disconnected;
            await ctx.RespondAsync("Connected to lavalink node.").ConfigureAwait(false);
        }

        [Command("disconnect"), Description("Disconnects from Lavalink")]
        public async Task DisconnectAsync(CommandContext ctx)
        {
            if (this.Lavalink == null)
                return;

            var lava = ctx.Client.GetLavalink();
            if (lava == null)
            {
                await ctx.RespondAsync("Lavalink is not enabled.").ConfigureAwait(false);
                return;
            }

            await this.Lavalink.StopAsync().ConfigureAwait(false);
            this.Lavalink = null;
            await ctx.RespondAsync("Disconnected from Lavalink node.").ConfigureAwait(false);
        }
        */


        /*  [Command("setup"), Description("Setup.")]
        [RequireBotPermissions(Permissions.ManageChannels)]
        [RequirePermissions(Permissions.Administrator)]
        public async Task SetupAsync(CommandContext ctx)
        {
            if (this.Lavalink == null)
            {
                GetNodeConnection(ctx);
                if (this.Lavalink == null)
                    return;
            }

            var interactivity = ctx.Client.GetInteractivity();
            var CreateNewChannelEmoji = DiscordEmoji.FromName(ctx.Client, ":regional_indicator_n:");
            var EditChannelEmoji = DiscordEmoji.FromName(ctx.Client, ":pencil2:");
            var CancelEmoji = DiscordEmoji.FromName(ctx.Client, ":x:");
            var AcceptEmoji = DiscordEmoji.FromName(ctx.Client, ":white_check_mark:");

            var Mainembed = new DiscordEmbedBuilder
            {
                Title = $"Please Respond Below",
                Color = DiscordColor.DarkGray
            };
            Mainembed.WithFooter("Idk what i write here currently ._.");

            // Fields
            if(!BotChannels.ContainsKey(ctx.Guild.Id))
                Mainembed.AddField("Add a Channel to the Bot", $"Use the reaction {CreateNewChannelEmoji} below.");
            else
                Mainembed.AddField("Remove the Channel", $"Use the reaction {EditChannelEmoji} below.");

            Mainembed.AddField("Cancel Setup", $"Use the reaction {CancelEmoji} below.");
            //Send MainMsg
            var MainembedMsg = await ctx.Channel.SendMessageAsync(embed: Mainembed).ConfigureAwait(false);
            //Create Reactions
            if (!BotChannels.ContainsKey(ctx.Guild.Id))
                await MainembedMsg.CreateReactionAsync(CreateNewChannelEmoji).ConfigureAwait(false);
            else
                await MainembedMsg.CreateReactionAsync(EditChannelEmoji).ConfigureAwait(false);

            await MainembedMsg.CreateReactionAsync(CancelEmoji).ConfigureAwait(false);

            var result = await interactivity.WaitForReactionAsync(x => x.Message == MainembedMsg && x.User == ctx.User && (x.Emoji == CreateNewChannelEmoji || x.Emoji == CancelEmoji || x.Emoji == EditChannelEmoji)).ConfigureAwait(false);
            await MainembedMsg.DeleteAsync();
            if(result.Result == null || result.Result.Emoji == CancelEmoji)
            {
                var Cancelembed = new DiscordEmbedBuilder
                {
                    Title = "Setup cancled",
                    Description = $"",
                    Color = DiscordColor.Red
                };
                Cancelembed.WithFooter("Idk what i write here currently ._.");

                await ctx.Channel.SendMessageAsync(embed: Cancelembed).ConfigureAwait(false);
                return;
            }
            else if (result.Result.Emoji == CreateNewChannelEmoji)
            {
                var embedBuilder = new DiscordEmbedBuilder
                {
                    Title = $"Please Respond Below",
                    Description = $"Please write the Channel name below",
                    Color = DiscordColor.DarkGray
                };
                embedBuilder.WithFooter("To stop the setup use the !cancel command");



                var embed = await ctx.Channel.SendMessageAsync(embed: embedBuilder).ConfigureAwait(false);

                var messageResult = await interactivity.WaitForMessageAsync(x => x.ChannelId == ctx.Channel.Id && x.Author.Id == ctx.User.Id && x.Channel.Guild.Id == ctx.Guild.Id).ConfigureAwait(false);
                if (messageResult.Result == null)
                {
                    await ctx.Channel.SendMessageAsync("Setup cancled after 60 seconds").ConfigureAwait(false);
                    return;
                }
                if (messageResult.Result.Content.Equals("!cancel", StringComparison.OrdinalIgnoreCase))
                {
                    await ctx.Channel.SendMessageAsync("Setup cancled").ConfigureAwait(false);
                    return;
                }
                String chresult = messageResult.Result.Content;
                var chn = await ctx.Client.GetChannelAsync(Convert.ToUInt64(chresult));
                if(chn == null)
                {
                    await ctx.Channel.SendMessageAsync("Channel cannot be found.").ConfigureAwait(false);
                }
                BotChannels.Add(ctx.Guild.Id, chn.Id);
                await ctx.Channel.SendMessageAsync("Channel created.").ConfigureAwait(false);
                return;
            } 
            else if (result.Result.Emoji == EditChannelEmoji)
            {
                var chnid = BotChannels[ctx.Guild.Id];
                var chn = await ctx.Client.GetChannelAsync(chnid);
                var embedBuilder = new DiscordEmbedBuilder
                {
                    Title = $"Do you want really remove the Channel?",
                    Description = $"",
                    Color = DiscordColor.Red
                };
                embedBuilder.WithFooter("To stop the setup use the !cancel command");
                embedBuilder.AddField("Name", chn.Name);
                embedBuilder.AddField("Id", chn.Id.ToString());
                var embedmsg = await ctx.Channel.SendMessageAsync(embed: embedBuilder).ConfigureAwait(false);
                await embedmsg.CreateReactionAsync(AcceptEmoji);
                await embedmsg.CreateReactionAsync(CancelEmoji);

                var removereaction = await interactivity.WaitForReactionAsync(x => x.Message == embedmsg && x.User == ctx.User && (x.Emoji == AcceptEmoji || x.Emoji == CancelEmoji)).ConfigureAwait(false);
                await embedmsg.DeleteAsync();
                if (removereaction.Result == null || removereaction.Result.Emoji == CancelEmoji)
                {
                    var Cancelembed = new DiscordEmbedBuilder
                    {
                        Title = "Setup cancled",
                        Description = $"",
                        Color = DiscordColor.Red
                    };
                    Cancelembed.WithFooter("Idk what i write here currently ._.");

                    await ctx.Channel.SendMessageAsync(embed: Cancelembed).ConfigureAwait(false);
                    return;
                }
                else if (removereaction.Result.Emoji == AcceptEmoji)
                {
                    try
                    {
                        if (BotChannels.ContainsKey(ctx.Guild.Id))
                        {
                            BotChannels.Remove(ctx.Guild.Id);
                            await ctx.Channel.SendMessageAsync("Channel removed from Bot.").ConfigureAwait(false);
                        } else
                        {
                            await ctx.Channel.SendMessageAsync("Something went wrong!.").ConfigureAwait(false);
                        }
                    }
                    catch
                    {

                    }
                }
                    return;
            }
            else
              return;
           

        }*/

        //Reworked Setup
        [Command("setup"), Description("3Setup the text channel.")]
        [RequireBotPermissions(Permissions.ManageChannels)]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task SetupAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
             try
            {
                if (CheckHasCooldown(ctx))
                {
                    SendCooldownAsync(ctx);
                    return;
                }
                if (CheckHasPermission(ctx, role.admin))
                    return;
                DiscordChannel checkchn = null;
                var interactivity = ctx.Client.GetInteractivity();
                try
                {
                    checkchn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
                }
                catch { }

                if (checkchn is null)
                {   
                    if(BotChannels.ContainsKey(ctx.Guild.Id))
                        BotChannels.Remove(ctx.Guild.Id);
                }
                if (BotChannels.ContainsKey(ctx.Guild.Id))
                {
                    var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
                    var MessagesRestored = new DiscordEmbedBuilder
                    {
                        Title = $"Botchannel messages restored!",
                        Color = DiscordColor.DarkGreen
                    };
                    MessagesRestored.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
                    MessagesRestored.AddField("Channel:", $"{chn.Mention}");


                    if (!BotChannelMainMessages.ContainsKey(ctx.Guild.Id) || !BotChannelBannerMessages.ContainsKey(ctx.Guild.Id))
                    {
                        if (VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                        {
                            await LeaveAsync(ctx);
                        }
                        await Task.Factory.StartNew(() => BotChannel(ctx, ctx.Guild.Id));

                        await ctx.Channel.SendMessageAsync(embed: MessagesRestored);
                        return;
                    }

                    try
                    {
                        var MainMsg = chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]).Result;
                        var BannerMsg = chn.GetMessageAsync(BotChannelBannerMessages[ctx.Guild.Id]).Result;
                    }
                    catch
                    {
                        if (VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection1))
                        {
                            await LeaveAsync(ctx);
                        }
                        await chn.DeleteAsync();
                        BotChannels.Remove(ctx.Guild.Id);
                        BotChannelMainMessages.Remove(ctx.Guild.Id);
                        BotChannelBannerMessages.Remove(ctx.Guild.Id);
                        if(TrackLoadPlaylists.ContainsKey(ctx.Guild.Id))
                            TrackLoadPlaylists[ctx.Guild.Id].Clear();
                        var newchn = await CreateBotChannelAsync(ctx, true);
                        var MessagesRestoredNewChn = new DiscordEmbedBuilder
                        {
                            Title = $"Botchannel messages restored!",
                            Color = DiscordColor.DarkGreen
                        };
                        MessagesRestoredNewChn.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
                        MessagesRestoredNewChn.AddField("Channel:", $"{newchn.Mention}");
                        await ctx.Channel.SendMessageAsync(embed: MessagesRestoredNewChn);
                        return;
                    }

                    var SetupAlreadyDone = new DiscordEmbedBuilder
                    {
                        Title = $"Setup already done!",
                        Description = $"If there a error then delete the Channel and repeat the Command",
                        Color = DiscordColor.DarkGreen
                    };
                    SetupAlreadyDone.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
                    SetupAlreadyDone.AddField("Channel:", $"{chn.Mention}");
                    await ctx.Channel.SendMessageAsync(embed: SetupAlreadyDone);
                    return;
                }
                if (VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection2))
                {
                    await LeaveAsync(ctx);
                }
                await CreateBotChannelAsync(ctx);

            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async Task<DiscordChannel> CreateBotChannelAsync(CommandContext ctx, Boolean Restore = false)
        {
            Task<DiscordChannel> newchnresult = null;
            try
            {
                newchnresult = ctx.Guild.CreateChannelAsync("crow-song-requests", ChannelType.Text, topic: @"
                :play_pause: Pause/Resume the song.
                :stop_button: Stop and empty the queue.
                :track_next: Skip the song.
                :arrows_counterclockwise: Switch between the loop modes.
                :twisted_rightwards_arrows: Shuffle the queue.
                :star: Add the current song to your private playlist.
                :x: Remove the current song from your private playlist.");
            }
            catch (UnauthorizedException)
            {
                var SetupFailed = new DiscordEmbedBuilder
                {
                    Title = $"Unauthorized access!",
                    Description = $"Cannot create Channel!",
                    Color = DiscordColor.Red
                };
                SetupFailed.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
                await ctx.Channel.SendMessageAsync(embed: SetupFailed);
                return null;
            }
            
            if (newchnresult.Result == null)
            {
                var SetupFailed = new DiscordEmbedBuilder
                {
                    Title = $"Something went wrong!",
                    Color = DiscordColor.Red
                };
                SetupFailed.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
                await ctx.Channel.SendMessageAsync(embed: SetupFailed);
                return null;
            }
            else
            {
                try
                {
                    if(BotChannels.ContainsKey(ctx.Guild.Id))
                        await ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]).DeleteAsync();
                }
                catch {}
                if (BotChannels.ContainsKey(ctx.Guild.Id))
                    BotChannels.Remove(ctx.Guild.Id);
                if (BotChannelMainMessages.ContainsKey(ctx.Guild.Id))
                    BotChannelMainMessages.Remove(ctx.Guild.Id);
                if (BotChannelBannerMessages.ContainsKey(ctx.Guild.Id))
                    BotChannelBannerMessages.Remove(ctx.Guild.Id);
                var newchn = newchnresult.Result;
                var SetupDone = new DiscordEmbedBuilder
                {
                    Title = $"Setup done!",
                    Color = DiscordColor.DarkGreen
                };
                BotChannels.Add(ctx.Guild.Id, newchn.Id);
                SetupDone.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
                SetupDone.AddField("Channel:", $"{newchn.Mention} (unnameable)");
                if(Restore != true)
                    await ctx.Channel.SendMessageAsync(embed: SetupDone);
                await Task.Factory.StartNew(() => BotChannel(ctx, ctx.Guild.Id));
                return newchn;
            }
        }

        private async Task<Boolean> CheckIsBotChannelAndMessagesExits(CommandContext ctx, ulong GuildId = 0)
        {
            try
            {
                DiscordEmbedBuilder BotChannelsOrMessagesAreMissing = new DiscordEmbedBuilder
                {
                    Title = "An Error ocurred",
                    Description = "Please run the Setup again!",
                    Color = DiscordColor.Red

                };
                if (GuildId == 0)
                    GuildId = ctx.Guild.Id;
                if (!BotChannels.ContainsKey(GuildId) || !BotChannelMainMessages.ContainsKey(GuildId) || !BotChannelBannerMessages.ContainsKey(GuildId))
                {
                    if (!DeletePool.ContainsKey(ctx.Message.Id))
                        DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
                    await ctx.Channel.SendMessageAsync(embed: BotChannelsOrMessagesAreMissing);
                    return true;
                }
                var guild = ctx.Client.GetGuildAsync(GuildId);
                if(guild.Result == null)
                {
                    if (!DeletePool.ContainsKey(ctx.Message.Id))
                        DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
                    await ctx.Channel.SendMessageAsync(embed: BotChannelsOrMessagesAreMissing);
                    return true;
                }
                var chn = guild.Result.GetChannel(BotChannels[GuildId]);
                if (chn == null)
                {
                    if (!DeletePool.ContainsKey(ctx.Message.Id))
                        DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
                    await ctx.Channel.SendMessageAsync(embed: BotChannelsOrMessagesAreMissing);
                    return true;
                }
                try
                {
                    var MainMsg = chn.GetMessageAsync(BotChannelMainMessages[GuildId]).Result;
                    var BannerMsg = chn.GetMessageAsync(BotChannelBannerMessages[GuildId]).Result;
                }
                catch
                {
                    var messages = chn.GetMessagesAsync(5);
                    foreach (DiscordMessage msg in messages.Result)
                    {
                        DeletePool.Add(msg.Id, new DeleteMessage(chn, msg));

                    }
                    BotChannelMainMessages.Remove(GuildId);
                    BotChannelBannerMessages.Remove(GuildId);
                    if (!DeletePool.ContainsKey(ctx.Message.Id))
                        DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
                    await ctx.Channel.SendMessageAsync(embed: BotChannelsOrMessagesAreMissing);
                    return true;
                }

                return false;
            }
            catch(Exception e)
            {
                ctx.Client.Logger.LogError(new EventId(7778, "CheckIsBotChannel"), e.ToString());
                return true;
            }
           
        }
        [RequireBotPermissions(Permissions.ManageChannels | Permissions.AccessChannels),RequirePermissions(Permissions.AccessChannels)]
        [Command("join"), Description("1Joins a voice channel."), Aliases("connect")]
        public async Task JoinAsync(CommandContext ctx, DiscordChannel Channelname = null, ulong GuildId = 0)
        {

            if (GuildId == 0)
            {
                GuildId = ctx.Guild.Id;
                if (!DeletePool.ContainsKey(ctx.Message.Id))
                    DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

                if (CheckHasCooldown(ctx, GuildId))
                {
                    
                    if (ctx.Command.Name == "join" || ctx.Command.Name == "connect")
                    {
                        
                        SendCooldownAsync(ctx);
                        return;
                    }

                }
                if (CheckHasPermission(ctx, role.everyone))
                    return;
                if (BotChannels[GuildId] != ctx.Channel.Id)
                {
                    SendRestrictedChannelAsync(ctx);
                    return;
                }
            }

            


            LavalinkGuildConnection VoiceConnection;
            var vc = Channelname ?? ctx.Member.VoiceState?.Channel;
            if (vc == null)
            {
                SendNotInAVoiceChannelAsync(ctx);
                return;
            }
           

            if (!BotChannels.ContainsKey(GuildId))
            {
                SendNeedSetupAsync(ctx);
                return;
            }
            

            if (VoiceConnections.TryGetValue(GuildId, out VoiceConnection))
            {
                if (vc != VoiceConnection.Channel)
                {
                    var chn1 = ctx.Guild.GetChannel(BotChannels[GuildId]);
                    var MainMsg = await chn1.GetMessageAsync(BotChannelMainMessages[GuildId]);
                    await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {ctx.Prefix}"); // PREFIX
                    await VoiceConnection.DisconnectAsync();
                    VoiceConnections.Remove(GuildId);
                    Volumes.Remove(VoiceConnection);
                    int timeout = 1000;
                    var task = this.Lavalink.ConnectAsync(vc);
                    if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                    {
                        VoiceConnections.Add(GuildId, task.Result);
                        VoiceConnection = task.Result;
                    }
                    else
                    {
                        var SendCannotJoinEmbed = new DiscordEmbedBuilder
                        {
                            Description = $"The bot cannot join this Channel!",
                            Color = DiscordColor.Red
                        };
                        DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendCannotJoinEmbed);
                        DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                        return;
                    }

                    Volumes.Add(VoiceConnection, configJson.DefaultVolume);
                }
            } 
            else
            {
                int timeout = 1000;
                var task = this.Lavalink.ConnectAsync(vc);
                if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                {
                    VoiceConnections.Add(GuildId, task.Result);
                    VoiceConnection = task.Result;
                }
                else
                {
                    var SendCannotJoinEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"The bot cannot join this Channel!",
                        Color = DiscordColor.Red
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendCannotJoinEmbed);
                    DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                    return;
                }
                
                
    
                if (!Volumes.ContainsKey(VoiceConnection))
                    Volumes.Add(VoiceConnection, configJson.DefaultVolume);
                else
                    Volumes[VoiceConnection] = configJson.DefaultVolume;
              
                if (!AFKTimeOffsets.ContainsKey(GuildId))
                    AFKTimeOffsets.Add(GuildId, new DateTimeOffset());
                else
                    AFKTimeOffsets[GuildId] = new DateTimeOffset();
                if (!CheckAFKStates.ContainsKey(GuildId))
                    CheckAFKStates.Add(GuildId, true);
                if (!AnnounceStates.ContainsKey(GuildId))
                    AnnounceStates.Add(GuildId, true);
                if(!IsPausedStates.ContainsKey(GuildId))
                    IsPausedStates.Add(GuildId, false);
                if (!TrackLoadPlaylists.ContainsKey(GuildId))
                    TrackLoadPlaylists.Add(GuildId, new List<LavalinkTrack>());
                if (!CurrentSong.ContainsKey(GuildId))
                    CurrentSong.Add(GuildId, 0);
                if (!FavoritesTracksLists.ContainsKey(GuildId))
                    FavoritesTracksLists.Add(GuildId, new List<LavalinkTrack>());
                if (!Loopmodes.ContainsKey(GuildId))
                    Loopmodes.Add(GuildId, loopmode.off);
                await VoiceConnection.SetVolumeAsync(configJson.DefaultVolume);
                
            }
            




        }


        [RequireBotPermissions(Permissions.ManageChannels)]
        [Command("leave"), Description("1Leaves a voice channel."), Aliases("dc", "disconnect")]
        public async Task LeaveAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (!BotChannels.ContainsKey(ctx.Guild.Id))
            {
                SendNeedSetupAsync(ctx);
                return;
            }
            LavalinkGuildConnection VoiceConnection;
            VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);
            if (!(VoiceConnection == null))
            {

                if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
                {
                    SendNotInSameChannelAsync(ctx);
                    return;
                }
                var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
                var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
                await VoiceConnection.DisconnectAsync().ConfigureAwait(false);
                VoiceConnections.Remove(ctx.Guild.Id);
                Volumes.Remove(VoiceConnection);
                AFKTimeOffsets.Remove(ctx.Guild.Id);
                IsPausedStates.Remove(ctx.Guild.Id);
                TrackLoadPlaylists[ctx.Guild.Id].Clear();
                Loopmodes.Remove(ctx.Guild.Id);
                await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {ctx.Prefix}"); // PREFIX

            }
            else
                SendNotConnectedAsync(ctx);
        }

     
        //Queue with uri

        [Command("play"), Description("1Add a song to the queue with a URL.|Add a song to the queue with a Keywords."), Aliases("p")]
        public async Task QueueAsync(CommandContext ctx, [Description("Youtube, Soundcloud, Twitch, Vimeo Links.\nTwitch Livestream support.")] Uri Url) {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (Url.ToString() == String.Empty)
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            await JoinAsync(ctx, ctx.Member.VoiceState?.Channel);
            if (await CheckIsBotChannelAndMessagesExits(ctx))
                return;

            //if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
           // {
           //     SendNotInSameChannelAsync(ctx);
          //      return;
           // }
            LavalinkLoadResult trackLoad;
            var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            //Show Playlist
            /*       if (Url == null)
            {
                if (TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() != 0)
                {
                    int i = 0;
                    var desc = "";
                    foreach (var item in TrackLoadPlaylists[ctx.Guild.Id].ToList())
                    {
                        i++;
                        if (CurrentSong[ctx.Guild.Id] + 1 == i)
                            desc += "\n• " + $"{i}. `{item.Title}` von `{item.Author}` - `Currently Playing`";
                        else
                            desc += "\n• " + $"{i}. `{item.Title}` von `{item.Author}`";
                    }
                    var listembed = new DiscordEmbedBuilder
                    {
                        Title = "Playlist",
                        Description = desc,
                        Color = DiscordColor.Orange
                    };

                    var listmsg = await chn.SendMessageAsync(embed: listembed).ConfigureAwait(false);
                    DeletePool.Add(listmsg.Id, new DeleteMessage(chn, listmsg));
                    return;
                }
                else
                {
                    await ctx.RespondAsync("Queue ist leer.").ConfigureAwait(false);
                    return;
                }
            }

            //End Show Playlist
            /*
            var vc = ctx.Member.VoiceState?.Channel;
            if (vc != null)
            {

                if (ctx.Member.VoiceState?.Channel != VoiceConnection?.Channel)
                {
                    if (VoiceConnection?.Channel != null)
                        await VoiceConnection?.DisconnectAsync();

                    VoiceConnection = await this.Lavalink.ConnectAsync(vc);
                    VoiceConnection.PlaybackFinished += VoiceConnection_PlaybackFinished;
                }

            }*/

            var notfoundembed = new DiscordEmbedBuilder
            {
                Title = "Song was not found!",
                Color = DiscordColor.Red
            };
            notfoundembed.AddField("Link:", $"`{Url}`");
            notfoundembed.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
            LavalinkGuildConnection VoiceConnection;
            VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

            if (Url.ToString().Contains("soundcloud") == true)
            {
                trackLoad = await this.Lavalink.Rest.GetTracksAsync(Url.ToString(), LavalinkSearchType.SoundCloud);
                if (trackLoad.Tracks.Count() == 0)
                {
                    var nfmsg = await ctx.RespondAsync(embed: notfoundembed).ConfigureAwait(false);
                    try
                    {
                        DeletePool.Add(nfmsg.Id, new DeleteMessage(ctx.Channel, nfmsg));
                    }
                    catch { }
                    return;
                }
                TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.First());
            }
            else
            {
                trackLoad = await this.Lavalink.Rest.GetTracksAsync(Url);
                if (trackLoad.Tracks.Count() == 0)
                {
                    var nfmsg = await ctx.RespondAsync(embed: notfoundembed).ConfigureAwait(false);
                    
                    try
                    {
                        DeletePool.Add(nfmsg.Id, new DeleteMessage(ctx.Channel, nfmsg));
                    }
                    catch { }
                    return;
                }
                TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.First());
            }

            if (VoiceConnection != null)
            {
                if (TrackLoadPlaylists[ctx.Guild.Id].Count == 1)
                {
                    var track = trackLoad.Tracks.First();
                    await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");
                    
                    await VoiceConnection.PlayAsync(TrackLoadPlaylists[ctx.Guild.Id].First());
                    /*
                    if (AnnounceStates[ctx.Guild.Id] == true)
                    {
                        var announceembed = new DiscordEmbedBuilder
                        {
                            Title = $"Spielt jetzt: \n`{TrackLoadPlaylists[ctx.Guild.Id].First().Title}` von `{TrackLoadPlaylists[ctx.Guild.Id].First().Author}`.",
                            Color = DiscordColor.DarkGreen
                        };
                        var announcemsg = await ctx.Channel.SendMessageAsync(embed: announceembed).ConfigureAwait(false);
                    }*/
                }
                else
                {
                    await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, trackLoad.Tracks.First())}");

                        var addtolistembed = new DiscordEmbedBuilder
                        {
                            Title = $"`{trackLoad.Tracks.First().Title}` by `{trackLoad.Tracks.First().Author}` added to the queue. `!skip`",
                            Color = DiscordColor.DarkGreen
                        };
                        var announcemsg = await ctx.Channel.SendMessageAsync(embed: addtolistembed).ConfigureAwait(false);
                    
                        DeletePool.Add(announcemsg.Id, new DeleteMessage(ctx.Channel, announcemsg));

                    return;
                }
                    
            }

        }

        /*      //Queue with uri
              [Command("queue"), Description("Fügt ein Lied zur Warteschlange hinzu."), Aliases("q")]
              public async Task QueueAsync(CommandContext ctx, [Description("Youtube, Soundcloud, Twitch, Vimeo Links.\nTwitch Livestream support."), RemainingText] Uri uri)
              {


                  LavalinkLoadResult trackLoad;

                  //Show Playlist
                  if (uri == null)
                  {
                      if (TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() != 0)
                      {
                          int i = 0;
                          var desc = "";
                          foreach (var item in TrackLoadPlaylists[ctx.Guild.Id].ToList())
                          {
                              i++;
                              if (CurrentSong[ctx.Guild.Id] + 1 == i)
                                  desc += "\n• " + $"{i}. `{item.Title}` von `{item.Author}` - `Currently Playing`";
                              else
                                  desc += "\n• " + $"{i}. `{item.Title}` von `{item.Author}`";
                          }
                          var listembed = new DiscordEmbedBuilder
                          {
                              Title = "Playlist",
                              Description = desc,
                              Color = DiscordColor.Orange
                          };

                          var listmsg = await ctx.Channel.SendMessageAsync(embed: listembed).ConfigureAwait(false);
                          return;
                      }
                      else
                      {
                          await ctx.RespondAsync("Queue ist leer.").ConfigureAwait(false);
                          return;
                      }
                  }

                  //End Show Playlist
                  /*
                  var vc = ctx.Member.VoiceState?.Channel;
                  if (vc != null)
                  {

                      if (ctx.Member.VoiceState?.Channel != VoiceConnection?.Channel)
                      {
                          if (VoiceConnection?.Channel != null)
                              await VoiceConnection?.DisconnectAsync();

                          VoiceConnection = await this.Lavalink.ConnectAsync(vc);
                          VoiceConnection.PlaybackFinished += VoiceConnection_PlaybackFinished;
                      }

                  }

                  var notfoundembed = new DiscordEmbedBuilder
                  {
                      Title = $"`{uri}` wurde nicht gefunden.",
                      Color = DiscordColor.Red
                  };


                  if (uri.ToString().Contains("soundcloud") == true)
                  {
                      trackLoad = await this.Lavalink.Rest.GetTracksAsync(uri.ToString(), LavalinkSearchType.SoundCloud);
                      if (trackLoad.Tracks.Count() == 0)
                      {
                          await ctx.RespondAsync(embed: notfoundembed).ConfigureAwait(false);
                          return;
                      }
                      TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.First());
                  }
                  else
                  {
                      trackLoad = await this.Lavalink.Rest.GetTracksAsync(uri);
                      if (trackLoad.Tracks.Count() == 0)
                      {
                          await ctx.RespondAsync(embed: notfoundembed).ConfigureAwait(false);
                          return;
                      }
                      TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.First());
                  }
                  LavalinkGuildConnection VoiceConnection;
                  VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

                  if (VoiceConnection?.Channel == null)
                      await JoinAsync(ctx, ctx.Member.VoiceState?.Channel);

                  if (VoiceConnection != null)
                  {
                      if (TrackLoadPlaylists[ctx.Guild.Id].Count == 1)
                      {
                          await VoiceConnection.PlayAsync(TrackLoadPlaylists[ctx.Guild.Id].First());
                          if (AnnounceStates[ctx.Guild.Id] == true)
                          {
                              var announceembed = new DiscordEmbedBuilder
                              {
                                  Title = $"Spielt jetzt: \n`{TrackLoadPlaylists[ctx.Guild.Id].First().Title}` von `{TrackLoadPlaylists[ctx.Guild.Id].First().Author}`.",
                                  Color = DiscordColor.DarkGreen
                              };
                              var announcemsg = await ctx.Channel.SendMessageAsync(embed: announceembed).ConfigureAwait(false);
                          }
                      }
                      else
                      {
                          if (AnnounceStates[ctx.Guild.Id] == true)
                          {
                              var addtolistembed = new DiscordEmbedBuilder
                              {
                                  Title = $"`{trackLoad.Tracks.First().Title}` von `{trackLoad.Tracks.First().Author}` zur Warteschlange hinzugefügt. `!skip`",
                                  Color = DiscordColor.DarkGreen
                              };
                              var announcemsg = await ctx.Channel.SendMessageAsync(embed: addtolistembed).ConfigureAwait(false);
                          }
                          return;
                      }

                  }

              }
      */
        //Queue with keywords
        [Command("play")]
        public async Task QueueAsync(CommandContext ctx, [Description("Keywords"), RemainingText] String Keywords)
        {
            try
            {

                if (!DeletePool.ContainsKey(ctx.Message.Id))
                    DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
                
                if (Keywords == String.Empty)
                    return;
                
                if (CheckHasCooldown(ctx))
                {
                    SendCooldownAsync(ctx);
                    return;
                }
                
                if (CheckHasPermission(ctx, role.everyone))
                    return;      
                if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
                {
                    SendRestrictedChannelAsync(ctx);
                    return;
                }
                await JoinAsync(ctx, ctx.Member.VoiceState?.Channel);
                if (await CheckIsBotChannelAndMessagesExits(ctx))
                    return;
                
                //   if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
                //  {
                //      SendNotInSameChannelAsync(ctx);
                //        return;
                //   }   
                var notfoundembed = new DiscordEmbedBuilder
                {
                    Title = "Song was not found!",
                    Color = DiscordColor.Red
                };
                notfoundembed.AddField("Keywords:", $"`{Keywords}`");
                notfoundembed.WithFooter($"Prefix for this Server is: {ctx.Prefix}");

                
                LavalinkGuildConnection VoiceConnection;
                VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);
                LavalinkLoadResult trackLoad;
                trackLoad = await this.Lavalink.Rest.GetTracksAsync(Keywords);
                if (trackLoad.Tracks.Count() == 0)
                {
                    var nfmsg = await ctx.RespondAsync(embed: notfoundembed).ConfigureAwait(false);
                    
                    try
                    {
                        DeletePool.Add(nfmsg.Id, new DeleteMessage(ctx.Channel, nfmsg));
                    }
                    catch { }
                    
                    return;
                }
                
                TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.First());
                
                if (VoiceConnection != null)
                {

                    var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
                    var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
                    if (TrackLoadPlaylists[ctx.Guild.Id].Count == 1)
                    {
                        var track = trackLoad.Tracks.First();
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, TrackLoadPlaylists[ctx.Guild.Id].First())}");
                        await VoiceConnection.PlayAsync(TrackLoadPlaylists[ctx.Guild.Id].First());
                        
                    }

                    else
                    {
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, TrackLoadPlaylists[ctx.Guild.Id].First())}");
                        
                        var addtolistembed = new DiscordEmbedBuilder
                        {
                            Title = $"`{trackLoad.Tracks.First().Title}` by `{trackLoad.Tracks.First().Author}` added to the queue. `!skip`",
                            Color = DiscordColor.DarkGreen
                        };
                        var announcemsg = await ctx.Channel.SendMessageAsync(embed: addtolistembed).ConfigureAwait(false);
                        
                        try
                        {
                            DeletePool.Add(announcemsg.Id, new DeleteMessage(ctx.Channel, announcemsg));
                        }
                        catch { }

                        return;
                    }

                }
            }
            catch (Exception e)
            {
                ctx.Client.Logger.LogError(new EventId(7780, "play"), e.Message);
                
            }

        }
        /*
        [Command("queuelist"), Aliases("ql")]
        public async Task QueueplaylistfuncAsync(CommandContext ctx)
        {
            if (this.Lavalink == null)
            {
                GetNodeConnection(ctx);
                if (this.Lavalink == null)
                    return;
            }

            if (ctx.Guild.Id == 0)
            {


                var errembed = new DiscordEmbedBuilder
                {
                    Title = "Error",
                    Description = $"An Error has occurred!",
                    Color = DiscordColor.Red
                };

                await ctx.Channel.SendMessageAsync(embed: errembed);
                return;
            }


            string dir = @"C:\Users\Marvin\source\repos\LemixDiscordMusikBot\LemixDiscordMusikBot\bin\Debug\netcoreapp3.1\Playlists";
            if (!Directory.Exists(dir + "\\" + ctx.Guild.Id))
            {
                Directory.CreateDirectory(dir + "\\" + ctx.Guild.Id);
            }
            dir = dir + "\\" + ctx.Guild.Id;

                String[] Files = Directory.GetFiles(dir);
                if (Files == null)
                    return;

                var desc = "Following Playlists were found: ";
                var emptydesc = $"No playlists were found! \n Use {ctx.Prefix}{ctx.Command.Name} save [Name].";

            foreach (var item in Files)
                {
                    desc += "\n• `" + Path.GetFileNameWithoutExtension(item) + "`";
                }

            if (Files.Length == 0)
                desc = emptydesc;

                var listembed = new DiscordEmbedBuilder
                {
                    Title = "Playlists",
                    Description = desc,
                    Color = DiscordColor.Orange
                };

                var listmsg = await ctx.Channel.SendMessageAsync(embed: listembed).ConfigureAwait(false);
                return;
            
        }
        [Command("queuelist")]
        public async Task QueueplaylistfuncAsync(CommandContext ctx, [Description("Use `load`, `info` or `save`.")]String Mode, [Description("Playlist name.\nIf empty then a list of saved playlists is displayed!")][RemainingText]String Name)
            {

                if (this.Lavalink == null)
                {
                    GetNodeConnection(ctx);
                    if (this.Lavalink == null)
                        return;
                }

                if (ctx.Guild.Id == 0)
                {


                    var errembed = new DiscordEmbedBuilder
                    {
                        Title = "Error",
                        Description = $"An Error has occurred!",
                        Color = DiscordColor.Red
                    };

                    await ctx.Channel.SendMessageAsync(embed: errembed);
                    return;
                }
                

                string dir = @"C:\Users\Marvin\source\repos\LemixDiscordMusikBot\LemixDiscordMusikBot\bin\Debug\netcoreapp3.1\Playlists";
                if (!Directory.Exists(dir+"\\"+ctx.Guild.Id))
                {
                    Directory.CreateDirectory(dir + "\\" + ctx.Guild.Id);
                }
                dir = dir + "\\" + ctx.Guild.Id;

                if (Name == String.Empty)
                {

                    var embed = new DiscordEmbedBuilder
                    {
                        Title = "Name cannot be empty!",
                        Description = $"Use {ctx.Prefix} help {ctx.Command.Name} to get more Informations.",
                        Color = DiscordColor.Red
                    };

                    var msg = await ctx.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
                    // await Task.Delay(5000);
                    //  await msg.DeleteAsync();
                    return;
                }

                var regx = new Regex("[^a-zA-Z0-9_.]");
                if (regx.IsMatch(Name))
                {
                    var embed = new DiscordEmbedBuilder
                    {
                        Title = "Name not allowed!",
                        Description = $"Use {ctx.Prefix}help {ctx.Command.Name} to get more Informations.",
                        Color = DiscordColor.Red
                    };

                    var msg = await ctx.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
                    return;
                }
                LavalinkGuildConnection VoiceConnection;
                VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);
                Name = Name.ToLower();
                string serializationFile = Path.Combine(dir, $"{Name}.playlist");
                

                
                if (Mode.Equals("load", StringComparison.OrdinalIgnoreCase))
                {

                    if (VoiceConnection != null)
                        await VoiceConnection.PauseAsync().ConfigureAwait(false);

                

                    TextReader reader = null;
                    var play = false;
                    List<LavalinkTrack> temptracklist = null;
                    try
                    {
                        try
                        {
                            reader = new StreamReader(serializationFile);
                        }
                        catch (Exception)
                        {
                            var dembed = new DiscordEmbedBuilder
                            {
                                Title = "Playlist not found!",
                                Description = $"Use {ctx.Prefix}help {ctx.Command.Name} to get more Informations.",
                                Color = DiscordColor.Red
                            };

                            var dmsg = await ctx.Channel.SendMessageAsync(embed: dembed).ConfigureAwait(false);
                            return;
                        }
                        var fileContents = reader.ReadToEnd();
                        if (TrackLoadPlaylists[ctx.Guild.Id].Count == 0)
                            play = true;
                        temptracklist = JsonConvert.DeserializeObject<List<LavalinkTrack>>(fileContents);

                    }
                    finally
                    {
                        if (reader != null)
                            reader.Close();
                    }

                    if (reader == null)
                    {
                        var pembed = new DiscordEmbedBuilder
                        {
                            Title = "Playlist not found!",
                            Description = $"Use {ctx.Prefix}help {ctx.Command.Name} to get more Informations.",
                            Color = DiscordColor.Red
                        };

                        var pmsg = await ctx.Channel.SendMessageAsync(embed: pembed).ConfigureAwait(false);
                        return;
                    }

                    LavalinkLoadResult track = null;
                    List<LavalinkLoadResult> errtrack = new List<LavalinkLoadResult>();
                TrackLoadPlaylists[ctx.Guild.Id].Clear();
                    foreach (LavalinkTrack i in temptracklist)
                    {
                        track = await this.Lavalink.Rest.GetTracksAsync(i.Uri);
                        if (track.Tracks.Count() != 0)
                        TrackLoadPlaylists[ctx.Guild.Id].Add(track.Tracks.First());
                        else
                        {
                            errtrack.Add(track);
                        }
                    }
                    if (errtrack.Count() > 0)
                    {
                        String errdes = String.Empty;
                        foreach (LavalinkLoadResult i in errtrack)
                        {
                            errdes += "`" + i.Tracks.First().Title + "` von `" + i.Tracks.First().Author + "`\n";
                        }

                        var errtracks = new DiscordEmbedBuilder
                        {
                            Title = "Following Tracks cannot be loaded",
                            Description = errdes,
                            Color = DiscordColor.DarkGreen
                        };

                        var errtracksmsg = await ctx.Channel.SendMessageAsync(embed: errtracks).ConfigureAwait(false);

                    }



                    var embed = new DiscordEmbedBuilder
                    {
                        Title = "Succesfully loaded!",
                        Color = DiscordColor.DarkGreen
                    };
                CurrentSong[ctx.Guild.Id] = 0;
                    var msg = await ctx.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
                    if (play)
                    {
                        await JoinAsync(ctx, ctx.Member.VoiceState?.Channel);

                        await VoiceConnection.PlayAsync(TrackLoadPlaylists[ctx.Guild.Id].First());
                        if (AnnounceStates[ctx.Guild.Id] == true)
                        {
                            var announceembed = new DiscordEmbedBuilder
                            {
                                Title = $"Spielt jetzt: \n`{TrackLoadPlaylists[ctx.Guild.Id].First().Title}` von `{TrackLoadPlaylists[ctx.Guild.Id].First().Author}`.",
                                Color = DiscordColor.DarkGreen
                            };
                            var announcemsg = await ctx.Channel.SendMessageAsync(embed: announceembed).ConfigureAwait(false);
                        }
                    }


                }
                else if (Mode.Equals("save", StringComparison.OrdinalIgnoreCase))
                {
                    TextWriter writer = null;
                    try
                    {
                        var contentsToWriteToFile = JsonConvert.SerializeObject(TrackLoadPlaylists[ctx.Guild.Id]);
                        if (TrackLoadPlaylists[ctx.Guild.Id].Count == 0)
                        {
                            var dembed = new DiscordEmbedBuilder
                            {
                                Title = "Playlist is empty!",
                                Description = $"Use {ctx.Prefix}help {ctx.Command.Name} to get more Informations.",
                                Color = DiscordColor.Red
                            };

                            var dmsg = await ctx.Channel.SendMessageAsync(embed: dembed).ConfigureAwait(false);
                            return;
                        }

                        try
                        {

                            writer = new StreamWriter(serializationFile, false);
                        }
                        catch (Exception)
                        {
                            var dembed = new DiscordEmbedBuilder
                            {
                                Title = "Playlist not found!",
                                Description = $"Use {ctx.Prefix}help {ctx.Command.Name} to get more Informations.",
                                Color = DiscordColor.Red
                            };

                            var dmsg = await ctx.Channel.SendMessageAsync(embed: dembed).ConfigureAwait(false);
                            return;
                        }


                        writer.Write(contentsToWriteToFile);
                    }
                    finally
                    {
                        if (writer != null)
                            writer.Close();
                    }
                    var embed = new DiscordEmbedBuilder
                    {
                        Title = "Succesfully saved!",
                        Color = DiscordColor.DarkGreen
                    };

                    var msg = await ctx.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);


                } 
                else if(Mode.Equals("info", StringComparison.OrdinalIgnoreCase))
                {
                    if (VoiceConnection != null)
                        await VoiceConnection.PauseAsync().ConfigureAwait(false);


                    TextReader reader = null;
                    List<LavalinkTrack> temptracklist = null;
                    try
                    {
                        try
                        {
                            reader = new StreamReader(serializationFile);
                        }
                        catch (Exception)
                        {
                            var dembed = new DiscordEmbedBuilder
                            {
                                Title = "Playlist not found!",
                                Description = $"Use {ctx.Prefix}help {ctx.Command.Name} to get more Informations.",
                                Color = DiscordColor.Red
                            };

                            var dmsg = await ctx.Channel.SendMessageAsync(embed: dembed).ConfigureAwait(false);
                            return;
                        }
                        var fileContents = reader.ReadToEnd();
                        temptracklist = JsonConvert.DeserializeObject<List<LavalinkTrack>>(fileContents);

                    }
                    finally
                    {
                        if (reader != null)
                            reader.Close();
                    }

                    if (reader == null)
                    {
                        var pembed = new DiscordEmbedBuilder
                        {
                            Title = "Playlist not found!",
                            Description = $"Use {ctx.Prefix}help {ctx.Command.Name} to get more Informations.",
                            Color = DiscordColor.Red
                        };

                        var pmsg = await ctx.Channel.SendMessageAsync(embed: pembed).ConfigureAwait(false);
                        return;
                    }

                        String des = string.Empty;
                        foreach (LavalinkTrack i in temptracklist)
                        {
                            des += "`" + i.Title + "` von `" + i.Author + "`\n";
                        }

                        var infotracks = new DiscordEmbedBuilder
                        {
                            Title = "Playlist Info",
                            Description = des,
                            Color = DiscordColor.DarkGreen
                        };

                        await ctx.Channel.SendMessageAsync(embed: infotracks).ConfigureAwait(false);

                }
                else
                {
                    return; // Nothing yet
                }


        }

        [Command("queueplaylist"), Description("Fügt eine Playlist zur Warteschlange hinzu."), Aliases("qp")]
        public async Task QueuePlayListAsync(CommandContext ctx, [Description("Youtube, Soundcloud, Twitch, Vimeo Playlists-Links."), RemainingText] Uri uri)
        {
            LavalinkGuildConnection VoiceConnection;
            VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

            if (VoiceConnection?.Channel == null)
                await JoinAsync(ctx, ctx.Member.VoiceState?.Channel);

            LavalinkLoadResult trackLoad;

            var notfoundembed = new DiscordEmbedBuilder
            {
                Title = $"`{uri}` wurde nicht gefunden.",
                Color = DiscordColor.Red
            };


            if (uri.ToString().Contains("soundcloud") == false)
            {

                trackLoad = await this.Lavalink.Rest.GetTracksAsync(uri).ConfigureAwait(false);

                if (trackLoad.Tracks.Count() == 0)
                {
                    await ctx.RespondAsync(embed: notfoundembed).ConfigureAwait(false);
                    return;
                }

                
                if (TrackLoadPlaylists[ctx.Guild.Id].Count() == 0)
                {
                    await VoiceConnection.PlayAsync(trackLoad.Tracks.First());
                    var announceembed = new DiscordEmbedBuilder
                    {
                        Title = $"Spielt jetzt: \n`{trackLoad.Tracks.First().Title}` von `{trackLoad.Tracks.First().Author}`.",
                        Color = DiscordColor.DarkGreen
                    };
                    var announcemsg = await ctx.Channel.SendMessageAsync(embed: announceembed).ConfigureAwait(false);
                }

                string titleplaylist = string.Empty;
                foreach (LavalinkTrack l in trackLoad.Tracks.ToList())
                {

                    TrackLoadPlaylists[ctx.Guild.Id].Add(l);
                    titleplaylist += $"`{l.Title}` von `{l.Author}` zur Warteschlange hinzugefügt. `!skip`\n";

                }

                await VoiceConnection.PlayAsync(TrackLoadPlaylists[ctx.Guild.Id].First());

                if (AnnounceStates[ctx.Guild.Id] == true)
                {
                    var announceplaylistembed = new DiscordEmbedBuilder
                    {
                        Title = titleplaylist,
                        Color = DiscordColor.DarkGreen
                    };
                    var announceplaylistmsg = await ctx.Channel.SendMessageAsync(embed: announceplaylistembed).ConfigureAwait(false);
                }
                return;

            }
            else
            {

                trackLoad = await this.Lavalink.Rest.GetTracksAsync(uri.ToString(), LavalinkSearchType.SoundCloud);

                if (trackLoad.Tracks.Count() == 0)
                {
                    await ctx.RespondAsync(embed: notfoundembed).ConfigureAwait(false);
                    return;
                }


                if (TrackLoadPlaylists[ctx.Guild.Id].Count() == 0)
                {
                    await VoiceConnection.PlayAsync(trackLoad.Tracks.First());
                    var announceembed = new DiscordEmbedBuilder
                    {
                        Title = $"Spielt jetzt: \n`{trackLoad.Tracks.First().Title}` von `{trackLoad.Tracks.First().Author}`.",
                        Color = DiscordColor.DarkGreen
                    };
                    var announcemsg = await ctx.Channel.SendMessageAsync(embed: announceembed).ConfigureAwait(false);
                }

                string titleplaylist = string.Empty;
                foreach (LavalinkTrack l in trackLoad.Tracks.ToList())
                {

                    TrackLoadPlaylists[ctx.Guild.Id].Add(l);
                    titleplaylist += $"`{l.Title}` von `{l.Author}` zur Warteschlange hinzugefügt. `!skip`\n";

                }

                await VoiceConnection.PlayAsync(TrackLoadPlaylists[ctx.Guild.Id].First());

                if (AnnounceStates[ctx.Guild.Id] == true)
                {
                    var announceplaylistembed = new DiscordEmbedBuilder
                    {
                        Title = titleplaylist,
                        Color = DiscordColor.DarkGreen
                    };
                    var announceplaylistmsg = await ctx.Channel.SendMessageAsync(embed: announceplaylistembed).ConfigureAwait(false);
                }
                return;
            }

        }

        /* //Playlist ID dont work with Lavalink // wip
                [Command]
                public async Task QueuePlayListAsync(CommandContext ctx, [Description("Schlüsselwörter."), RemainingText] String uri)
                {

                    bool ForcePlay = false;

                    await JoinAsync(ctx, ctx.Member.VoiceState?.Channel);

                    var vc = ctx.Member.VoiceState?.Channel;
                    if (vc != null)
                    {

                        if (ctx.Member.VoiceState?.Channel != VoiceConnection?.Channel)
                        {
                            if (VoiceConnection?.Channel != null)
                                await VoiceConnection?.DisconnectAsync();

                            VoiceConnection = await this.Lavalink.ConnectAsync(vc);
                            VoiceConnection.PlaybackFinished += VoiceConnection_PlaybackFinished;
                        }

                    }
                    await JoinAsync(ctx, ctx.Member.VoiceState?.Channel);

                    LavalinkLoadResult trackLoad;
                    this.ContextChannel = ctx.Channel;

                    String responsejson = String.Empty;
                    String keywords = uri.ToString().Replace(" ", "%20");

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"https://www.googleapis.com/youtube/v3/search?part=snippet&order=relevance&q={keywords}&type=playlist&key={configJson.YoutubeAPIKey}");
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    {
                        using (Stream stream = response.GetResponseStream())
                        using (StreamReader reader = new StreamReader(stream))
                            responsejson = await reader.ReadToEndAsync().ConfigureAwait(false);
                        dynamic data = JObject.Parse(responsejson);
                        String pid = data.items[0].id.playlistId;

                        trackLoad = await this.Lavalink.Rest.GetTracksAsync(pid);
                        pt = PlayingType.Play;

                        if (trackLoad.Tracks.Count() == 0)
                        {
                            Console.WriteLine(data);
                            await ctx.RespondAsync($"`{uri}` wurde nicht gefunden."+ pid).ConfigureAwait(false);
                            return;
                        }

                        if (trackLoadplaylist.Count == 0)
                            ForcePlay = true;

                        foreach (LavalinkTrack l in trackLoad.Tracks.ToList())
                        {
                            trackLoadplaylist.Add(l);
                            await ctx.RespondAsync($"`{l.Title}` von `{l.Author}` zur Warteschlange hinzugefügt. `!skip`").ConfigureAwait(false);
                        }
                    }
                    await PlaySongFromPlaylistAsync(ForcePlay, true);


                }
                */

        [Command("next"), Description("2Skips to the next song."), Aliases("n", "skip")]
        public async Task NextAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                if (ctx.Command.Name == "next" || ctx.Command.Name == "n" || ctx.Command.Name == "skip")
                {
                    SendCooldownAsync(ctx);
                    return;
                }
            }
            if (ctx.Command.Name == "next" || ctx.Command.Name == "n" || ctx.Command.Name == "skip")
                if (CheckHasPermission(ctx, role.dj))
                    return;
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (await CheckIsBotChannelAndMessagesExits(ctx))
                return;

            if (TrackLoadPlaylists[ctx.Guild.Id].Count <= 1)
            {
                await StopAsync(ctx);
                return;
            }
            var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            TrackLoadPlaylists[ctx.Guild.Id].RemoveAt(0);
            var track = TrackLoadPlaylists[ctx.Guild.Id].First();
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");
            await VoiceConnection.PauseAsync();
            await VoiceConnection.PlayAsync(track);
        }

        public async Task VoteSkipSkipFunction(CommandContext ctx)
        {
            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (await CheckIsBotChannelAndMessagesExits(ctx))
                return;

            if (TrackLoadPlaylists[ctx.Guild.Id].Count <= 1)
            {
                await StopAsync(ctx);
                return;
            }
            var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            TrackLoadPlaylists[ctx.Guild.Id].RemoveAt(0);
            var track = TrackLoadPlaylists[ctx.Guild.Id].First();
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");
            await VoiceConnection.PauseAsync();
            await VoiceConnection.PlayAsync(track);
        }

        [Command("voteskip"), Description("2Work in Progess."), Aliases("vs")]
        public async Task VoteSkipAsync(CommandContext ctx)
        {
            try
            {
                if (!DeletePool.ContainsKey(ctx.Message.Id))
                    DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
                if (CheckHasCooldown(ctx))
                {
                    SendCooldownAsync(ctx);
                    return;
                }
                if (CheckHasPermission(ctx, role.everyone))
                    return;
                if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                     return;
                if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
                {
                    SendNotInSameChannelAsync(ctx);
                    return;
                }
                if (await CheckIsBotChannelAndMessagesExits(ctx))
                    return;

                if (!TrackLoadPlaylists.ContainsKey(ctx.Guild.Id))
                    return;
                if (TrackLoadPlaylists[ctx.Guild.Id].Count <= 0)
                {
                    //await StopAsync(ctx);
                    return;
                }
                if (!BotChannels.ContainsKey(ctx.Guild.Id))
                    return;
                var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);

                if (VoteSkip.ContainsKey(ctx.Guild.Id))
                {
                    if (VoteSkip[ctx.Guild.Id])
                    {
                        var VoteSkipAlreadyExistsEmbed = new DiscordEmbedBuilder
                        {
                            Description = "One voteskip already exists.",
                            Color = DiscordColor.Orange
                        };
                        var msgal = chn.SendMessageAsync(embed: VoteSkipAlreadyExistsEmbed);
                        DeletePool.Add(msgal.Result.Id, new DeleteMessage(chn, msgal.Result));
                        return;
                    }

                    VoteSkip[ctx.Guild.Id] = true;
                }
                else
                {
                    VoteSkip.Add(ctx.Guild.Id, true);
                }
                if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
                {
                    SendRestrictedChannelAsync(ctx);
                    VoteSkip[ctx.Guild.Id] = false;
                    return;
                }
                int UserCount = VoiceConnection.Channel.Users.Count()-1;
                if (UserCount <= 0)
                    return;
                if (UserCount == 1)
                {
                    await VoteSkipSkipFunction(ctx);
                    var VoteSkip1Embed = new DiscordEmbedBuilder
                    {
                        Title = "Voteskip",
                        Description = "Voteskip was successfully.",
                        Color = DiscordColor.Orange
                    };
                    DiscordMessage msg2 = chn.SendMessageAsync(embed: VoteSkip1Embed).Result;
                    DeletePool.Add(msg2.Id, new DeleteMessage(chn, msg2));
                    VoteSkip[ctx.Guild.Id] = false;
                    return;
                }
                var VoteSkipEmbed = new DiscordEmbedBuilder
                {
                    Title = "Voteskip",
                    Description = "Please vote to skip with the reaction.",
                    Color = DiscordColor.Orange
                };
                VoteSkipEmbed.WithFooter($"Voteskip running: 1/{UserCount}");
                DiscordMessage msg = chn.SendMessageAsync(embed: VoteSkipEmbed).Result;
                if (msg == null)
                {
                    VoteSkip[ctx.Guild.Id] = false;
                    return;
                }
                await msg.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":track_next:"));
                var interactivity = ctx.Client.GetInteractivity();
                var DatetimeNow = DateTime.Now;
                List<ulong> DiscordMemberAlreadyVoted = new List<ulong>();
                DiscordMemberAlreadyVoted.Add(ctx.Member.Id);
                while (true)
                {
                    if (DateTime.Now >= DatetimeNow.AddMinutes(1))
                    {
                        if (msg.Channel == null)
                        {
                            VoteSkip[ctx.Guild.Id] = false;
                            return;
                        }
                        await msg.DeleteReactionsEmojiAsync(DiscordEmoji.FromName(ctx.Client, ":track_next:"));
                        DiscordEmbed editedembed = msg.Embeds.First();
                        DiscordEmbedBuilder EditedEmbed = new DiscordEmbedBuilder
                        {
                            Title = "Voteskip",
                            Description = "Voteskip was aborted.",
                            Color = DiscordColor.Orange
                        };
                        await msg.ModifyAsync(embed: EditedEmbed.Build());
                        DeletePool.Add(msg.Id, new DeleteMessage(chn, msg));
                        VoteSkip[ctx.Guild.Id] = false;
                    }
                    await Task.Delay(1000);
                    var result = await interactivity.WaitForReactionAsync(x => x.Message == msg && x.User != ctx.Client.CurrentUser);
                    if (result.Result == null)
                        continue;
                    await msg.DeleteReactionAsync(result.Result.Emoji, result.Result.User);
                    if (result.Result.Emoji == DiscordEmoji.FromName(ctx.Client, ":track_next:"))
                    {
                        if (DiscordMemberAlreadyVoted.Contains(result.Result.User.Id))
                        {
                            continue;
                        }
                        DiscordMemberAlreadyVoted.Add(result.Result.User.Id);
                        DiscordEmbed editedembed = msg.Embeds.First();
                        DiscordEmbedBuilder EditedEmbed = new DiscordEmbedBuilder
                        {
                            Title = "Voteskip",
                            Description = "Please vote to skip with the reaction.",
                            Color = DiscordColor.Orange
                        };
                        EditedEmbed.WithFooter($"Voteskip running: {DiscordMemberAlreadyVoted.Count}/{UserCount}");
                        await msg.ModifyAsync(embed: EditedEmbed.Build());
                        if (Math.Ceiling((decimal)UserCount / 2) <= DiscordMemberAlreadyVoted.Count)
                        {
                            
                            if (msg.Channel == null)
                            {
                                VoteSkip[ctx.Guild.Id] = false;
                                return;
                            }
                            await msg.DeleteReactionsEmojiAsync(DiscordEmoji.FromName(ctx.Client, ":track_next:"));
                            await VoteSkipSkipFunction(ctx);
                            DiscordEmbedBuilder Editedembedsuccess = new DiscordEmbedBuilder
                            {
                                Title = "Voteskip",
                                Description = "Voteskip was successfully.",
                                Color = DiscordColor.DarkGreen
                            };
                            await msg.ModifyAsync(embed: Editedembedsuccess.Build());
                            DeletePool.Add(msg.Id, new DeleteMessage(chn, msg));
                            VoteSkip[ctx.Guild.Id] = false;
                            return;
                        }
                    }
                }

            }
            catch (Exception e)
            {
                VoteSkip[ctx.Guild.Id] = false;
                Console.WriteLine(e);
            }
        }
        //Jump to the Song with index
        [Command("jump"), Description("2Jumps to a special song in the queue."), Aliases("j", "goto")]
        public async Task JumpAsync(CommandContext ctx, [Description("Index der Playlist"), RemainingText] int index)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
           
            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            if (TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() == 1)
            {
                SendQueueIsEmptyAsync(ctx);
                return;
            }

            if (index > TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() - 1 || index < 1)
            {

                var SendOutOfRangeEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Track `{index}` is out of range.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendOutOfRangeEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }
            var track = TrackLoadPlaylists[ctx.Guild.Id].GetItemByIndex(index);
            await VoiceConnection.PauseAsync();
            await VoiceConnection.PlayAsync(track);
            TrackLoadPlaylists[ctx.Guild.Id].RemoveRange(1, index);
            var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");

        }
       
        //Remove song with Index
        [Command("remove"), Description("2Deletes a song from the queue."), Aliases("r", "rm", "delete", "del")]
        public async Task RemoveAsync(CommandContext ctx, [Description("Index der Playlist"), RemainingText] int index)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() == 1)
            {
                SendQueueIsEmptyAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            if (index > TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() - 1 || index < 1)
            {
                var SendOutOfRangeEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Track `{index}` is out of range.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendOutOfRangeEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }
            TrackLoadPlaylists[ctx.Guild.Id].RemoveAt(index);
            var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}");


        }
       

        [Command("loop"), Description("2Switch between 3 loop modes.\nOff: Loop deactivated.\nLoopqueue: Loops the complete queue.\nLoopsong: Loops only a special song."), Aliases("l")]
        public async Task LoopAsync(CommandContext ctx)
          {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() == 1)
            {
                SendQueueIsEmptyAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            if (Loopmodes[ctx.Guild.Id] == loopmode.off)
            {
                Loopmodes[ctx.Guild.Id] = loopmode.loopqueue;
            }
            else if (Loopmodes[ctx.Guild.Id] == loopmode.loopqueue)
            {
                Loopmodes[ctx.Guild.Id] = loopmode.loopsong;
            }
            else if (Loopmodes[ctx.Guild.Id] == loopmode.loopsong)
            {
                Loopmodes[ctx.Guild.Id] = loopmode.off;
            }
            var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{TrackLoadPlaylists[ctx.Guild.Id].First().Title}`", ImageUrl: getThumbnail(TrackLoadPlaylists[ctx.Guild.Id].First()), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, TrackLoadPlaylists[ctx.Guild.Id].First())}");



            // var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            //var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            //await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetFavoriteMessage(ctx.Guild.Id, track)}");

        }

        /*
        [Command("back"), Description("Spielt das vorherige Lied."), Aliases("b", "previous", "prev")]
        public async Task BackAsync(CommandContext ctx)
        {
            LavalinkGuildConnection VoiceConnection;
            VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

            if (VoiceConnection == null)
                return;

            if (CurrentSong[ctx.Guild.Id] == 0)
            {
                CurrentSong[ctx.Guild.Id] = getQueueCount(ctx.Guild.Id);
                return;
            }


            //   var track = trackLoadplaylist.GetItemByIndex(CurrentSong);
            CurrentSong[ctx.Guild.Id]--;
            await VoiceConnection.PauseAsync().ConfigureAwait(false);
            //  await ctx.RespondAsync($"`{track.Title}` von `{track.Author}` wurde übersprungen.").ConfigureAwait(false);
            await VoiceConnection.PlayAsync(TrackLoadPlaylists[ctx.Guild.Id].GetItemByIndex(CurrentSong[ctx.Guild.Id]));
        }*/

        //Queue clear
        [Command("clear"), Description("2Clears the queue.")]
        public async Task ClearAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            LavalinkGuildConnection VoiceConnection;
            VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

            if (VoiceConnection == null)
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            if (TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() == 0)
                return;

            if (TrackLoadPlaylists[ctx.Guild.Id].Count > 1)
                TrackLoadPlaylists[ctx.Guild.Id].RemoveRange(1, TrackLoadPlaylists[ctx.Guild.Id].Count - 1);
            var SendQueueClearedEmbed = new DiscordEmbedBuilder
            {
                Description = $"The queue has been emptied.",
                Color = DiscordColor.Orange
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendQueueClearedEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
            var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange);

        }

        //Get current Lyrics
        /*[Command("lyrics"), Description("Zeigt die Lyrics an.")]
        public async Task LyricsAsync(CommandContext ctx)
        {
            LavalinkGuildConnection VoiceConnection;
            VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

            if (VoiceConnection == null)
                return;


            if (VoiceConnection.CurrentState.CurrentTrack == null)
                return;


            var lyricsService = new LyricsService(new LyricsOptions());

            var title = VoiceConnection.CurrentState.CurrentTrack.Title.Replace("(Official Video)", String.Empty);

            title = title.Replace(VoiceConnection.CurrentState.CurrentTrack.Author, String.Empty);

            var lyrics = await lyricsService.GetLyricsAsync(Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(VoiceConnection.CurrentState.CurrentTrack.Author)).Replace("?", String.Empty), Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(title)).Replace("?", String.Empty));

            if (lyrics == null)
            {
                await ctx.RespondAsync($"Lyrics not found.").ConfigureAwait(false);
                return;
            }
            await ctx.RespondAsync($"`{VoiceConnection.CurrentState.CurrentTrack.Title}` von `{VoiceConnection.CurrentState.CurrentTrack.Author}`:\n`{lyrics}`").ConfigureAwait(false);
        }

        //Get Lyrics by Keyword
        [Command, Description("Zeigt die Lyrics eines bestimmtes Liedes an.")]
        public async Task LyricsAsync(CommandContext ctx, [Description("Schlüsselwörter"), RemainingText]String Keywords)
        {
            LavalinkGuildConnection VoiceConnection;
            VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

            if (VoiceConnection == null)
                return;

            var lyricsService = new LyricsService(new LyricsOptions());

            var trackLoad = await this.Lavalink.Rest.GetTracksAsync(Keywords);

            if (trackLoad == null)
            {
                await ctx.RespondAsync($"Song not found.").ConfigureAwait(false);
                return;
            }


            var title = trackLoad.Tracks.ToList().First().Title.Replace("(Official Video)", "");
            title = title.Replace(trackLoad.Tracks.ToList().First().Author, "");


            var lyrics = await lyricsService.GetLyricsAsync(trackLoad.Tracks.ToList().First().Author, title);

            if (lyrics == null)
            {
                await ctx.RespondAsync($"Lyrics not found.").ConfigureAwait(false);
                return;
            }

            await ctx.RespondAsync($"`{trackLoad.Tracks.ToList().First().Title}` von `{trackLoad.Tracks.ToList().First().Author}`:\n`{lyrics}`").ConfigureAwait(false);
        }*/

        [Command("pause"), Description("1Pauses playback.")]
        public async Task PauseAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if(!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() == 1)
            {
                SendQueueIsEmptyAsync(ctx);
                return;
            }
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            var chn = await ctx.Client.GetChannelAsync(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            if (!IsPausedStates[ctx.Guild.Id])
            {
                await VoiceConnection.PauseAsync();
                IsPausedStates[ctx.Guild.Id] = !IsPausedStates[ctx.Guild.Id];
                var track = TrackLoadPlaylists[ctx.Guild.Id].First();
                await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Paused at `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");
            }
         }

        [Command("resume"), Description("1Resumes playback.")]
        public async Task ResumeAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() == 1)
            {
                SendQueueIsEmptyAsync(ctx);
                return;
            }
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            var chn = await ctx.Client.GetChannelAsync(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            if (IsPausedStates[ctx.Guild.Id])
            {
                await VoiceConnection.ResumeAsync();
                IsPausedStates[ctx.Guild.Id] = !IsPausedStates[ctx.Guild.Id];
                var track = TrackLoadPlaylists[ctx.Guild.Id].First();
                await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");
            }


        }

        /*   [Command("ping"), Description("4Get current Websocket Latency to Discord API")]
        public async Task PingAsync(CommandContext ctx)
        {
            DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            DiscordEmbedBuilder PingEmbed = new DiscordEmbedBuilder
            {
                Title = $"Ping: {ctx.Client.Ping} ms",
                Description = $"",
                Color = DiscordColor.DarkGreen
            };
            if(ctx.Client.Ping <= 35)
            {
                PingEmbed.WithColor(DiscordColor.DarkGreen);
            } else if (ctx.Client.Ping <= 75)
            {
                PingEmbed.WithColor(DiscordColor.Orange);
            }
            else if (ctx.Client.Ping > 75)
            {
                PingEmbed.WithColor(DiscordColor.Red);
            }

            await ctx.Channel.SendMessageAsync(embed: PingEmbed);
        }*/

        [Command("stop"), Description("1Stops playback and clear the queue.")]
        public async Task StopAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            TrackLoadPlaylists[ctx.Guild.Id].Clear();
            await VoiceConnection.StopAsync().ConfigureAwait(false);
            var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {ctx.Prefix}"); // PREFIX
        }

        [Command("shuffle"), Description("2Shuffel the current queue.")]
        public async Task ShuffleAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
    
            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            await VoiceConnection.PauseAsync();
            Utils.Shuffle(TrackLoadPlaylists[ctx.Guild.Id]);
            var track = TrackLoadPlaylists[ctx.Guild.Id].First();
            await VoiceConnection.PlayAsync(track);
            var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");

        }

        [Command("24/7"), Description("2Enable the 24/7 mode.\nThe bot will no longer automatically leave the voice channel."), Aliases("247")]
        public async Task TwentyFourSevenAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            if (AFKTimeOffsets.ContainsKey(ctx.Guild.Id))
                AFKTimeOffsets[ctx.Guild.Id] = new DateTimeOffset();

            if (CheckAFKStates.ContainsKey(ctx.Guild.Id))
                CheckAFKStates[ctx.Guild.Id] = !CheckAFKStates[ctx.Guild.Id];
            else
                CheckAFKStates.Add(ctx.Guild.Id, false);

            if (CheckAFKStates[ctx.Guild.Id] == false)
            {

                var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
                {
                    Description = "The 24/7 mode has been enabled.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
            } 
            else
            {
                var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
                {
                    Description = "The 24/7 mode has been disabled.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
            }
            
        }

        /* [Command("announce"), Description("WIP")]
         public async Task AnnounceAsync(CommandContext ctx)
         {
            // LavalinkGuildConnection VoiceConnection;
            // VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

            // if (VoiceConnection == null)
             //    return;

             if (AnnounceStates.ContainsKey(ctx.Guild.Id))
                 AnnounceStates[ctx.Guild.Id] = !AnnounceStates[ctx.Guild.Id];
             else
                 AnnounceStates.Add(ctx.Guild.Id, false);

             if (AnnounceStates[ctx.Guild.Id] == false)
             {
                 await ctx.RespondAsync("Announce was disabled").ConfigureAwait(false);
             }
             else
             {
                 await ctx.RespondAsync("Announce was enabled").ConfigureAwait(false);
             }

         }
 */
        [Command("seek"), Description("2Goes to a specific position in the current song.\nIn the following format (max 2 digits each):\n [Seconds] \n [Minutes]:[Seconds] \n [Hours]:[Minutes]:[Seconds]")]
        public async Task SeekAsync(CommandContext ctx, String pos)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            DateTime dt;
            if (pos.Length == 1 && Char.IsDigit(pos.ToList().First()))
            {
               pos = pos.PadLeft(2, '0');
            }
            if (DateTime.TryParseExact(pos, "ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) {  }
            else if (DateTime.TryParseExact(pos, "mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) {  }
            else if (DateTime.TryParseExact(pos, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) {  }
            else
            {
                var SendErrMsgEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Wrong Format.\nUse {ctx.Prefix}help seek to get more information.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage errmsg = await ctx.Channel.SendMessageAsync(embed: SendErrMsgEmbed);
                DeletePool.Add(errmsg.Id, new DeleteMessage(ctx.Channel, errmsg));
                return; 
            }
     
            TimeSpan position = dt.TimeOfDay;

            String Time;
            if (position.Days != 0)
                Time = position.ToString("d'd 'h'h 'm'm 's's'");
            else if (position.Hours != 0)
                Time = position.ToString("h'h 'm'm 's's'");
            else if(position.Minutes != 0)
                Time = position.ToString("m'm 's's'");
            else
                Time = position.ToString("s's'");

            await VoiceConnection.SeekAsync(position);
            var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
            {
                Description = $"Fast forward {Time}.",
                Color = DiscordColor.Orange
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        [Command("volume"), Description("2Lautstärke regeln."), Aliases("v")]
        public async Task VolumeAsync(CommandContext ctx, int volume)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
    
            LavalinkGuildConnection VoiceConnection;
            VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

            if (VoiceConnection == null)
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            if (volume < 0 || volume > 1000)
            {
                var SendnotInRangeEmbed = new DiscordEmbedBuilder
                {
                    Description = "Volume need to be between 0 and 1000%.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendnotInRangeEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }

            await VoiceConnection.SetVolumeAsync(volume);
            Volumes[VoiceConnection] = volume;

            var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
           
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, Footer: $"0 songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}");
        }
        //Get current song
        /*     [Command("song"), Description("1Shows what's being currently played."), Aliases("np", "nowplaying")]
                public async Task NowPlayingAsync(CommandContext ctx)
                {
                    LavalinkGuildConnection VoiceConnection;
                    VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);
    
                    if (VoiceConnection == null)
                    {
                        await ctx.RespondAsync("No song is currently played.").ConfigureAwait(false);
                        return;
                    }
                    if (CheckHasCooldown(ctx))
                    {
                        SendCooldownAsync(ctx);
                        return;
                    }
                    if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
                    {
                        SendRestrictedChannelAsync(ctx);
                        return;
                    }

                    var track = VoiceConnection.CurrentState.CurrentTrack;
                    if (track == null)
                        await ctx.RespondAsync("No song is currently played.").ConfigureAwait(false);
                    else
                        await ctx.RespondAsync($"Now playing: `{track.Title}` by `{track.Author}`.").ConfigureAwait(false);

                }
                //Get song by Keywords
                [Command("song")]
                public async Task NowPlayingAsync(CommandContext ctx, [Description("wip"), RemainingText] String Keywords)
                {
                    LavalinkGuildConnection VoiceConnection;
                    VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

                    if (VoiceConnection == null)
                        return;
                    if (CheckHasCooldown(ctx))
                    {
                        SendCooldownAsync(ctx);
                        return;
                    }
                    if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
                    {
                        SendRestrictedChannelAsync(ctx);
                        return;
                    }
                    var track = GetCurrentSongByKeywords(Keywords, ctx);
                    if (track == null)
                        await ctx.RespondAsync("Not found.").ConfigureAwait(false);
                    else
                        await ctx.RespondAsync($"`{track.Title}` by `{track.Author}`.").ConfigureAwait(false);

                }
                //Get song by Index
                [Command("song")]
                public async Task NowPlayingAsync(CommandContext ctx, [Description("wip"), RemainingText] int index)
                {
                    LavalinkGuildConnection VoiceConnection;
                    VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);
                    if (VoiceConnection == null)
                        return;
                    if (CheckHasCooldown(ctx))
                    {
                        SendCooldownAsync(ctx);
                        return;
                    }
                    if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
                    {
                        SendRestrictedChannelAsync(ctx);
                        return;
                    }
                    if (index > TrackLoadPlaylists[ctx.Guild.Id].ToList().Count() || index < 1)
                    {

                        await ctx.RespondAsync($"Track `{index}` is out of range.").ConfigureAwait(false);
                        return;
                    }

                    var track = TrackLoadPlaylists[ctx.Guild.Id].ToList().GetItemByIndex(index);

                    if (track == null)
                        await ctx.RespondAsync("Not found").ConfigureAwait(false);
                    else
                        await ctx.RespondAsync($"`{track.Title}` by `{track.Author}`.").ConfigureAwait(false);
                }
                */
        [Command("equalizer"), Description("2Resets equalizer settings.|Sets equalizer settings."), Aliases("eq")]
        public async Task EqualizerAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            await VoiceConnection.ResetEqualizerAsync();
            
            var eqmsg = await ctx.RespondAsync("All equalizer bands were reset.").ConfigureAwait(false);
            DeletePool.Add(eqmsg.Id, new DeleteMessage(eqmsg.Channel, eqmsg));
        }

        [Command("equalizer")]
        public async Task EqualizerAsync(CommandContext ctx, int Band, float Gain)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            await VoiceConnection.AdjustEqualizerAsync(new LavalinkBandAdjustment(Band, Gain));
            
            var eqmsg = await ctx.RespondAsync($"Band {Band} adjusted by {Gain}").ConfigureAwait(false);
            DeletePool.Add(eqmsg.Id, new DeleteMessage(eqmsg.Channel, eqmsg));
        }

        /*    [Command("prefix")]
            public async Task PrefixAsync(CommandContext ctx)
            {

            }*/

        [Command("setdj"), Description("3Set a Role as DJ")]
        public async Task SetDjAsync(CommandContext ctx, [Description("@Role Name")]String RoleName)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.admin))
                return;
            DiscordRole Role = null;
            foreach (KeyValuePair<ulong, DiscordRole> entry in ctx.Guild.Roles)
            {
                if(entry.Value.Name == RoleName.Replace("@", ""))
                {
                    Role = entry.Value;
                    break;
                }
                
            }
            
            if(Role == null)
            {
                var SendRoleNotFoundEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Role ***{RoleName}*** was not found.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleNotFoundEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }
            
            if (GuildRoles.ContainsKey(ctx.Guild.Id))
            {
                if (GuildRoles[ctx.Guild.Id].Contains(new Tuple<DiscordRole, role>(Role, role.dj))) 
                {
                    var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was already added.",
                        Color = DiscordColor.Orange
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                    DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                    return;
                } else
                {
                    var tup = GuildRoles[ctx.Guild.Id];
                    tup.Add(new Tuple<DiscordRole, role>(Role, role.dj));
                    var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was added to DJs.",
                        Color = DiscordColor.DarkGreen
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                    DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                    return;
                }
                
                
            } else
            {
                List<Tuple<DiscordRole, role>> tup = new List<Tuple<DiscordRole, role>>();
                tup.Add(new Tuple<DiscordRole, role>(Role, role.dj));
                GuildRoles.Add(ctx.Guild.Id, tup);
                var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Role ***{Role.Name}*** was added to DJs.",
                    Color = DiscordColor.DarkGreen
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }

        }
        [Command("removedj"), Description("3Set a Role as DJ"), Aliases("remdj")]
        public async Task RemoveDjAsync(CommandContext ctx, [Description("@Role Name")] String RoleName)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.admin))
                return;
            DiscordRole Role = null;
            foreach (KeyValuePair<ulong, DiscordRole> entry in ctx.Guild.Roles)
            {
                if (entry.Value.Name == RoleName.Replace("@", ""))
                {
                    Role = entry.Value;
                    break;
                }

            }

            if (Role == null)
            {
                var SendRoleNotFoundEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Role ***{RoleName}*** was not found.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleNotFoundEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }

            if (GuildRoles.ContainsKey(ctx.Guild.Id))
            {
                if (GuildRoles[ctx.Guild.Id].Contains(new Tuple<DiscordRole, role>(Role, role.dj)))
                {
                    GuildRoles[ctx.Guild.Id].Remove(new Tuple<DiscordRole, role>(Role, role.dj));
                    var SendRoleRemovedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was removed from Djs.",
                        Color = DiscordColor.Orange
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleRemovedEmbed);
                    DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                    return;
                }
                else
                {
                    var SendRoleIsNotAddedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** is not added to DJs.",
                        Color = DiscordColor.DarkGreen
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleIsNotAddedEmbed);
                    DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                    return;
                }

            }
            else
            {
                var SendRoleIsNotAddedEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Role ***{Role.Name}*** is not added to DJs.",
                    Color = DiscordColor.DarkGreen
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleIsNotAddedEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }

        }
        [Command("djs"), Description("3Get all the roles that are Djs.")]
        public async Task GetDjsAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.admin))
                return;
            String DjList = String.Empty;
            foreach(Tuple<DiscordRole, role> entry in GuildRoles[ctx.Guild.Id])
            {
                if (entry.Item2 == role.dj)
                {
                    DjList += $"• {entry.Item1.Name}\n";
                }
            }
            if(DjList == String.Empty)
            {
                var SendDjListEmptyEmbed = new DiscordEmbedBuilder
                {
                    Title = "Dj List",
                    Description = $"No Roles found.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage msg1 = await ctx.Channel.SendMessageAsync(embed: SendDjListEmptyEmbed);
                DeletePool.Add(msg1.Id, new DeleteMessage(ctx.Channel, msg1));
                return;
            }
            var SendDjListEmbed = new DiscordEmbedBuilder
            {
                Title = "Dj List",
                Description = $"Roles:\n```{DjList}```",
                Color = DiscordColor.DarkGreen
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendDjListEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));

        }
        [Command("setadmin"), Description("3Set a Role as Admin")]
        public async Task SetAdminAsync(CommandContext ctx, [Description("@Role Name")] String RoleName)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.admin))
                return;
            DiscordRole Role = null;
            foreach (KeyValuePair<ulong, DiscordRole> entry in ctx.Guild.Roles)
            {
                if (entry.Value.Name == RoleName.Replace("@", ""))
                {
                    Role = entry.Value;
                    break;
                }

            }

            if (Role == null)
            {
                var SendRoleNotFoundEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Role ***{RoleName}*** was not found.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleNotFoundEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }
            if (GuildRoles.ContainsKey(ctx.Guild.Id))
            {
                if (GuildRoles[ctx.Guild.Id].Contains(new Tuple<DiscordRole, role>(Role, role.admin)))
                {
                    var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was already added.",
                        Color = DiscordColor.Orange
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                    DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                    return;
                }
                else
                {
                    var tup = GuildRoles[ctx.Guild.Id];
                    tup.Add(new Tuple<DiscordRole, role>(Role, role.admin));
                    var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was added to Admins.",
                        Color = DiscordColor.DarkGreen
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                    DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                    return;
                }


            }
            else
            {
                List<Tuple<DiscordRole, role>> tup = new List<Tuple<DiscordRole, role>>();
                tup.Add(new Tuple<DiscordRole, role>(Role, role.admin));
                GuildRoles.Add(ctx.Guild.Id, tup);
                var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Role ***{Role.Name}*** was added to Admins.",
                    Color = DiscordColor.DarkGreen
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }

        }
        [Command("removeadmin"), Description("3Set a Role as DJ"), Aliases("remadmin")]
        public async Task RemoveAdminAsync(CommandContext ctx, [Description("@Role Name")] String RoleName)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.admin))
                return;
            DiscordRole Role = null;
            foreach (KeyValuePair<ulong, DiscordRole> entry in ctx.Guild.Roles)
            {
                if (entry.Value.Name == RoleName.Replace("@", ""))
                {
                    Role = entry.Value;
                    break;
                }

            }

            if (Role == null)
            {
                var SendRoleNotFoundEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Role ***{RoleName}*** was not found.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleNotFoundEmbed);
                DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }

            if (GuildRoles.ContainsKey(ctx.Guild.Id))
            {
                if (GuildRoles[ctx.Guild.Id].Contains(new Tuple<DiscordRole, role>(Role, role.admin)))
                {
                    GuildRoles[ctx.Guild.Id].Remove(new Tuple<DiscordRole, role>(Role, role.admin));
                    var SendRoleRemovedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was removed from Admins.",
                        Color = DiscordColor.Orange
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleRemovedEmbed);
                    DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                    return;
                }
                else
                {
                    var SendRoleIsNotAddedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** is not added to Admins.",
                        Color = DiscordColor.DarkGreen
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleIsNotAddedEmbed);
                    DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                    return;
                }

            }
            else
            {
                var SendRoleIsNotAddedEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Role ***{Role.Name}*** is not added to DJs.",
                    Color = DiscordColor.DarkGreen
                };
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleIsNotAddedEmbed);
                
                return;
            }

        }

        [Command("admins"), Description("3Get all the roles that are Admins.")]
        public async Task GetAdminsAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.admin))
                return;
            var SendAdminListEmptyEmbed = new DiscordEmbedBuilder
            {
                Title = "Admin List",
                Description = "No Roles found.",
                Color = DiscordColor.Orange
            };
            if (!GuildRoles.ContainsKey(ctx.Guild.Id))
            {
                DiscordMessage msg1 = await ctx.Channel.SendMessageAsync(embed: SendAdminListEmptyEmbed);
                DeletePool.Add(msg1.Id, new DeleteMessage(ctx.Channel, msg1));
                return;
            }
            String AdminList = String.Empty;
            foreach (Tuple<DiscordRole, role> entry in GuildRoles[ctx.Guild.Id])
            {
                if (entry.Item2 == role.admin)
                {
                    AdminList += $"• {entry.Item1.Name}\n";
                }
            }
            if (AdminList == String.Empty)
            {
                DiscordMessage msg1 = await ctx.Channel.SendMessageAsync(embed: SendAdminListEmptyEmbed);
                DeletePool.Add(msg1.Id, new DeleteMessage(ctx.Channel, msg1));
                return;
            }
            var SendAdminListEmbed = new DiscordEmbedBuilder
            {
                Title = "Admin List",
                Description = $"Roles:\n```{AdminList}```",
                Color = DiscordColor.DarkGreen
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendAdminListEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));

        }

        [Command("prefix"), Description("3Get and sets the Prefix.")]
        public async Task PrefixAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.admin))
                return;

            var reader = db.Query($"SELECT Prefix FROM data WHERE GuildId IN ({ctx.Guild.Id})");
            DiscordEmoji SetNewEmoji = DiscordEmoji.FromName(ctx.Client, ":regional_indicator_s:");
            DiscordEmoji Crossed = DiscordEmoji.FromName(ctx.Client, ":x:");
            string prefixes = String.Empty;
            while (reader.HasRows)
            {
                while (reader.Read())
                {
                    if (reader.GetString(0) != String.Empty)
                        prefixes = reader.GetString(0);
                }

                await reader.NextResultAsync();
            }
            db.Disconnect();
            var PrefixFirstEmbed = new DiscordEmbedBuilder
            {
                Title = $"Prefixes for this server!",
                Description = $"Current prefix: `{prefixes}`",
                Color = DiscordColor.DarkGreen
            };
            PrefixFirstEmbed.AddField("Set new Prefix", $"To set a new prefix react with {SetNewEmoji}.");
            PrefixFirstEmbed.AddField("Cancel", $"To cancel this action react with {Crossed}.");
            var PrefixFirstMsg = await ctx.Channel.SendMessageAsync(embed: PrefixFirstEmbed);
            await PrefixFirstMsg.CreateReactionAsync(SetNewEmoji);
            await PrefixFirstMsg.CreateReactionAsync(Crossed);

            var AbortEmbed = new DiscordEmbedBuilder
            {
                Title = $"Action aborted",
                Color = DiscordColor.Red
            };


            InteractivityResult<MessageReactionAddEventArgs> firstresult = await ctx.Client.GetInteractivity().WaitForReactionAsync(x => x.Message == PrefixFirstMsg && x.User != ctx.Client.CurrentUser);
            if (firstresult.TimedOut)
            {
                DeletePool.Add(PrefixFirstMsg.Id, new DeleteMessage(ctx.Channel, PrefixFirstMsg));
                var abort = await ctx.Channel.SendMessageAsync(embed: AbortEmbed);
                await Task.Delay(5000);
                DeletePool.Add(abort.Id, new DeleteMessage(ctx.Channel, abort));
                return;
            }

            // await PrefixFirstMsg.DeleteReactionAsync(result.Result.Emoji, result.Result.User);

            if (firstresult.Result.Emoji == SetNewEmoji)
            {
                DeletePool.Add(PrefixFirstMsg.Id, new DeleteMessage(ctx.Channel, PrefixFirstMsg));
                var PrefixSetNewEmbed = new DiscordEmbedBuilder
                {
                    Title = "Set new prefix!",
                    Description = "Please write the new prefix below.\nOnly one letter or special char!",
                    Color = DiscordColor.DarkGreen
                };
                var setnew = await ctx.Channel.SendMessageAsync(embed: PrefixSetNewEmbed);
                InteractivityResult<DiscordMessage> newprefixresult = await ctx.Client.GetInteractivity().WaitForMessageAsync(x => x.Author == firstresult.Result.User);
                if (newprefixresult.TimedOut)
                {
                    DeletePool.Add(setnew.Id, new DeleteMessage(ctx.Channel, setnew));
                    var abort = await ctx.Channel.SendMessageAsync(embed: AbortEmbed);
                    await Task.Delay(5000);
                    DeletePool.Add(abort.Id, new DeleteMessage(ctx.Channel, abort));
                    return;
                }
                string newprefix = newprefixresult.Result.Content.ToCharArray().First().ToString();
                var PrefixNewSuccessEmbed = new DiscordEmbedBuilder
                {
                    Title = "New Prefix succesfully set!",
                    Color = DiscordColor.DarkGreen
                };
                PrefixNewSuccessEmbed.AddField("Old Prefix", $"`{prefixes}`");
                PrefixNewSuccessEmbed.AddField("New Prefix", $"`{newprefix}`");

                db.Execute($"UPDATE data SET Prefix = '{newprefix}' WHERE GuildId IN ({ctx.Guild.Id})");

                DeletePool.Add(setnew.Id, new DeleteMessage(ctx.Channel, setnew));
                var success = await ctx.Channel.SendMessageAsync(embed: PrefixNewSuccessEmbed);
                var MainMsg = await ctx.Channel.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
                await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, Footer: $"Prefix for this Server is: {newprefix}");
                await Task.Delay(5000);
                DeletePool.Add(success.Id, new DeleteMessage(ctx.Channel, success));
            }

            if (firstresult.Result.Emoji == Crossed)
            {
                DeletePool.Add(PrefixFirstMsg.Id, new DeleteMessage(ctx.Channel, PrefixFirstMsg));
                var abort = await ctx.Channel.SendMessageAsync(embed: AbortEmbed);
                await Task.Delay(5000);
                DeletePool.Add(abort.Id, new DeleteMessage(ctx.Channel, abort));
                return;
            }

        }

        [Command("support"), Description("1To get support.")]
        public async Task SupportAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));


            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            DiscordEmbedBuilder CreditsEmbed = new DiscordEmbedBuilder
            {
                Title = "Support",
                Color = DiscordColor.Blurple,
                Description = "To get support you can simply write to us."
            };
            var lemix = await ctx.Client.GetUserAsync(267645496020041729);
            var michi = await ctx.Client.GetUserAsync(352508207094038538);
            CreditsEmbed.AddField("For technical support:", lemix.Mention);
            CreditsEmbed.AddField("For coopartions or further:", michi.Mention);
            CreditsEmbed.WithFooter("This bot is in beta and is constantly being developed.");
            var msg = await ctx.Channel.SendMessageAsync(embed: CreditsEmbed);
            await Task.Delay(15000);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        [Command("invite"), Description("1To get an invitation link.")]
        public async Task InviteAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));


            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            DiscordEmbedBuilder CreditsEmbed = new DiscordEmbedBuilder
            {
                Title = "Invitation Link",
                Color = DiscordColor.Purple,
                Description = "The link is there to invite the bot to other servers."
            };
            CreditsEmbed.AddField("Link", $"[Click here](https://discord.com/oauth2/authorize?client_id={ctx.Client.CurrentUser.Id}&permissions=3271760&scope=bot)");
            CreditsEmbed.WithFooter("This bot is in beta and is constantly being developed.");
            var msg = await ctx.Channel.SendMessageAsync(embed: CreditsEmbed);
            await Task.Delay(5000);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        //Description first char:
        // 1 or nothing(please use 1 to provide errors) = everyone
        // 2 = dj
        // 3 = Admin
        // 4 = hidden
        [Command("help"), Description("4")]
        public async Task HelpAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            Dictionary<String, Command> FilteredCommands = new Dictionary<string, Command>();
            foreach (KeyValuePair<String, Command> entry in ctx.CommandsNext.RegisteredCommands)
            {
                if (!FilteredCommands.ContainsKey(entry.Value.Name))
                    FilteredCommands.Add(entry.Key, entry.Value);
            }
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            DiscordEmbedBuilder HelpEmbed = new DiscordEmbedBuilder
            {
                Title = "Help Command"
            };
            HelpEmbed.WithFooter($"With '{ctx.Prefix}{ctx.Command.Name} [Command]' you get detailed informations");
            String EveryoneCommands = string.Empty;
            String DjCommands = string.Empty;
            String AdminCommands = string.Empty;
            foreach (KeyValuePair<String, Command> entry in FilteredCommands)
            {

                if (entry.Value.Description == string.Empty)
                {
                    if (EveryoneCommands == String.Empty)
                        EveryoneCommands += $"`{entry.Value.Name}`";
                    EveryoneCommands += $", `{entry.Value.Name}`";
                    continue;
                }
                if (entry.Value.Description.ToCharArray().First().ToString() == "2")
                {
                    if (DjCommands == String.Empty)
                    {
                        DjCommands += $"`{entry.Value.Name}`";
                        continue;
                    }

                    DjCommands += $", `{entry.Value.Name}`";
                }
                else if (entry.Value.Description.ToCharArray().First().ToString() == "3")
                {
                    if (AdminCommands == String.Empty)
                    {
                        AdminCommands += $"`{entry.Value.Name}`";
                        continue;
                    }

                    AdminCommands += $", `{entry.Value.Name}`";
                }
                else if (entry.Value.Description.ToCharArray().First().ToString() == "4")
                {
                    continue;
                }
                else
                {
                    if (EveryoneCommands == String.Empty)
                    {
                        EveryoneCommands += $"`{entry.Value.Name}`";
                        continue;
                    }

                    EveryoneCommands += $", `{entry.Value.Name}`";
                }
            }
            if (EveryoneCommands != String.Empty)
                HelpEmbed.AddField("Everyone commands", EveryoneCommands);
            if (DjCommands != String.Empty)
                HelpEmbed.AddField("DJ commands", DjCommands);
            if (AdminCommands != String.Empty)
                HelpEmbed.AddField("Administration commands", AdminCommands);
            var msg = await ctx.RespondAsync(embed: HelpEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        [Command("help")]
        public async Task HelpAsync(CommandContext ctx, String Command)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if(Command.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                await HelpAsync(ctx);
                return;
            }
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            Dictionary<String, Command> UnfilteredCommands = new Dictionary<string, Command>();
            foreach (KeyValuePair<String, Command> entry in ctx.CommandsNext.RegisteredCommands)
            {
                UnfilteredCommands.Add(entry.Key, entry.Value);
            }
            List<Command> cmd = new List<Command>();
            foreach (KeyValuePair<String, Command> entry in UnfilteredCommands)
            {
                if (entry.Value.Name.Equals(Command, StringComparison.OrdinalIgnoreCase) || entry.Value.Aliases.ToList().Exists(x => x.Equals(Command, StringComparison.OrdinalIgnoreCase)))
                {
                    cmd.Add(entry.Value);
                }
            }
            if (cmd.Count == 0 || cmd.First().Description.ToCharArray().First().ToString() == "4")
            {
                await HelpAsync(ctx);
                return;
                /* DiscordEmbedBuilder NotFoundEmbed = new DiscordEmbedBuilder
                 {
                     Title = $"Command {Command} not found",
                     Description = $"With '{ctx.Prefix}{ctx.Command.Name}' you can find all commands",
                     Color = DiscordColor.Red
                 };
                 await ctx.RespondAsync(embed: NotFoundEmbed);*/
            }
            String AliasesText = String.Empty;
            foreach (String entry in cmd.First().Aliases)
            {
                if (AliasesText == string.Empty)
                {
                    AliasesText += $"`{entry}`";
                    continue;
                }
                AliasesText += $", `{entry}`";
            }
            if (AliasesText == string.Empty)
            {
                AliasesText += "No aliases exist";
            }
            DiscordEmbedBuilder HelpEmbed = new DiscordEmbedBuilder
            {
                Title = $"Help Command: {cmd.First().Name}",
                Description = $"Aliases: {AliasesText}"
            };

            int i = 0;
            List<String> Descriptions = cmd.First().Description.Split("|").ToList();
            Descriptions[0] = Descriptions.First().Substring(1);
            foreach (CommandOverload entry1 in cmd.First().Overloads)
            {
                if (Descriptions.Count() <= i)
                    Descriptions.Add("No Description avaiable.");
                if (entry1.Arguments.Count != 0)
                {
                    String Arguments = String.Empty;
                    foreach (var entry2 in entry1.Arguments)
                    {
                        var descr = String.Empty;
                        if (entry2.Description != String.Empty)
                            descr = " " + entry2.Description;
                        else
                            descr = entry2.Description;
                        if (Arguments == String.Empty)
                        {
                            Arguments += $"`<{entry2.Name}>`";
                            continue;
                        }
                        Arguments += $" `<{entry2.Name}>`";
                    }
                    HelpEmbed.AddField($"{ctx.Prefix}{cmd[i].Name} {Arguments}", $"{Descriptions[i]}");
                    i++;
                }
                else
                {
                    HelpEmbed.AddField($"{ctx.Prefix}{cmd[i].Name}", $"{Descriptions[i]}");
                    i++;
                }
            }
            var msg = await ctx.RespondAsync(embed: HelpEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        [Command("credits"), Description("1")]
        public async Task CreditsAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

          
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            DiscordEmbedBuilder CreditsEmbed = new DiscordEmbedBuilder
            {
                Title = "Credits",
                Color = DiscordColor.Blurple
            };
            var lemix = await ctx.Client.GetUserAsync(267645496020041729);
            var michi = await ctx.Client.GetUserAsync(352508207094038538);
            CreditsEmbed.AddField("Developer", lemix.Mention, true);
            CreditsEmbed.AddField("Distribution and Marketing", michi.Mention, true);
            CreditsEmbed.AddField("Bot Creation Time", ctx.Client.CurrentApplication.CreationTimestamp.ToString());
            CreditsEmbed.WithFooter("This bot is in beta and is constantly being developed.");
            var msg = await ctx.Channel.SendMessageAsync(embed: CreditsEmbed);
            await Task.Delay(5000);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }


        // !sendglobalmsg "Title" "TEXT" "color code without #" "footer text"
        [Command("sendglobalmsg"), Description("4")]
        public async Task SendGlobalMessageAsync(CommandContext ctx, string Title, string Description, string Color, string Footer)
        {

            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
           
            if (ctx.Member.Id == 267645496020041729 || ctx.Member.Id == 352508207094038538)
            {
                DiscordMessage gm;
                int i = 0;
                DiscordEmbedBuilder GlobalMsgEmbed = new DiscordEmbedBuilder
                {
                    Title = "Global messages are sent!",
                    Description = "Please wait this process takes some time.",
                    Color = DiscordColor.Yellow
                };
                GlobalMsgEmbed.AddField("Total Guilds", ctx.Client.Guilds.Count.ToString());
                GlobalMsgEmbed.WithFooter("This process can no longer be stopped");
                gm = await ctx.RespondAsync(embed: GlobalMsgEmbed);
                foreach (KeyValuePair<ulong, DiscordGuild> guild in ctx.Client.Guilds)
                {
                    i++;

                    var embed = new DiscordEmbedBuilder()
                    {
                        Title = Title,
                        Description = Description
                    };
                    embed.WithThumbnail(ctx.Client.CurrentUser.AvatarUrl);
                    embed.WithColor(new DiscordColor(Color));
                    embed.WithFooter(Footer); 
                    await guild.Value.Owner.SendMessageAsync(embed: embed);
                    ctx.Client.Logger.LogInformation(new EventId(7879, "SendGlobalmsg"), $"Message sent to {guild.Value.Name} Owner ({i} out of {ctx.Client.Guilds.Count})");
                    await Task.Delay(1000);
                }
                DiscordEmbedBuilder GlobalMsgFinishedEmbed = new DiscordEmbedBuilder
                {
                    Title = "Global messages was sent!",
                    Description = $"{ctx.Client.Guilds.Count} messages was sent.",
                    Color = DiscordColor.Green
                };
                var gmf = await ctx.RespondAsync(embed: GlobalMsgFinishedEmbed);
                DeletePool.Add(gm.Id, new DeleteMessage(gm.Channel, gm));
                await Task.Delay(10000);
                DeletePool.Add(gmf.Id, new DeleteMessage(gmf.Channel, gmf));
                
            }

        }

        // !sendglobalmsg "Title" "TEXT" "color code without #" "footer text"
        [Command("sendglobalmsgtest"), Description("4")]
        public async Task SendGlobalMessageTestAsync(CommandContext ctx, string Title, string Description, string Color, string Footer)
        {

            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (ctx.Member.Id == 267645496020041729 || ctx.Member.Id == 352508207094038538)
            {
              
                var embed = new DiscordEmbedBuilder()
                {
                    Title = Title,
                    Description = Description
                };
                embed.WithThumbnail(ctx.Client.CurrentUser.AvatarUrl);
                embed.WithColor(new DiscordColor(Color));
                embed.WithFooter(Footer);
                await ctx.Member.SendMessageAsync($"USAGE: {ctx.Prefix}{ctx.Command.Name} \"Title\" \"Text\" \"Colorcode without # (HexCode)\" \"FooterText\" ",embed: embed);
              
            }

        }

        [Command("stats"), Description("4Developer Command.")]
        public async Task StatsAsync(CommandContext ctx)
        {

            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (ctx.Member.Id == 267645496020041729 || ctx.Member.Id == 352508207094038538)
            {
                ctx.Client.Logger.LogInformation(new EventId(7700, "StatsCmd"), $"{ctx.Member.DisplayName} [{ctx.Member.Id}] executed Stats Command!");

                ObjectQuery query = new ObjectQuery("SELECT Capacity FROM Win32_PhysicalMemory");
                ManagementObjectSearcher mos = new ManagementObjectSearcher(query);
                UInt64 capacity = 0;
                foreach (ManagementObject WmiObject in mos.Get())
                {
                    capacity += (UInt64)WmiObject["Capacity"];
                }

                SystemUsage currentSystemUsage = await GetUsageAsync();

                var stats = this.Lavalink.Statistics;
                var DcBotUptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                var SystemSpecs = new StringBuilder();
                SystemSpecs.Append("Host System: ```")
                    .Append("Motherboard:               ").AppendFormat($"{GetComponent("Win32_BaseBoard", "Manufacturer")} {GetComponent("Win32_BaseBoard", "Product")}").AppendLine()
                    .Append("Processor:                 ").Append($"{GetComponent("Win32_Processor", "Name")}").AppendLine()
                    .Append("RAM:                       ").Append($"{Convert.ToDouble(capacity) / 1073741824 + "GB"}").AppendLine()
                    .Append("GPU:                       ").Append($"{GetComponent("Win32_VideoController", "Name")}").AppendLine()
                    // .Append("Network:                   ").Append($"{GetComponent("Win32_NetworkAdapter", "Name")}").AppendLine()
                    .Append("```");
                var dcbot = new StringBuilder();
                dcbot.Append("Discordbot resources usage statistics: ```")
                    .Append("Discordbot uptime:         ").AppendFormat($"{DcBotUptime.Days}:{DcBotUptime.Hours}:{DcBotUptime.Minutes}:{DcBotUptime.Seconds}").AppendLine()
                    .Append("CPU Usage:                 ").Append($"{currentSystemUsage.getCPU()}%").AppendLine()
                    .Append("RAM Usage:                 ").AppendFormat("{0} MiB used", currentSystemUsage.getRAM()).AppendLine()
                    .Append("```")
                    .Append("Discordbot statistics: ```")
                    .Append("Guilds:                    ").AppendFormat($"{ctx.Client.Guilds.Count}").AppendLine()
                    .Append("Ping:                      ").AppendFormat($"{ctx.Client.Ping}").AppendLine()
                    .Append("Shards:                    ").AppendFormat($"{ctx.Client.ShardCount}").AppendLine()
                    .Append("```");
                var lavalink = new StringBuilder();
                lavalink.Append("Lavalink resources usage statistics: ```")
                    .Append("Lavalink uptime:           ").Append($"{stats.Uptime.Days}:{stats.Uptime.Hours}:{stats.Uptime.Minutes}:{stats.Uptime.Seconds}").AppendLine()
                    .Append("Lavalink Players:          ").AppendFormat("{0} active / {1} total", stats.ActivePlayers, stats.TotalPlayers).AppendLine()
                    .Append("CPU Usage:                 ").AppendFormat("{0:#,##0.0%}", stats.CpuLavalinkLoad).AppendLine()
                    .Append("RAM Usage:                 ").AppendFormat("{0} allocated / {1} used / {2} free / {3} reservable", SizeToString(stats.RamAllocated), SizeToString(stats.RamUsed), SizeToString(stats.RamFree), SizeToString(stats.RamReservable)).AppendLine()
                    .Append("Audio frames (per minute): ").AppendFormat("{0:#,##0} sent / {1:#,##0} nulled / {2:#,##0} deficit", stats.AverageSentFramesPerMinute, stats.AverageNulledFramesPerMinute, stats.AverageDeficitFramesPerMinute).AppendLine()
                    .Append("```");

                await ctx.Member.SendMessageAsync(SystemSpecs.Append(dcbot).Append(lavalink).ToString());
            }
        }
        [Command("guildlist"), Description("4Developer Command.")]
        public async Task GuildListAsync(CommandContext ctx)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))
                DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (ctx.Member.Id == 267645496020041729 || ctx.Member.Id == 352508207094038538)
            {
                ctx.Client.Logger.LogInformation(new EventId(7700, "StatsCmd"), $"{ctx.Member.DisplayName} [{ctx.Member.Id}] executed Guildlist Command!");

           

                 var dcbot = new StringBuilder();
                dcbot.Append("Guild list: ```");

                    
                int totalmember = 0;
                int i = 0;
                foreach (DiscordGuild entry in ctx.Client.Guilds.Values)
                {
                    i++; 
                    String t = String.Empty;
                    t = $"{i}. Name: {entry.Name}, Member Count: {entry.MemberCount}, Id: {entry.Id}";
                    totalmember = totalmember + entry.MemberCount;
                    dcbot.Append(t).AppendLine();
                }
                dcbot.Append($"Total Member: {totalmember}").Append("```");
                await ctx.Member.SendMessageAsync(dcbot.ToString());
            }
        }

        private static string[] Units = new[] { "", "ki", "Mi", "Gi" };
        private static string SizeToString(long l, bool showType = true)
        {
            double d = l;
            int u = 0;
            while (d >= 900 && u < Units.Length - 2)
            {
                u++;
                d /= 1024;
            }
            if(showType)
            return $"{d:#,##0.00} {Units[u]}B";
            else
            return $"{d:#.##0.00}";
        }

        private LavalinkTrack GetCurrentSongByKeywords(String keywords, CommandContext ctx)
        {

            foreach (LavalinkTrack i in TrackLoadPlaylists[ctx.Guild.Id].ToList())
            {
                if (i.Title.ToLower().Contains(keywords.ToLower()) || i.Author.ToLower().Contains(keywords.ToLower()))
                {
                    return i;
                }
            }
            return null;
        }

        public void WriteToJsonFile()
        {
            TextWriter writer = null;
            try
            {
                var contentsToWriteToFile = JsonConvert.SerializeObject(getLavaVariablesObject());
                writer = new StreamWriter($"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}/data/lcd.json", false);
                writer.Write(contentsToWriteToFile);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }
        public void SaveJsonToDatabase()
        {
            String contentsToWriteToFile = JsonConvert.SerializeObject(getLavaVariablesObject());
            db.Execute(@$"UPDATE guilds SET JSON='{contentsToWriteToFile.Replace("'", "")}' WHERE ID = 1;");
        }
        public LavaVariables ReadFromJsonFile()
        {
            TextReader reader = null;
            try
            {
                reader = new StreamReader($"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}/data/lcd.json");
                var fileContents = reader.ReadToEnd();
                return (LavaVariables)JsonConvert.DeserializeObject(fileContents);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }
        public LavaVariables ReadJsonFromDatabase()
        {
            var reader = db.Query("SELECT Json FROM guilds;");
            String fileContents = String.Empty;
            while (reader.Read())
            {
                fileContents += $"{reader.GetString("Json")};";
            }
            db.Disconnect();
            fileContents = fileContents.Replace(";", "");
            return (LavaVariables)JsonConvert.DeserializeObject(fileContents, typeof(LavaVariables));
        }
        public LavaVariables getLavaVariablesObject()
        {
            LavaVariables ContentToSave = new LavaVariables();
            ContentToSave.BotChannels = new Dictionary<ulong, ulong>();
            ContentToSave.CheckAFKStates = new Dictionary<ulong, Boolean>();
            ContentToSave.AnnounceStates = new Dictionary<ulong, Boolean>();
            ContentToSave.BotChannelBannerMessages = new Dictionary<ulong, ulong>();
            ContentToSave.BotChannelMainMessages = new Dictionary<ulong, ulong>();
            ContentToSave.FavoritesTracksLists = new Dictionary<ulong, List<LavalinkTrack>>();
            ContentToSave.GuildRoles = new Dictionary<ulong, List<Tuple<DiscordRole, role>>>();

            foreach (KeyValuePair<ulong, ulong> entry in BotChannels)
            {
                ContentToSave.BotChannels.Add(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<ulong, Boolean> entry in CheckAFKStates)
            {
                ContentToSave.CheckAFKStates.Add(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<ulong, Boolean> entry in AnnounceStates)
            {
                ContentToSave.AnnounceStates.Add(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<ulong, ulong> entry in BotChannelBannerMessages)
            {
                ContentToSave.BotChannelBannerMessages.Add(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<ulong, ulong> entry in BotChannelMainMessages)
            {
                ContentToSave.BotChannelMainMessages.Add(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<ulong, List<LavalinkTrack>> entry in FavoritesTracksLists)
            {
                ContentToSave.FavoritesTracksLists.Add(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<ulong, List<Tuple<DiscordRole, role>>> entry in GuildRoles)
            {
                ContentToSave.GuildRoles.Add(entry.Key, entry.Value);
            }
           
            return ContentToSave;
        }

        private String getThumbnail(LavalinkTrack track)
        {
            if (track.Uri.AbsoluteUri.Contains("youtube", StringComparison.OrdinalIgnoreCase) || track.Uri.AbsoluteUri.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            {

                WebRequest webRequest = WebRequest.Create($"https://img.youtube.com/vi/{track.Identifier}/maxresdefault.jpg");
                webRequest.Timeout = 1200; // miliseconds
                webRequest.Method = "HEAD";
                try
                {
                    webRequest.GetResponse();
                    return $"https://img.youtube.com/vi/{track.Identifier}/maxresdefault.jpg";
                }
                catch
                {
                    return $"https://img.youtube.com/vi/{track.Identifier}/hqdefault.jpg";
                }
            }
            else if (track.Uri.AbsoluteUri.Contains("vimeo", StringComparison.OrdinalIgnoreCase))
            {
                using (WebClient wc = new WebClient())
                {
                    var json = wc.DownloadString($"https://vimeo.com/api/oembed.json?url={track.Identifier}");
                    var data = (JObject)JsonConvert.DeserializeObject(json);
                    string thumbnailurl = data["thumbnail_url"].Value<string>();
                    return thumbnailurl;
                }
            }
            else if (track.Uri.AbsoluteUri.Contains("soundcloud", StringComparison.OrdinalIgnoreCase))
                throw new NotImplementedException();
            else if (track.Uri.AbsoluteUri.Contains("twitch", StringComparison.OrdinalIgnoreCase))
                throw new NotImplementedException();
            else
                throw new NotImplementedException();
        }
        private int getQueueCount(ulong GuildId)
        {
            if (TrackLoadPlaylists[GuildId].Count == 0)
                return 0;
            else
                return TrackLoadPlaylists[GuildId].Count - 1;
        }
        private async Task<DiscordMessage> ModifyMainMsgAsync(DiscordMessage msg, DiscordColor Color, String Title = null, String Description = null, String Footer = null, String ImageUrl = null, String VideoUrl = null)
        {
            if (msg == null)
                return null;
            var embed = msg.Embeds.First();
            if (String.IsNullOrEmpty(Title))
                Title = embed.Title;
            if (String.IsNullOrEmpty(Description))
                Description = embed.Description;
            if (String.IsNullOrEmpty(Footer))
                Footer = embed.Footer.Text;
            if (String.IsNullOrEmpty(ImageUrl))
                ImageUrl = embed.Image.Url.ToString();
            var EditedEmbed = new DiscordEmbedBuilder
            {
                Title = Title,
                Description = Description,
                Color = Color
            };
            EditedEmbed.WithFooter(Footer);
            EditedEmbed.WithImageUrl(ImageUrl);
            return await msg.ModifyAsync(GetMainMessageContent(msg), embed: EditedEmbed.Build());
        }
        private String GetMainMessageContent(DiscordMessage msg)
        {
            if (!TrackLoadPlaylists.TryGetValue(msg.Channel.GuildId, out var tracks))
                return "";
            String Content = "__**Queue list:**__";
            if(tracks.Count <= 1)
            {
                Content += "\nJoin a voice channel and queue songs by name or url in here.";
                return Content;
            } 
            else
            {
                int i = 0;
                String Time;
                List<String> tracktitles = new List<String>();
                foreach (LavalinkTrack entry in tracks)
                {
                    if (i == 0)
                    {
                        i++;
                        continue;
                    }
                    if(entry.Length.Days != 0)
                        Time = entry.Length.ToString("d'd 'h'h 'm'm 's's'");
                    else if (entry.Length.Hours != 0)
                        Time = entry.Length.ToString("h'h 'm'm 's's'");
                    else
                        Time = entry.Length.ToString("m'm 's's'");
                    tracktitles.Add($"\n{i}. {entry.Title} [{Time}]");
                    i++;
                }
                tracktitles.Reverse();
                foreach(String entry in tracktitles)
                {
                    Content += entry;
                }
                return Content;
            }

        }

        private String GetFavoriteMessage(ulong GuildId, LavalinkTrack track)
        {
            if (IsTrackFavorite(GuildId, track))
            {
                return " | Favorite";
            }
            return "";
        }
        private String GetLoopMessage(ulong GuildId)
        {
            if(Loopmodes[GuildId] == loopmode.off)
            {
                return "";
            } else if (Loopmodes[GuildId] == loopmode.loopqueue)
            {
                return " | Loopmode: Queue";
            }
            else if (Loopmodes[GuildId] == loopmode.loopsong)
            {
                return " | Loopmode: Song";
            }

            return "";
        }
        private Boolean IsTrackFavorite(ulong GuildId, LavalinkTrack track)
        {
            foreach(LavalinkTrack entry in FavoritesTracksLists[GuildId])
            {
                if(track.Identifier == entry.Identifier)
                {
                    return true;
                }
            }
            return false;
        }
        private async Task<SystemUsage> GetUsageAsync()
        {
            // Getting information about current process
            var process = Process.GetCurrentProcess();

            // Preparing variable for application instance name
            var name = string.Empty;

            foreach (var instance in new PerformanceCounterCategory("Process").GetInstanceNames())
            {
                if (instance.StartsWith(process.ProcessName))
                {
                    using (var processId = new PerformanceCounter("Process", "ID Process", instance, true))
                    {
                        if (process.Id == (int)processId.RawValue)
                        {
                            name = instance;
                            break;
                        }
                    }
                }
            }

            var cpu = new PerformanceCounter("Process", "% Processor Time", name, true);
            var ram = new PerformanceCounter("Process", "Private Bytes", name, true);

            // Getting first initial values
            cpu.NextValue();
            ram.NextValue();

            // Creating delay to get correct values of CPU usage during next query
            await Task.Delay(500);


            SystemUsage result = new SystemUsage(Math.Round(cpu.NextValue() / Environment.ProcessorCount, 2), Math.Round(ram.NextValue() / 1024 / 1024, 2));
     
            return result;
        }

        private static String GetComponent(string hwclass, string syntax)
        {
            ManagementObjectSearcher mos = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM " + hwclass);
            foreach (ManagementObject mj in mos.Get())
            {
                if (Convert.ToString(mj[syntax]) != "")
                  return Convert.ToString(mj[syntax]);
            }
            return "N/A";
        }

        private Boolean CheckHasCooldown(CommandContext ctx, ulong GuildId = 0)
        {
            if(GuildId == 0)
                GuildId = ctx.Guild.Id;
            if (Cooldown.ContainsKey(GuildId))
            {
                if (Cooldown[GuildId])
                {
                    
                    return true;
                }
                else
                {
                    Cooldown[GuildId] = true;
                    return false;
                }
            }
            else
            {
                Cooldown.Add(GuildId, true);
                return false;
            }
            
        }
        private Boolean CheckHasReactionCooldown(ulong GuildId)
        {
            if (ReactionCooldown.ContainsKey(GuildId))
            {
                if (ReactionCooldown[GuildId])
                {
                    return true;
                }
                else
                {
                    ReactionCooldown[GuildId] = true;
                    return false;
                }
            }
            else
            {
                ReactionCooldown.Add(GuildId, true);
                return false;
            }

        }

        private async void SendCooldownAsync(CommandContext ctx)
        {
            var cooldownembed = new DiscordEmbedBuilder
            {
                Title = "Cooldown",
                Description = "Please be patient.",
                Color = DiscordColor.Orange
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: cooldownembed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }
        private async void SendRestrictedChannelAsync(CommandContext ctx, ulong GuildId = 0)
        {
            if (GuildId == 0)
                GuildId = ctx.Guild.Id;
            DiscordChannel chn = null;
            try
            {
                chn = await ctx.Client.GetChannelAsync(BotChannels[GuildId]);
            } catch(Exception e)
            {
                ctx.Client.Logger.LogError(new EventId(7778, "SendRestriChnMsg"), e.ToString());
                return;
            }
            
            var RestrictedChannelEmbed = new DiscordEmbedBuilder
            {
                Description = $"This command is restricted to {chn.Mention}.",
                Color = DiscordColor.Orange
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: RestrictedChannelEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }
        private async void SendNotInSameChannelAsync(CommandContext ctx)
        {
            var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
            {
                Description = "You need to be in the same Channel with the Bot.",
                Color = DiscordColor.Orange
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        private async void SendNotConnectedAsync(CommandContext ctx)
        {
            var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
            {
                Description = "The Bot is not connected.",
                Color = DiscordColor.Orange
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }
        private async void SendNeedSetupAsync(CommandContext ctx)
        {
            var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
            {
                Description = $"You need to execute the setup before you can use this command. \n For more information use ***{ctx.Prefix}help setup***.",
                Color = DiscordColor.Orange
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
         //   DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }
        private async void SendNotInAVoiceChannelAsync(CommandContext ctx)
        {
            var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
            {
                Description = "You are not in a voicechannel or have specified a channel.",
                Color = DiscordColor.Orange
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }
        private async void SendQueueIsEmptyAsync(CommandContext ctx)
        {
            var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
            {
                Description = $"The queue is empty.",
                Color = DiscordColor.Orange
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }
        private async void SendNoPermssionAsync(CommandContext ctx)
        {
            var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
            {
                Description = $"You are not allowed to execute the command!",
                Color = DiscordColor.Red
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
            DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }
        private bool CheckHasPermission(CommandContext ctx, role NeededRole, DiscordMember member = null, ulong GuildId = 0)
        {
            if(GuildId == 0)
                GuildId = ctx.Guild.Id;
            if (member == null)
                member = ctx.Member;
            if (member.IsOwner || role.everyone == NeededRole || member.PermissionsIn(ctx.Channel) == Permissions.Administrator)
            {
                return false;
            }
            if (GuildRoles.ContainsKey(GuildId))    
            foreach (Tuple<DiscordRole, role> entry in GuildRoles[GuildId])
            {
                if(entry.Item2 == NeededRole || entry.Item2 == role.admin)
                {
                    if (member.Roles.Contains(entry.Item1))
                    {
                        return false;
                    }
                }
            }
            SendNoPermssionAsync(ctx);
            return true;
        }

    }

}
