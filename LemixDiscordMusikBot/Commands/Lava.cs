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
using System.Threading;
using LemixDiscordMusikBot.Classes;
using LemixDiscordMusikBot.Classes.Database;

namespace LemixDiscordMusikBot.Commands
{

    public class Lava : BaseCommandModule
    {
        private LavalinkNodeConnection Lavalink { get; set; }
        private Dictionary<ulong, LavalinkGuildConnection> VoiceConnections = new Dictionary<ulong, LavalinkGuildConnection>();
        private Dictionary<LavalinkGuildConnection, int> Volumes = new Dictionary<LavalinkGuildConnection, int>();
        private Dictionary<ulong, DateTimeOffset> AFKTimeOffsets = new Dictionary<ulong, DateTimeOffset>();
        private Dictionary<ulong, Boolean> AnnounceStates = new Dictionary<ulong, Boolean>();
        private Dictionary<ulong, Boolean> IsPausedStates = new Dictionary<ulong, Boolean>();
        private Dictionary<ulong, DeleteMessage> DeletePool = new Dictionary<ulong, DeleteMessage>();
        private Dictionary<ulong, loopmode> Loopmodes = new Dictionary<ulong, loopmode>();
        private Dictionary<ulong, Boolean> Cooldown = new Dictionary<ulong, Boolean>();
        private List<ulong> CooldownAlreadyOnCooldown = new List<ulong>();
        private Dictionary<ulong, Boolean> ReactionCooldown = new Dictionary<ulong, Boolean>();
        private Dictionary<ulong, Boolean> VoteSkip = new Dictionary<ulong, Boolean>();
        private Dictionary<DateTime, SystemUsageItem> SystemUsageLog = new Dictionary<DateTime, SystemUsageItem>();



        private Config configJson;
        private ConnectionEndpoint conEndPoint;
        private DBConnection db;
        int CommandCooldown = 1000; // ms
        int LastHourStatistic;

        public Dictionary<ulong, List<LavalinkTrack>> TrackLoadPlaylists = new Dictionary<ulong, List<LavalinkTrack>>();
        public Dictionary<ulong, int> CurrentSong = new Dictionary<ulong, int>();

        public enum loopmode
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

            System.Timers.Timer AFKtimer = new System.Timers.Timer();
            AFKtimer.Interval = 10000;
            AFKtimer.Elapsed += CheckIsAFK;
            AFKtimer.Start();
        }

        private void OnProgramExit(object sender, EventArgs e)
        {
            //SaveJsonToDatabase();
            Environment.Exit(0);
        }

        private void DeleteAllMsgInPool(object sender, ElapsedEventArgs e, CommandContext ctx)
        {
            List<DeleteChannelMessages> ToDeleteMessages = new List<DeleteChannelMessages>();
            if (DeletePool.Count != 0)
            {
                foreach (DeleteMessage entry in DeletePool.Values.ToList())
                {
                    if (entry == null)
                    {
                        DeletePool.Values.ToList().Remove(entry);
                        continue;
                    }
                    if (!(entry.DateTime.AddSeconds(7) <= DateTime.Now))
                        continue;
                    if (ToDeleteMessages.Count == 0)
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

        [Command("init"), Description("4")]
        public async Task GetNodeConnection(CommandContext ctx)
        {


            try
            {
                if (ctx.User != ctx.Client.CurrentUser)
                    return;
                //Init Database
                db = new DBConnection(ctx.Client.Logger, configJson);
                Variables.WaitForLavalinkConnect.TryGetValue(ctx.Client.ShardId, out EventWaitHandle handle);
                ctx.Client.Logger.LogInformation(new EventId(7777, "InitCommand"), $"Shard {ctx.Client.ShardId} wait for Lavalink Node Connection");
                handle.WaitOne();
                handle.Reset();
                this.Lavalink = ctx.Client.GetLavalink().GetNodeConnection(conEndPoint);
                if (this.Lavalink == null)
                {
                    ctx.Client.Logger.LogError(new EventId(7777, "InitCommand"), "Lavalink Node is NULL waiting 10 seconds for retry");
                    await Task.Delay(10000);
                    this.Lavalink = ctx.Client.GetLavalink().GetNodeConnection(conEndPoint);
                    if (this.Lavalink == null)
                    {

                        ctx.Client.Logger.LogError(new EventId(7777, "InitCommand"), "Lavalink Node is NULL cancel program");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Press any Key to close the Window");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                    else
                    {
                        this.Lavalink.PlaybackFinished += async (s, e) => { await VoiceConnection_PlaybackFinished(s, e); }; ;
                        this.Lavalink.Disconnected += async (s, e) => { await Lavalink_Disconnected(s, e); };
                        this.Lavalink.LavalinkSocketErrored += async (s, e) => { await Lavalink_LavalinkSocketErrored(s, e); };
                        ctx.Client.VoiceStateUpdated += async (s, e) => { await VoiceStateUpdate(s, e); }; ;
                        ctx.Client.GuildDeleted += async (s, e) => { await OnGuildDeleted(s, e); }; ;
                        ctx.Client.GuildMemberRemoved += async (s, e) => { await OnMemberRemoved(s, e); }; ;
                        ctx.CommandsNext.CommandExecuted += async (s, e) => { await OnCommandExecuted(s, e); }; ;
                        ctx.CommandsNext.CommandErrored += async (s, e) => { await OnCommandErrored(s, e); }; ;
                        ctx.Client.MessageReactionAdded += async (s, e) => { await OnMessageReactionAdded(s, e); }; ;
                        ctx.Client.MessageCreated += (s, e) => OnMessageCreated(s, e);


                    }
                }
                else
                {
                    this.Lavalink.PlaybackFinished += async (s, e) => { await VoiceConnection_PlaybackFinished(s, e); }; ;
                    this.Lavalink.Disconnected += async (s, e) => { await Lavalink_Disconnected(s, e); };
                    this.Lavalink.LavalinkSocketErrored += async (s, e) => { await Lavalink_LavalinkSocketErrored(s, e); };
                    ctx.Client.VoiceStateUpdated += async (s, e) => { await VoiceStateUpdate(s, e); }; ;
                    ctx.Client.GuildDeleted += async (s, e) => { await OnGuildDeleted(s, e); }; ;
                    ctx.Client.GuildMemberRemoved += async (s, e) => { await OnMemberRemoved(s, e); }; ;
                    ctx.CommandsNext.CommandExecuted += async (s, e) => { await OnCommandExecuted(s, e); }; ;
                    ctx.CommandsNext.CommandErrored += async (s, e) => { await OnCommandErrored(s, e); }; ;
                    ctx.Client.MessageReactionAdded += async (s, e) => { await OnMessageReactionAdded(s, e); }; ;
                    ctx.Client.MessageCreated += (s, e) => OnMessageCreated(s, e);
                }

                System.Timers.Timer PoolDelete = new System.Timers.Timer(5000);
                PoolDelete.Elapsed += (sender, e) => DeleteAllMsgInPool(sender, e, ctx);
                PoolDelete.Start();

                foreach (ulong entry in Data.GetAllGuildsIds())
                {
                    await Task.Factory.StartNew(() => BotChannel(ctx, entry));
                }
                var StatisticTimer = new System.Timers.Timer(1000);
                LastHourStatistic = DateTime.Now.Hour;
                StatisticTimer.Elapsed += (sender, e) => Statistic(sender, e, ctx, LastHourStatistic);
                StatisticTimer.Start();
                var SystemUsageTimer = new System.Timers.Timer(1000);
                SystemUsageTimer.Elapsed += (sender, e) => SystemUsageGetterAsync(sender, e, ctx.Client);
                SystemUsageTimer.Start();
                //ctx.CommandsNext.UnregisterCommands(ctx.Command);
                ctx.Client.Logger.LogInformation(new EventId(7777, "InitCommand"), $"Shard {ctx.Client.ShardId} successfully started!");

            }
            catch (Exception e)
            {
                ctx.Client.Logger.LogCritical(new EventId(7777, "InitCommand"), $"Error in Init Command: {e.Message}:\n {e}");
            }

        }

        private Task OnMemberRemoved(DiscordClient s, GuildMemberRemoveEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task Lavalink_LavalinkSocketErrored(LavalinkNodeConnection s, SocketErrorEventArgs e)
        {
            s.Discord.Logger.LogCritical(e.Exception.ToString());
            return Task.CompletedTask;
        }

        private async void SystemUsageGetterAsync(object sender, ElapsedEventArgs e, DiscordClient client)
        {
            SystemUsage currentSystemUsage = await GetUsageAsync();
            var LavaStats = this.Lavalink.Statistics;

            DateTime LogDate = DateTime.Now;
            int Ping = client.Ping;
            double DiscordBotCPU = currentSystemUsage.getCPU();
            double DiscordBotRAM = currentSystemUsage.getRAM();
            double LavalinkCPU = LavaStats.CpuLavalinkLoad;
            long LavalinkRAM = LavaStats.RamUsed;
            try
            {
                lock (SystemUsageLog)
                {
                    if (!SystemUsageLog.ContainsKey(LogDate))
                        SystemUsageLog.Add(LogDate, new SystemUsageItem(Ping, DiscordBotCPU, DiscordBotRAM, LavalinkCPU, LavalinkRAM));
                }

            }
            catch (Exception ex)
            {
                client.Logger.LogError(ex.ToString());
                await Task.Delay(10000);
            }

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
                List<KeyValuePair<DateTime, SystemUsageItem>> TempSystemUsageLog = null;
                lock (SystemUsageLog)
                {

                    TempSystemUsageLog = SystemUsageLog.ToList();
                    SystemUsageLog.Clear();
                }
                List<int> PingItems = new List<int>();
                List<double> DiscordBotCPUItems = new List<double>();
                List<double> DiscordBotRAMItems = new List<double>();
                List<double> LavalinkCPUItems = new List<double>();
                List<long> LavalinkRAMItems = new List<long>();
                foreach (KeyValuePair<DateTime, SystemUsageItem> entry in TempSystemUsageLog.ToList())
                {
                    if ((LogDate - TimeSpan.FromHours(1)) < entry.Key)
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

                db.Execute(@$"INSERT INTO dbot_statistic_log(log_date, guilds_count, ping, active_shards, dbot_cpu, dbot_ram, dbot_uptime, lavalink_cpu, lavalink_ram, lavalink_uptime, lavalink_players_total, lavalink_players_active) VALUES ('{LogDate.ToString("yyyy-MM-dd HH:mm:ss")}',{GuildCount},{PingAverage.ToString().Replace(",", ".")},{ActiveShards},{DiscordBotCPUAverage.ToString().Replace(",", ".")},{DiscordBotRAMItemsAverage.ToString().Replace(",", ".")},'{String.Format("{0:00}:{1:00}:{2:00}", Math.Floor(DiscordBotUptime.TotalHours), DiscordBotUptime.Minutes, DiscordBotUptime.Seconds)}',{LavalinkCPUItemsAverage.ToString().Replace(",", ".")},{SizeToString(Convert.ToInt64(LavalinkRAMItemsAverage), false).ToString().Replace(",", ".")},'{String.Format("{0:00}:{1:00}:{2:00}", Math.Floor(LavalinkUptime.TotalHours), LavalinkUptime.Minutes, LavalinkUptime.Seconds)}',{LavalinkPlayersTotal},{LavalinkPlayersActive});");
                PingItems.Clear();
                DiscordBotCPUItems.Clear();
                DiscordBotRAMItems.Clear();
                LavalinkCPUItems.Clear();
                LavalinkRAMItems.Clear();

            }
        }

        //Add all messages in Botchannel for delete
        private Task OnMessageCreated(DiscordClient s, MessageCreateEventArgs e)
        {
            Task.Run(async () =>
            {
                await Task.Delay(1500); // An event handler caused the invocation of an asynchronous event to time out. // Task.run maybe fix it but code is not needed
                if (e.Guild == null)
                    return;
                if (!Data.CheckBotChannelExists(e.Guild.Id))
                    return;
                if (e.Message.Id == Data.GetBotChannelMainMessageId(e.Guild.Id) || e.Message.Id == Data.GetBotChannelBannerMessageId(e.Guild.Id) || e.Channel.Id != Data.GetBotChannelId(e.Guild.Id))
                    return;
                if (e.Message.Embeds.Count > 0)
                    if (e.Message.Embeds.First().Footer != null)
                        if (e.Message.Embeds.First().Footer?.Text == "After 30 seconds this is canceled" && e.Author == s.CurrentUser)
                            return;
                if (DeletePool != null)
                    if (!DeletePool.ContainsKey(e.Message.Id))
                        DeletePool.Add(e.Message.Id, new DeleteMessage(e.Channel, e.Message));
            });
            return Task.CompletedTask;
        }

        private async Task OnMessageReactionAdded(DiscordClient sender, MessageReactionAddEventArgs e)
        {
            if (!Data.CheckBotChannelMainMessageExists(e.Guild.Id))
                return;
            if (e.Message.Id == Data.GetBotChannelMainMessageId(e.Guild.Id) && e.User.Id != sender.CurrentUser.Id)
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
            var ArgumentCommandEmbed = new DiscordEmbedBuilder
            {
                Title = $"Could not find a suitable arguments for the command!",
                Color = DiscordColor.Gray
            };
            var UnkownEmbed = new DiscordEmbedBuilder
            {
                Title = $"Unkown Error!",
                Description = $"If this problem persists please report it to support\n Use: {e.Context.Prefix}support",
                Color = DiscordColor.Red
            };
            if (!(e.Exception is UnauthorizedException))
            {
                if (e.Exception is DSharpPlus.CommandsNext.Exceptions.CommandNotFoundException)
                {

                }
                else if (e.Exception is ArgumentException)
                {
                    await e.Context.Channel.SendMessageAsync(embed: ArgumentCommandEmbed);
                }
                else
                {
                    s.Client.Logger.LogError(new EventId(7776, "UnkownError"), $"Unknown command error has occurred! (Command: {e.Context.Message.Content} Exception: {e.Exception})");
                    await e.Context.Channel.SendMessageAsync(embed: UnkownEmbed);
                }
            }

            if (e.Context.Guild == null)
                return Task.CompletedTask;
            if (!Cooldown.ContainsKey(e.Context.Guild.Id))
                return Task.CompletedTask;
            if (Cooldown.ContainsKey(e.Context.Guild.Id))
                Cooldown[e.Context.Guild.Id] = false;
            return Task.CompletedTask;
        }

        private async Task<Task> OnCommandExecuted(CommandsNextExtension s, CommandExecutionEventArgs e)
        {
            try
            {

                if (e != null)
                    if (e.Context.Message.Channel != null)
                        if (e?.Context?.Guild != null)
                        {
                            if (!Cooldown.ContainsKey(e.Context.Guild.Id)) { return Task.CompletedTask; }
                            if (CooldownAlreadyOnCooldown.Contains(e.Context.Guild.Id)) { return Task.CompletedTask; }
                            CooldownAlreadyOnCooldown.Add(e.Context.Guild.Id);
                            await Task.Delay(CommandCooldown);
                            CooldownAlreadyOnCooldown.Remove(e.Context.Guild.Id);
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
                Data.DeleteGuild(e.Guild.Id);
                if(VoiceConnections.ContainsKey(e.Guild.Id))
                    if (Volumes.ContainsKey(VoiceConnections[e.Guild.Id]))
                        Volumes.Remove(VoiceConnections[e.Guild.Id]);
                if (VoiceConnections.ContainsKey(e.Guild.Id))
                    VoiceConnections.Remove(e.Guild.Id);
                if (AFKTimeOffsets.ContainsKey(e.Guild.Id))
                    AFKTimeOffsets.Remove(e.Guild.Id);
                if (IsPausedStates.ContainsKey(e.Guild.Id))
                    IsPausedStates.Remove(e.Guild.Id);
                if (TrackLoadPlaylists.ContainsKey(e.Guild.Id))
                    TrackLoadPlaylists.Remove(e.Guild.Id);
                if (Loopmodes.ContainsKey(e.Guild.Id))
                    Loopmodes.Remove(e.Guild.Id);
                if (AnnounceStates.ContainsKey(e.Guild.Id))
                    AnnounceStates.Remove(e.Guild.Id);
                if (Cooldown.ContainsKey(e.Guild.Id))
                    Cooldown.Remove(e.Guild.Id);
                if (CooldownAlreadyOnCooldown.Contains(e.Guild.Id))
                    CooldownAlreadyOnCooldown.Remove(e.Guild.Id);
                if (ReactionCooldown.ContainsKey(e.Guild.Id))
                    ReactionCooldown.Remove(e.Guild.Id);
                if (VoteSkip.ContainsKey(e.Guild.Id))
                    VoteSkip.Remove(e.Guild.Id);
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
            Console.WriteLine("ddd");
            if (e.User.Id == s.CurrentUser.Id)
            {
                if (!VoiceConnections.TryGetValue(e.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                    return;
                if (e.Before.Channel?.Id != VoiceConnection.Channel?.Id && e.Before.Channel?.Id != null)
                {
                    try
                    {
                        var chn = e.Guild.GetChannel(Data.GetBotChannelId(e.Guild.Id));
                        var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(e.Guild.Id));
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
                        try
                        {
                            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {prefix}"); // PREFIX

                        }catch(UnauthorizedException) {  }
                        if (VoiceConnection.IsConnected)
                            await VoiceConnection.DisconnectAsync();
                        VoiceConnections.Remove(e.Guild.Id);
                        Volumes.Remove(VoiceConnection);
                        AFKTimeOffsets.Remove(e.Guild.Id);
                        IsPausedStates.Remove(e.Guild.Id);
                        TrackLoadPlaylists[e.Guild.Id].Clear();
                        Loopmodes.Remove(e.Guild.Id);
                    }
                    catch (Exception e1)
                    {
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
                if (!Data.CheckBotChannelExists(GuildId))
                    return;
                ulong chnid = Data.GetBotChannelId(GuildId);
                DiscordChannel chn = null;

                try { chn = await ctx.Client.GetChannelAsync(chnid); } catch { return; }
                var interactivity = ctx.Client.GetInteractivity();
                DiscordMessage BannerMsg;
                DiscordMessage MainMsg;
                //   FileStream fs;

                DiscordEmoji PlayPause = DiscordEmoji.FromName(ctx.Client, ":play_pause:");
                DiscordEmoji Stop = DiscordEmoji.FromName(ctx.Client, ":stop_button:");
                DiscordEmoji NextTrack = DiscordEmoji.FromName(ctx.Client, ":track_next:");
                DiscordEmoji Loop = DiscordEmoji.FromName(ctx.Client, ":arrows_counterclockwise:");
                DiscordEmoji TwistedArrows = DiscordEmoji.FromName(ctx.Client, ":twisted_rightwards_arrows:");
                DiscordEmoji Star = DiscordEmoji.FromName(ctx.Client, ":star:");
                DiscordEmoji Crossed = DiscordEmoji.FromName(ctx.Client, ":x:");
                DiscordEmoji l_char = DiscordEmoji.FromName(ctx.Client, ":regional_indicator_l:");

                if (!Data.CheckBotChannelBannerMessageExists(GuildId))
                {
                    // Need final rework maybe integrated
                    //   fs = File.OpenRead(System.Environment.CurrentDirectory + @"/pics/banner.png");
                    BannerMsg = await chn.SendMessageAsync(configJson.BannerPicture);
                    Data.SetBotChannelBannerMessageId(GuildId, BannerMsg.Id);
                }
                else
                {
                    if (configJson.BotChannelRebuild)
                    {
                        try
                        {
                            BannerMsg = await chn.GetMessageAsync(Data.GetBotChannelBannerMessageId(GuildId));
                            Data.SetBotChannelBannerMessageId(GuildId, 0);
                            DeletePool.Add(BannerMsg.Id, new DeleteMessage(BannerMsg.Channel, BannerMsg));
                            BannerMsg = await chn.SendMessageAsync(configJson.BannerPicture);
                            Data.SetBotChannelBannerMessageId(GuildId, BannerMsg.Id);
                        }
                        catch (Exception e)
                        {
                            ctx.Client.Logger.LogError(GuildId + " Bot Channel cannot rebuild! " + e.ToString());
                            return;
                        }
                    }
                    else
                    {
                        try
                        {
                            BannerMsg = await chn.GetMessageAsync(Data.GetBotChannelBannerMessageId(GuildId));
                        }
                        catch { return; }

                    }


                }

                if (!Data.CheckBotChannelMainMessageExists(GuildId))
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
                    Data.SetBotChannelMainMessageId(GuildId, MainMsg.Id);
                    await MainMsg.CreateReactionAsync(PlayPause);
                    await MainMsg.CreateReactionAsync(Stop);
                    await MainMsg.CreateReactionAsync(NextTrack);
                    await MainMsg.CreateReactionAsync(Loop);
                    await MainMsg.CreateReactionAsync(TwistedArrows);
                    await MainMsg.CreateReactionAsync(Star);
                    await MainMsg.CreateReactionAsync(Crossed);
                    await MainMsg.CreateReactionAsync(l_char);

                }
                else
                {
                    if (configJson.BotChannelRebuild)
                    {
                        try
                        {
                            MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(GuildId));
                            Data.SetBotChannelMainMessageId(GuildId, 0);
                            DeletePool.Add(MainMsg.Id, new DeleteMessage(MainMsg.Channel, MainMsg));
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
                            Data.SetBotChannelMainMessageId(GuildId, MainMsg.Id);
                        }
                        catch (Exception e)
                        {
                            ctx.Client.Logger.LogError(GuildId + " Bot Channel cannot rebuild! " + e.ToString());
                            return;
                        }
                    }
                    else
                    {
                        try
                        {
                            MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(GuildId));
                        }
                        catch { return; }

                    }

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
                    if (!TrackLoadPlaylists.ContainsKey(GuildId))
                    {
                        TrackLoadPlaylists.Add(GuildId, new List<LavalinkTrack>());
                    }
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
                                //SendCooldownAsync(ctx);
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
                        if (!Data.CheckFavoritesTracksListsExists(GuildId))
                            continue;
                        if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                            continue;

                        Boolean abort = false;
                        LavalinkTrack track = TrackLoadPlaylists[GuildId].First();
                        foreach (LavalinkTrack entry in Data.GetFavoritesTracksLists(GuildId))
                        {
                            if (entry.Identifier == track.Identifier)
                            {
                                var errmsg = await chn.SendMessageAsync("Already favorite.");
                                abort = true;
                            }
                        }
                        if (abort)
                            continue;

                        if (Data.AddFavoritesTracksLists(GuildId, track))
                        {
                            await chn.SendMessageAsync("Successfully added.");
                            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(GuildId)}{GetFavoriteMessage(GuildId, track)}");
                        }
                        else
                        {
                            await chn.SendMessageAsync("Something went wrong.");
                        }

                    }
                    else if (result.Result.Emoji == Crossed)
                    {

                        if (TrackLoadPlaylists[GuildId].Count == 0)
                            continue;
                        if (!Data.CheckFavoritesTracksListsExists(GuildId))
                            continue;

                        if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                            continue;
                        if (await CheckIsBotChannelAndMessagesExits(ctx, GuildId))
                            continue;
                        Boolean abort = false;
                        LavalinkTrack track = TrackLoadPlaylists[GuildId].First();
                        foreach (LavalinkTrack entry in Data.GetFavoritesTracksLists(GuildId).ToList())
                        {
                            if (entry.Identifier == track.Identifier)
                            {
                                Data.RemoveFavoritesTracksLists(GuildId, entry);


                                if (TrackLoadPlaylists[GuildId].Count <= 1)
                                {
                                    result.Result.Guild.Members.TryGetValue(result.Result.User.Id, out DiscordMember member);


                                    if (member.VoiceState?.Channel != VoiceConnection.Channel || member.VoiceState?.Channel == null)
                                    {
                                        continue;
                                    }
                                    TrackLoadPlaylists[GuildId].Clear();
                                    await VoiceConnection.StopAsync().ConfigureAwait(false);
                                    await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {ctx.Prefix}"); // PREFIX
                                }
                                else
                                {
                                    await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetFavoriteMessage(GuildId, track)}");
                                }
                                var msg = await chn.SendMessageAsync("Successfully removed.");
                                abort = true;
                            }
                        }
                        if (abort)
                            continue;
                        var errmsg = await chn.SendMessageAsync("Not at favorites.");
                        // DeletePool.Add(errmsg.Id, new DeleteMessage(chn, errmsg));



                    }
                    else if (result.Result.Emoji == l_char)
                    {
                        DiscordChannel tchn = null;
                        if (!Data.CheckFavoritesTracksListsExists(GuildId))
                            continue;
                        if (Data.GetFavoritesTracksLists(GuildId).Count == 0)
                            continue;

                        var guild = ctx.Client.GetGuildAsync(GuildId);
                        if (result.Result == null)
                        {
                            var NoVoiceConnectionEmbed = new DiscordEmbedBuilder
                            {
                                Description = "Guild not found.",
                                Color = DiscordColor.Red
                            };
                            DiscordMessage msg1 = await chn.SendMessageAsync(embed: NoVoiceConnectionEmbed);
                            // DeletePool.Add(msg1.Id, new DeleteMessage(chn, msg1));
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
                            // DeletePool.Add(msg1.Id, new DeleteMessage(chn, msg1));
                            continue;
                        }
                        else
                        {
                            await JoinAsync(ctx, tchn, GuildId);
                        }
                        if (!VoiceConnections.TryGetValue(GuildId, out LavalinkGuildConnection VoiceConnection))
                        {
                            //var NoVoiceConnectionEmbed = new DiscordEmbedBuilder
                            //{
                            //    Description = "The bot must already be in a voice channel.",
                            //    Color = DiscordColor.Orange
                            //};
                            //await chn.SendMessageAsync(embed: NoVoiceConnectionEmbed);
                            continue;
                        }
                        TrackLoadPlaylists[GuildId].Clear();
                        LavalinkLoadResult trackload;
                        foreach (LavalinkTrack entry in Data.GetFavoritesTracksLists(GuildId).ToList())
                        {

                            try
                            {
                                trackload = await Lavalink.Rest.GetTracksAsync(entry.Uri);
                                TrackLoadPlaylists[GuildId].Add(trackload.Tracks.First());
                            }
                            catch
                            {
                                var errmsg = await ctx.Channel.SendMessageAsync($"Song {entry?.Title} cannot be found and will be removed!");
                                Data.RemoveFavoritesTracksLists(GuildId, entry);
                            }

                        }
                        var track = TrackLoadPlaylists[GuildId].First();
                        await VoiceConnection.PlayAsync(track);
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(GuildId)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetFavoriteMessage(GuildId, track)}");
                    }
                }


            }
            catch (UnauthorizedException)
            {
                ctx.Client.Logger.LogInformation(new EventId(7780, "Botchannel"), $"Unauthorized Botchannel {GuildId}");
            }
            catch (Exception e)
            {
                ctx.Client.Logger.LogCritical(new EventId(7780, "Botchannel"), e.ToString());

            }
            // restart task if something errored
            await Task.Delay(60000);
            await BotChannel(ctx, GuildId);
        }
        private async Task VoiceConnection_PlaybackFinished(LavalinkGuildConnection s, TrackFinishEventArgs e)
        {
            if (e.Reason == TrackEndReason.Stopped)
                return;
            if (e.Reason != TrackEndReason.Finished)
                return;
            LavalinkGuildConnection VoiceConnection;
            if (e.Player?.Guild == null)
                return;
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
                var chn = e.Player.Guild.GetChannel(Data.GetBotChannelId(e.Player.Guild.Id));
                var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(e.Player.Guild.Id));
                var track = TrackLoadPlaylists[e.Player.Guild.Id].GetItemByIndex(CurrentSong[e.Player.Guild.Id]);

                await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(e.Player.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(e.Player.Guild.Id)}{GetFavoriteMessage(e.Player.Guild.Id, track)}");

                await VoiceConnection.PlayAsync(track);
            }
            else
            {
                var chn = e.Player.Guild.GetChannel(Data.GetBotChannelId(e.Player.Guild.Id));
                var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(e.Player.Guild.Id));
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
                if (AFKTimeOffsets.TryGetValue(VoicePair.Key, out dto))
                {
                    if (VoiceConnection != null)
                    {
                        if (Data.GetAfkState(VoicePair.Key) == false)
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

        [Command("setup"), Description("3Setup the text channel.\nAdministration rights are *required* for the user. \nThe bot *needs* rights to manage the channels.")]
        [RequireBotPermissions(Permissions.ManageChannels)]
        public async Task SetupAsync(CommandContext ctx)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //     // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
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
                    checkchn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
                }
                catch { }

                if (checkchn is null)
                {
                    if (Data.CheckBotChannelExists(ctx.Guild.Id))
                        Data.SetBotChannelId(ctx.Guild.Id, 0);
                }
                if (Data.CheckBotChannelExists(ctx.Guild.Id))
                {
                    var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
                    var MessagesRestored = new DiscordEmbedBuilder
                    {
                        Title = $"Botchannel messages restored!",
                        Color = DiscordColor.DarkGreen
                    };
                    MessagesRestored.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
                    MessagesRestored.AddField("Channel:", $"{chn.Mention}");


                    if (!Data.CheckBotChannelMainMessageExists(ctx.Guild.Id) || !Data.CheckBotChannelBannerMessageExists(ctx.Guild.Id))
                    {
                        if (VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                        {
                            await LeaveAsync(ctx);
                        }
                        await Task.Factory.StartNew(() => BotChannel(ctx, ctx.Guild.Id));
                        try
                        {
                            await ctx.Channel.SendMessageAsync(embed: MessagesRestored);
                        }
                        catch (UnauthorizedException)
                        {
                            var embed = new DiscordEmbedBuilder
                            {
                                Title = "Missing Permission!",
                                Description = "The bot does not have the necessary rights to execute the command!",
                                Color = DiscordColor.Red

                            };
                            embed.AddField("Missing permission", "Send Messages");
                            embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                            try
                            {
                                var dmchannel = await ctx.Member.CreateDmChannelAsync();
                                await dmchannel.SendMessageAsync(embed: embed);
                            }
                            catch { }
                        }

                        return;
                    }

                    try
                    {
                        var MainMsg = chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id)).Result;
                        var BannerMsg = chn.GetMessageAsync(Data.GetBotChannelBannerMessageId(ctx.Guild.Id)).Result;
                    }
                    catch
                    {
                        try
                        {
                            if (VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection1))
                            {
                                await LeaveAsync(ctx);
                            }
                            await chn.DeleteAsync();
                            Data.SetBotChannelId(ctx.Guild.Id, 0);
                            Data.SetBotChannelMainMessageId(ctx.Guild.Id, 0);
                            Data.SetBotChannelBannerMessageId(ctx.Guild.Id, 0);
                            if (TrackLoadPlaylists.ContainsKey(ctx.Guild.Id))
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
                        }
                        catch (UnauthorizedException)
                        {
                            var embed = new DiscordEmbedBuilder
                            {
                                Title = "Missing Permission!",
                                Description = "The bot does not have the necessary rights to execute the command!",
                                Color = DiscordColor.Red

                            };
                            embed.AddField("Missing permission", "Send Messages");
                            embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                            try
                            {
                                var dmchannel = await ctx.Member.CreateDmChannelAsync();
                                await dmchannel.SendMessageAsync(embed: embed);
                            }
                            catch { }
                        }
                        catch { }

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
                    try
                    {
                        await ctx.Channel.SendMessageAsync(embed: SetupAlreadyDone);
                    }
                    catch (UnauthorizedException)
                    {
                        var embed = new DiscordEmbedBuilder
                        {
                            Title = "Missing Permission!",
                            Description = "The bot does not have the necessary rights to execute the command!",
                            Color = DiscordColor.Red

                        };
                        embed.AddField("Missing permission", "Send Messages");
                        embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                        try
                        {
                            var dmchannel = await ctx.Member.CreateDmChannelAsync();
                            await dmchannel.SendMessageAsync(embed: embed);
                        }
                        catch { }
                    }

                    return;
                }
                if (VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection2))
                {
                    await LeaveAsync(ctx);
                }
                await CreateBotChannelAsync(ctx);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async Task<DiscordChannel> CreateBotChannelAsync(CommandContext ctx, Boolean Restore = false)
        {
            try
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
                :star: Add the current song to the server playlist.
                :x: Remove the current song from the server playlist.
                :regional_indicator_l: Load the server playlist.");
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
                        if (Data.CheckBotChannelExists(ctx.Guild.Id))
                            await ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id)).DeleteAsync();
                    }
                    catch { }
                    if (Data.CheckBotChannelExists(ctx.Guild.Id))
                        Data.SetBotChannelId(ctx.Guild.Id, 0);
                    if (Data.CheckBotChannelMainMessageExists(ctx.Guild.Id))
                        Data.SetBotChannelMainMessageId(ctx.Guild.Id, 0);
                    if (Data.CheckBotChannelBannerMessageExists(ctx.Guild.Id))
                        Data.SetBotChannelBannerMessageId(ctx.Guild.Id, 0);
                    var newchn = newchnresult.Result;
                    var SetupDone = new DiscordEmbedBuilder
                    {
                        Title = $"Setup done!",
                        Color = DiscordColor.DarkGreen
                    };
                    Data.SetBotChannelId(ctx.Guild.Id, newchn.Id);
                    SetupDone.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
                    SetupDone.AddField("Channel:", $"{newchn.Mention} (unnameable)");
                    if (Restore != true)
                        await ctx.Channel.SendMessageAsync(embed: SetupDone);
                    await Task.Factory.StartNew(() => BotChannel(ctx, ctx.Guild.Id));
                    return newchn;
                }
            }
            catch (UnauthorizedException)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Missing Permission!",
                    Description = "The bot does not have the necessary rights to execute the command!",
                    Color = DiscordColor.Red

                };
                embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                try
                {
                    var dmchannel = await ctx.Member.CreateDmChannelAsync();
                    await dmchannel.SendMessageAsync(embed: embed);
                }
                catch { }
            }
            return null;
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
                if (!Data.CheckBotChannelExists(GuildId) || !Data.CheckBotChannelMainMessageExists(GuildId) || !Data.CheckBotChannelBannerMessageExists(GuildId))
                {
                    await ctx.Channel.SendMessageAsync(embed: BotChannelsOrMessagesAreMissing);
                    return true;
                }
                var guild = ctx.Client.GetGuildAsync(GuildId);
                if (guild.Result == null)
                {
                    await ctx.Channel.SendMessageAsync(embed: BotChannelsOrMessagesAreMissing);
                    return true;
                }
                var chn = guild.Result.GetChannel(Data.GetBotChannelId(GuildId));
                if (chn == null)
                {
                    await ctx.Channel.SendMessageAsync(embed: BotChannelsOrMessagesAreMissing);
                    return true;
                }
                try
                {
                    var MainMsg = chn.GetMessageAsync(Data.GetBotChannelMainMessageId(GuildId)).Result;
                    var BannerMsg = chn.GetMessageAsync(Data.GetBotChannelBannerMessageId(GuildId)).Result;
                }
                catch
                {
                    var messages = chn.GetMessagesAsync(5);
                    foreach (DiscordMessage msg in messages.Result)
                    {
                        DeletePool.Add(msg.Id, new DeleteMessage(chn, msg));

                    }
                    Data.SetBotChannelMainMessageId(GuildId, 0);
                    Data.SetBotChannelBannerMessageId(GuildId, 0);
                    await ctx.Channel.SendMessageAsync(embed: BotChannelsOrMessagesAreMissing);
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                ctx.Client.Logger.LogError(new EventId(7778, "CheckIsBotChannel"), e.ToString());
                return true;
            }

        }

        [RequireBotPermissions(Permissions.ManageChannels | Permissions.AccessChannels), RequirePermissions(Permissions.AccessChannels)]
        [Command("join"), Description("1Joins a voice channel."), Aliases("connect")]
        public async Task JoinAsync(CommandContext ctx, DiscordChannel Channelname = null, ulong GuildId = 0)
        {

            if (GuildId == 0)
            {
                GuildId = ctx.Guild.Id;

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
                if (Data.GetBotChannelId(GuildId) != ctx.Channel.Id)
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


            if (!Data.CheckBotChannelExists(GuildId))
            {
                SendNeedSetupAsync(ctx);
                return;
            }


            if (VoiceConnections.TryGetValue(GuildId, out VoiceConnection))
            {
                if (vc != VoiceConnection.Channel)
                {
                    var chn1 = ctx.Guild.GetChannel(Data.GetBotChannelId(GuildId));
                    var MainMsg = await chn1.GetMessageAsync(Data.GetBotChannelMainMessageId(GuildId));
                    await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {ctx.Prefix}"); // PREFIX
                    await VoiceConnection.DisconnectAsync();
                    VoiceConnections.Remove(GuildId);
                    Volumes.Remove(VoiceConnection);
                    int timeout = 1000;
                    Task<LavalinkGuildConnection> task = null;
                    try
                    {
                        task = Lavalink.ConnectAsync(vc);
                    }
                    catch { return; }

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
                        await ctx.Channel.SendMessageAsync(embed: SendCannotJoinEmbed);
                        return;
                    }

                    Volumes.Add(VoiceConnection, configJson.DefaultVolume);
                }
            }
            else
            {
                int timeout = 1000;
                Task<LavalinkGuildConnection> task = null;
                try
                {
                    task = Lavalink.ConnectAsync(vc);
                }
                catch { return; }
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
                    await ctx.Channel.SendMessageAsync(embed: SendCannotJoinEmbed);
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
                if (!AnnounceStates.ContainsKey(GuildId))
                    AnnounceStates.Add(GuildId, true);
                if (!IsPausedStates.ContainsKey(GuildId))
                    IsPausedStates.Add(GuildId, false);
                if (!TrackLoadPlaylists.ContainsKey(GuildId))
                    TrackLoadPlaylists.Add(GuildId, new List<LavalinkTrack>());
                if (!CurrentSong.ContainsKey(GuildId))
                    CurrentSong.Add(GuildId, 0);
                if (!Data.CheckFavoritesTracksListsExists(GuildId))
                    Data.SetFavoritesTracksList(GuildId, new List<LavalinkTrack>());
                if (!Loopmodes.ContainsKey(GuildId))
                    Loopmodes.Add(GuildId, loopmode.off);
                await VoiceConnection.SetVolumeAsync(configJson.DefaultVolume);

            }





        }


        [RequireBotPermissions(Permissions.ManageChannels)]
        [Command("leave"), Description("1Leaves a voice channel."), Aliases("dc", "disconnect")]
        public async Task LeaveAsync(CommandContext ctx)
        {
            //  if (!DeletePool.ContainsKey(ctx.Message.Id))
            //      // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (!Data.CheckBotChannelExists(ctx.Guild.Id))
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
                var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
                var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
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

        [Command("play"), Description("1Add a song or playlist to the queue with a URL.\n*Maximum 500 songs per playlist are loaded.*\n*Youtube, Twitch, Vimeo Links.*\n*Only Twitch Livestream support.*|Search for a term and choose a song from a maximum of five.\n*Youtube videos only.*"), Aliases("p")]
        public async Task QueueAsync(CommandContext ctx, [Description("Youtube, Twitch, Vimeo Links.\nOnly Twitch Livestream support.")] Uri Url)
        {
            // if (!DeletePool.ContainsKey(ctx.Message.Id))
            //    // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (Url.ToString() == String.Empty)
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInAVoiceChannelAsync(ctx);
                return;
            }
            await JoinAsync(ctx, ctx.Member.VoiceState?.Channel);
            VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection);
            if (VoiceConnection == null)
                return;

            if (await CheckIsBotChannelAndMessagesExits(ctx))
                return;

            //if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            // {
            //     SendNotInSameChannelAsync(ctx);
            //      return;
            // }
            LavalinkLoadResult trackLoad;
            var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
            bool IsFirstSong = false;

            var notfoundembed = new DiscordEmbedBuilder
            {
                Title = "Song was not found!",
                Color = DiscordColor.Red
            };
            notfoundembed.AddField("Link:", $"`{Url}`");
            notfoundembed.WithFooter($"Prefix for this Server is: {ctx.Prefix}");
            VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);
            // SOUNDCLOUD API ist schmutz
            if (Url.ToString().Contains("soundcloud"))
            {

                await ctx.RespondAsync(embed: notfoundembed).ConfigureAwait(false);
                return;

            }
            else
            {
                trackLoad = await this.Lavalink.Rest.GetTracksAsync(Url);
                if (trackLoad.Tracks.Count() == 0)
                {

                    await ctx.RespondAsync(embed: notfoundembed).ConfigureAwait(false);
                    return;
                }
                if (Url.ToString().Contains("twitch"))
                {
                    DiscordEmbedBuilder deb = new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Blurple,
                        Description = "Twitch livestreams need a little more time!\nDont worry."
                    };
                    await ctx.RespondAsync(embed: deb).ConfigureAwait(false);
                }
                if (TrackLoadPlaylists[ctx.Guild.Id].Count == 0)
                    IsFirstSong = true;
                foreach (var entry in trackLoad.Tracks)
                {
                    if (TrackLoadPlaylists[ctx.Guild.Id].Count > 10000) // max Songs in playlist
                    {
                        DiscordEmbedBuilder embedmaxsong = new DiscordEmbedBuilder
                        {
                            Title = "You have reached the maximum number of songs in a queue!",
                            Color = DiscordColor.Red
                        };
                        embedmaxsong.WithFooter("Maximum 10000 songs!");
                        await ctx.RespondAsync(embed: embedmaxsong);
                        break;
                    }

                    TrackLoadPlaylists[ctx.Guild.Id].Add(entry);
                }

            }

            if (VoiceConnection != null)
            {
                if (IsFirstSong)
                {
                    var track = trackLoad.Tracks.First();
                    await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");

                    await VoiceConnection.PlayAsync(TrackLoadPlaylists[ctx.Guild.Id].First());
                }
                else
                {
                    await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, trackLoad.Tracks.First())}");
                }

            }
            if (trackLoad.Tracks.Count() == 1)
            {
                var addtolistembed = new DiscordEmbedBuilder
                {
                    Title = $"{trackLoad.Tracks.First().Title} by {trackLoad.Tracks.First().Author}",
                    Color = DiscordColor.DarkGreen
                };
                addtolistembed.ImageUrl = getThumbnail(trackLoad.Tracks.First());
                addtolistembed.WithAuthor("Song");
                addtolistembed.WithFooter($"Use {ctx.Prefix}skip to skip to the next song");
                var announcemsg = await ctx.Channel.SendMessageAsync(embed: addtolistembed).ConfigureAwait(false);
            }
            else
            {
                var addtolistembed = new DiscordEmbedBuilder
                {
                    Title = $"You added {trackLoad.Tracks.Count()} songs to the queue.",
                    Color = DiscordColor.DarkGreen
                };
                addtolistembed.ImageUrl = getThumbnail(trackLoad.Tracks.First());
                addtolistembed.WithAuthor("Playlist");
                addtolistembed.WithFooter($"Use {ctx.Prefix}skip to skip to the next song");
                var announcemsg = await ctx.Channel.SendMessageAsync(embed: addtolistembed).ConfigureAwait(false);
            }
        }
        //Queue with keywords
        [Command("play")]
        public async Task QueueAsync(CommandContext ctx, [Description("Keywords"), RemainingText] String Keywords)
        {
            try
            {

                if (Keywords == String.Empty)
                    return;

                if (CheckHasCooldown(ctx))
                {
                    SendCooldownAsync(ctx);
                    return;
                }

                if (CheckHasPermission(ctx, role.everyone))
                    return;
                if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
                {
                    SendRestrictedChannelAsync(ctx);
                    return;
                }
                if (ctx.Member.VoiceState?.Channel == null)
                {
                    SendNotInAVoiceChannelAsync(ctx);
                    return;
                }
                await JoinAsync(ctx, ctx.Member.VoiceState?.Channel);
                VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection);
                if (VoiceConnection == null)
                    return;
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
                VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

                LavalinkLoadResult trackLoad;
                var interactivity = ctx.Client.GetInteractivity();
                trackLoad = await this.Lavalink.Rest.GetTracksAsync(Keywords);
                if (trackLoad.Tracks.Count() == 0)
                {
                    await ctx.RespondAsync(embed: notfoundembed).ConfigureAwait(false);
                    return;
                }
                else if (trackLoad.Tracks.Count() == 1)
                {
                    TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.First());
                }
                else
                {
                    DiscordEmbedBuilder AbortEmbed = new DiscordEmbedBuilder
                    {
                        Title = $"Action aborted",
                        Color = DiscordColor.Red
                    };

                    DiscordEmbedBuilder foundSongs = new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.DarkGreen
                    };
                    foundSongs.WithDescription("Choose a song from the following");
                    // Need dependency in message create event for footer
                    foundSongs.WithFooter("After 30 seconds this is canceled");
                    int c = 0;
                    DiscordEmoji emoji = null;
                    foreach (var track in trackLoad.Tracks)
                    {
                        c++;
                        switch (c)
                        {
                            case 1:
                                emoji = DiscordEmoji.FromName(ctx.Client, ":one:");
                                break;
                            case 2:
                                emoji = DiscordEmoji.FromName(ctx.Client, ":two:");
                                break;
                            case 3:
                                emoji = DiscordEmoji.FromName(ctx.Client, ":three:");
                                break;
                            case 4:
                                emoji = DiscordEmoji.FromName(ctx.Client, ":four:");
                                break;
                            case 5:
                                emoji = DiscordEmoji.FromName(ctx.Client, ":five:");
                                break;
                        }
                        foundSongs.WithTitle($"{c} songs was found!");
                        foundSongs.AddField($"{track.Title} by {track.Author}", $"React with {emoji}");
                        if (foundSongs.Fields.Count >= 5)
                            break;
                    }
                    var msg = await ctx.RespondAsync(embed: foundSongs);
                    int x = 0;
                    foreach (var entry in foundSongs.Fields)
                    {
                        x++;
                        switch (x)
                        {
                            case 1:
                                await msg.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":one:"));
                                break;
                            case 2:
                                await msg.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":two:"));
                                break;
                            case 3:
                                await msg.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":three:"));
                                break;
                            case 4:
                                await msg.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":four:"));
                                break;
                            case 5:
                                await msg.CreateReactionAsync(DiscordEmoji.FromName(ctx.Client, ":five:"));
                                break;
                        }
                    }
                    InteractivityResult<MessageReactionAddEventArgs> result = await interactivity.WaitForReactionAsync(x => x.Message == msg && x.User == ctx.Member, new TimeSpan(0, 0, 30));
                    if (result.TimedOut)
                    {
                        if (!DeletePool.ContainsKey(msg.Id))
                            DeletePool.Add(msg.Id, new DeleteMessage(msg.Channel, msg));
                        var abort = await ctx.Channel.SendMessageAsync(embed: AbortEmbed);
                        if (!DeletePool.ContainsKey(abort.Id))
                            DeletePool.Add(abort.Id, new DeleteMessage(abort.Channel, abort));
                        return;
                    }
                    if (result.Result.Emoji == DiscordEmoji.FromName(ctx.Client, ":one:"))
                    {
                        TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.GetItemByIndex(0));
                    }
                    else if (result.Result.Emoji == DiscordEmoji.FromName(ctx.Client, ":two:"))
                    {
                        TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.GetItemByIndex(1));
                    }
                    else if (result.Result.Emoji == DiscordEmoji.FromName(ctx.Client, ":three:"))
                    {
                        TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.GetItemByIndex(2));
                    }
                    else if (result.Result.Emoji == DiscordEmoji.FromName(ctx.Client, ":four:"))
                    {
                        TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.GetItemByIndex(3));
                    }
                    else if (result.Result.Emoji == DiscordEmoji.FromName(ctx.Client, ":five:"))
                    {
                        TrackLoadPlaylists[ctx.Guild.Id].Add(trackLoad.Tracks.GetItemByIndex(4));
                    }
                    if (!DeletePool.ContainsKey(msg.Id))
                        DeletePool.Add(msg.Id, new DeleteMessage(msg.Channel, msg));
                }
                if (VoiceConnection != null)
                {

                    var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
                    var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
                    if (TrackLoadPlaylists[ctx.Guild.Id].Count == 1)
                    {
                        var track = TrackLoadPlaylists[ctx.Guild.Id].First();
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, TrackLoadPlaylists[ctx.Guild.Id].First())}");
                        await VoiceConnection.PlayAsync(track);

                    }
                    else
                    {
                        await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, TrackLoadPlaylists[ctx.Guild.Id].First())}");
                    }
                    if (trackLoad.Tracks.Count() >= 1)
                    {
                        var addtolistembed = new DiscordEmbedBuilder
                        {
                            Title = $"{TrackLoadPlaylists[ctx.Guild.Id].Last().Title} by {TrackLoadPlaylists[ctx.Guild.Id].Last().Author}",
                            Color = DiscordColor.DarkGreen
                        };
                        addtolistembed.ImageUrl = getThumbnail(TrackLoadPlaylists[ctx.Guild.Id].Last());
                        addtolistembed.WithAuthor("Song");
                        addtolistembed.WithFooter($"Use {ctx.Prefix}skip to skip to the next song");
                        var announcemsg = await ctx.Channel.SendMessageAsync(embed: addtolistembed).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                ctx.Client.Logger.LogError(new EventId(7780, "Play"), e.Message);
            }

        }


        [Command("next"), Description("2Skips to the next song."), Aliases("n", "skip")]
        public async Task NextAsync(CommandContext ctx)
        {

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
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
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
            var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
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
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
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
            var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
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
                //  if (!DeletePool.ContainsKey(ctx.Message.Id))
                //     // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
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
                if (!Data.CheckBotChannelExists(ctx.Guild.Id))
                    return;
                var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));

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
                        // DeletePool.Add(msgal.Result.Id, new DeleteMessage(chn, msgal.Result));
                        return;
                    }

                    VoteSkip[ctx.Guild.Id] = true;
                }
                else
                {
                    VoteSkip.Add(ctx.Guild.Id, true);
                }
                if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
                {
                    SendRestrictedChannelAsync(ctx);
                    VoteSkip[ctx.Guild.Id] = false;
                    return;
                }
                int UserCount = VoiceConnection.Channel.Users.Count() - 1;
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
                    // DeletePool.Add(msg2.Id, new DeleteMessage(chn, msg2));
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
                        // DeletePool.Add(msg.Id, new DeleteMessage(chn, msg));
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
                            // DeletePool.Add(msg.Id, new DeleteMessage(chn, msg));
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
            //  if (!DeletePool.ContainsKey(ctx.Message.Id))
            //     // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
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
                // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }
            var track = TrackLoadPlaylists[ctx.Guild.Id].GetItemByIndex(index);
            await VoiceConnection.PauseAsync();
            await VoiceConnection.PlayAsync(track);
            TrackLoadPlaylists[ctx.Guild.Id].RemoveRange(1, index);
            var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");

        }

        //Remove song with Index
        [Command("remove"), Description("2Deletes a song from the queue."), Aliases("r", "rm", "delete", "del")]
        public async Task RemoveAsync(CommandContext ctx, [Description("Index der Playlist"), RemainingText] int index)
        {
            // if (!DeletePool.ContainsKey(ctx.Message.Id))
            // // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
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
                // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                return;
            }
            TrackLoadPlaylists[ctx.Guild.Id].RemoveAt(index);
            var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}");


        }


        [Command("loop"), Description("2Switch between 3 loop modes.\nOff: Loop deactivated.\nLoopqueue: Loops the complete queue.\nLoopsong: Loops only a special song."), Aliases("l")]
        public async Task LoopAsync(CommandContext ctx)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            // // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
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
            var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{TrackLoadPlaylists[ctx.Guild.Id].First().Title}`", ImageUrl: getThumbnail(TrackLoadPlaylists[ctx.Guild.Id].First()), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, TrackLoadPlaylists[ctx.Guild.Id].First())}");



            // var chn = ctx.Guild.GetChannel(BotChannels[ctx.Guild.Id]);
            //var MainMsg = await chn.GetMessageAsync(BotChannelMainMessages[ctx.Guild.Id]);
            //await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetFavoriteMessage(ctx.Guild.Id, track)}");

        }

        //Queue clear
        [Command("clear"), Description("2Clears the queue.")]
        public async Task ClearAsync(CommandContext ctx)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            // // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
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
            // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
            var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange);

        }
        /*
        //Get current Lyrics
        [Command("lyrics"), Description("Zeigt die Lyrics an.")]
        public async Task LyricsAsync(CommandContext ctx)
        {
            LavalinkGuildConnection VoiceConnection;
            VoiceConnections.TryGetValue(ctx.Guild.Id, out VoiceConnection);

            if (VoiceConnection == null)
                return;
            if (VoiceConnection.CurrentState.CurrentTrack == null)
                return;

         

            try
            {

                var baseAddress = new Uri("https://api.musixmatch.com/ws/1.1/");

                using (var httpClient = new HttpClient { BaseAddress = baseAddress })
                {
                    Console.WriteLine(VoiceConnection.CurrentState.CurrentTrack.Author);
                    Console.WriteLine(VoiceConnection.CurrentState.CurrentTrack.Title);
                    using (var response = await httpClient.GetAsync("Coldplay/Adventure%20of%20a%20Lifetime"))
                    {

                        string responseData = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(responseData);
                    }
                }
                //using (HttpClient httpclient = new HttpClient())
                //{
                //    Console.WriteLine(VoiceConnection.CurrentState.CurrentTrack.Author);
                //    Console.WriteLine(VoiceConnection.CurrentState.CurrentTrack.Title);
                //    Console.WriteLine($"https://api.lyrics.ovh/v1/{VoiceConnection.CurrentState.CurrentTrack.Author}/{VoiceConnection.CurrentState.CurrentTrack.Title}");
                //    var content = await httpclient.GetStringAsync($"https://api.lyrics.ovh/v1/{VoiceConnection.CurrentState.CurrentTrack.Author}/{VoiceConnection.CurrentState.CurrentTrack.Title}");
                //    Console.WriteLine(content);
                //}
               
               

            }
            catch
            {
               
            }

            

         //   var title = VoiceConnection.CurrentState.CurrentTrack.Title.Replace("(Official Video)", String.Empty);

         //   title = title.Replace(VoiceConnection.CurrentState.CurrentTrack.Author, String.Empty);

          //  var lyrics = await lyricsService.GetLyricsAsync(Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(VoiceConnection.CurrentState.CurrentTrack.Author)).Replace("?", String.Empty), Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(title)).Replace("?", String.Empty));

         //   if (lyrics == null)
       //     {
        //        await ctx.RespondAsync($"Lyrics not found.").ConfigureAwait(false);
        //        return;
        //    }
        //    await ctx.RespondAsync($"`{VoiceConnection.CurrentState.CurrentTrack.Title}` von `{VoiceConnection.CurrentState.CurrentTrack.Author}`:\n`{lyrics}`").ConfigureAwait(false);
        }
/*
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
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //// DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
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
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            var chn = await ctx.Client.GetChannelAsync(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
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
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //// DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
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
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
            {
                SendNotInSameChannelAsync(ctx);
                return;
            }
            var chn = await ctx.Client.GetChannelAsync(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
            if (IsPausedStates[ctx.Guild.Id])
            {
                await VoiceConnection.ResumeAsync();
                IsPausedStates[ctx.Guild.Id] = !IsPausedStates[ctx.Guild.Id];
                var track = TrackLoadPlaylists[ctx.Guild.Id].First();
                await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");
            }


        }

        [Command("stop"), Description("1Stops playback and clear the queue.")]
        public async Task StopAsync(CommandContext ctx)
        {

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
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
            var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, "No song playing currently", ImageUrl: configJson.NoSongPicture, Footer: $"Prefix for this Server is: {ctx.Prefix}"); // PREFIX
        }

        [Command("shuffle"), Description("2Shuffel the current queue.")]
        public async Task ShuffleAsync(CommandContext ctx)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //// DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
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
            var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, $"Playing `{track.Title}`", ImageUrl: getThumbnail(track), Footer: $"{getQueueCount(ctx.Guild.Id)} songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}{GetFavoriteMessage(ctx.Guild.Id, track)}");

        }

        //[Command("24/7"), Description("2Enable the 24/7 mode.\nThe bot will no longer automatically leave the voice channel."), Aliases("247")]
        //public async Task TwentyFourSevenAsync(CommandContext ctx)
        //{
        //    //if (!DeletePool.ContainsKey(ctx.Message.Id))
        //    //// DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
        //    if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
        //        return;
        //    if (CheckHasCooldown(ctx))
        //    {
        //        SendCooldownAsync(ctx);
        //        return;
        //    }
        //    if (CheckHasPermission(ctx, role.dj))
        //        return;
        //    if (BotChannels[ctx.Guild.Id] != ctx.Channel.Id)
        //    {
        //        SendRestrictedChannelAsync(ctx);
        //        return;
        //    }
        //    if (ctx.Member.VoiceState?.Channel != VoiceConnection.Channel || ctx.Member.VoiceState?.Channel == null)
        //    {
        //        SendNotInSameChannelAsync(ctx);
        //        return;
        //    }
        //    if (AFKTimeOffsets.ContainsKey(ctx.Guild.Id))
        //        AFKTimeOffsets[ctx.Guild.Id] = new DateTimeOffset();

        //    if (CheckAFKStates.ContainsKey(ctx.Guild.Id))
        //        CheckAFKStates[ctx.Guild.Id] = !CheckAFKStates[ctx.Guild.Id];
        //    else
        //        CheckAFKStates.Add(ctx.Guild.Id, false);

        //    if (CheckAFKStates[ctx.Guild.Id] == false)
        //    {

        //        var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
        //        {
        //            Description = "The 24/7 mode has been enabled.",
        //            Color = DiscordColor.Orange
        //        };
        //        DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
        //        // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        //    } 
        //    else
        //    {
        //        var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
        //        {
        //            Description = "The 24/7 mode has been disabled.",
        //            Color = DiscordColor.Orange
        //        };
        //        DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
        //        // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        //    }

        //}

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
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //// DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
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
            if (DateTime.TryParseExact(pos, "ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) { }
            else if (DateTime.TryParseExact(pos, "mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) { }
            else if (DateTime.TryParseExact(pos, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) { }
            else
            {
                var SendErrMsgEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Wrong Format.\nUse {ctx.Prefix}help seek to get more information.",
                    Color = DiscordColor.Orange
                };
                DiscordMessage errmsg = await ctx.Channel.SendMessageAsync(embed: SendErrMsgEmbed);
                // DeletePool.Add(errmsg.Id, new DeleteMessage(ctx.Channel, errmsg));
                return;
            }

            TimeSpan position = dt.TimeOfDay;

            String Time;
            if (position.Days != 0)
                Time = position.ToString("d'd 'h'h 'm'm 's's'");
            else if (position.Hours != 0)
                Time = position.ToString("h'h 'm'm 's's'");
            else if (position.Minutes != 0)
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
            // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        [Command("volume"), Description("2Lautstärke regeln."), Aliases("v")]
        public async Task VolumeAsync(CommandContext ctx, int volume)
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
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
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

                return;
            }

            await VoiceConnection.SetVolumeAsync(volume);
            Volumes[VoiceConnection] = volume;

            var chn = ctx.Guild.GetChannel(Data.GetBotChannelId(ctx.Guild.Id));
            var MainMsg = await chn.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));

            await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, Footer: $"0 songs in queue | Volume: {Volumes[VoiceConnection]}%{GetLoopMessage(ctx.Guild.Id)}");
        }

        [Command("equalizer"), Description("2Resets equalizer settings.|Sets equalizer settings.\nBand: 0-14\nGain: -0.25 (muted) up to +1.0 (+0.25 means the band is doubled)\n*Presets are planned.*"), Aliases("eq")]
        public async Task EqualizerAsync(CommandContext ctx)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            // // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            await VoiceConnection.ResetEqualizerAsync();

            var eqmsg = await ctx.RespondAsync("All equalizer bands were reset.").ConfigureAwait(false);
            // DeletePool.Add(eqmsg.Id, new DeleteMessage(eqmsg.Channel, eqmsg));
        }

        [Command("equalizer")]
        public async Task EqualizerAsync(CommandContext ctx, int Band, float Gain)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //// DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (!VoiceConnections.TryGetValue(ctx.Guild.Id, out LavalinkGuildConnection VoiceConnection))
                return;
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.dj))
                return;
            if (Data.GetBotChannelId(ctx.Guild.Id) != ctx.Channel.Id)
            {
                SendRestrictedChannelAsync(ctx);
                return;
            }
            await VoiceConnection.AdjustEqualizerAsync(new LavalinkBandAdjustment(Band, Gain));

            var eqmsg = await ctx.RespondAsync($"Band {Band} adjusted by {Gain}").ConfigureAwait(false);
            // DeletePool.Add(eqmsg.Id, new DeleteMessage(eqmsg.Channel, eqmsg));
        }

        [Command("setdj"), Description("3Set a Role as DJ")]
        public async Task SetDjAsync(CommandContext ctx, [Description("@Role Name")] String RoleName)
        {

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
                if (entry.Value.Name == RoleName)
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
                try { Role = ctx.Guild.GetRole(ulong.Parse(RoleName.Replace("<", "").Replace(">", "").Replace("&", "").Replace("@", ""))); } catch { }
                if (Role == null)
                {
                    await ctx.Channel.SendMessageAsync(embed: SendRoleNotFoundEmbed);
                    return;
                }
            }

            if (Data.CheckGuildRolesExists(ctx.Guild.Id))
            {
                if (Data.GetGuildRoles(ctx.Guild.Id).Contains(new Tuple<DiscordRole, role>(Role, role.dj)))
                {
                    var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was already added.",
                        Color = DiscordColor.Orange
                    };
                    await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                    return;
                }
                else
                {
                    Data.AddGuildRole(ctx.Guild.Id, new Tuple<DiscordRole, role>(Role, role.dj));
                    var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was added to DJs.",
                        Color = DiscordColor.DarkGreen
                    };
                    await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                    return;
                }


            }
            else
            {
                List<Tuple<DiscordRole, role>> tup = new List<Tuple<DiscordRole, role>>();
                tup.Add(new Tuple<DiscordRole, role>(Role, role.dj));
                Data.SetGuildRoles(ctx.Guild.Id, tup);
                var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Role ***{Role.Name}*** was added to DJs.",
                    Color = DiscordColor.DarkGreen
                };
                await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                return;
            }

        }
        [Command("removedj"), Description("3Set a Role as DJ"), Aliases("remdj")]
        public async Task RemoveDjAsync(CommandContext ctx, [Description("@Role Name")] String RoleName)
        {
            if (!DeletePool.ContainsKey(ctx.Message.Id))

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
                try { Role = ctx.Guild.GetRole(ulong.Parse(RoleName.Replace("<", "").Replace(">", "").Replace("&", "").Replace("@", ""))); } catch { }
                if (Role == null)
                {
                    await ctx.Channel.SendMessageAsync(embed: SendRoleNotFoundEmbed);
                    return;
                }
            }

            if (Data.CheckGuildRolesExists(ctx.Guild.Id))
            {
                if (Data.GetGuildRoles(ctx.Guild.Id).Contains(new Tuple<DiscordRole, role>(Role, role.dj)))
                {
                    Data.RemoveGuildRole(ctx.Guild.Id, new Tuple<DiscordRole, role>(Role, role.dj));
                    var SendRoleRemovedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was removed from Djs.",
                        Color = DiscordColor.Orange
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleRemovedEmbed);
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
        [Command("djs"), Description("3Get all the roles that are Djs.")]
        public async Task GetDjsAsync(CommandContext ctx)
        {

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.admin))
                return;
            var SendDjListEmptyEmbed = new DiscordEmbedBuilder
            {
                Title = "Dj List",
                Description = $"No Roles found.",
                Color = DiscordColor.Orange
            };
            if (!Data.CheckGuildRolesExists(ctx.Guild.Id))
            {
                await ctx.Channel.SendMessageAsync(embed: SendDjListEmptyEmbed);
                return;
            }
            String DjList = String.Empty;
            foreach (Tuple<DiscordRole, role> entry in Data.GetGuildRoles(ctx.Guild.Id))
            {
                if (entry.Item2 == role.dj)
                {
                    DjList += $"• {entry.Item1.Name} ({entry.Item1.Id})\n";
                }
            }
            if (DjList == String.Empty)
            {

                await ctx.Channel.SendMessageAsync(embed: SendDjListEmptyEmbed);
                return;
            }
            var SendDjListEmbed = new DiscordEmbedBuilder
            {
                Title = "Dj List",
                Description = $"Roles:\n```{DjList}```",
                Color = DiscordColor.DarkGreen
            };
            await ctx.Channel.SendMessageAsync(embed: SendDjListEmbed);

        }
        [Command("setadmin"), Description("3Set a Role as Admin")]
        public async Task SetAdminAsync(CommandContext ctx, [Description("@Role Name")] String RoleName)
        {

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

                try { Role = ctx.Guild.GetRole(ulong.Parse(RoleName.Replace("<", "").Replace(">", "").Replace("&", "").Replace("@", ""))); } catch { }
                if (Role == null)
                {
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleNotFoundEmbed);
                    return;
                }

            }
            if (Data.CheckGuildRolesExists(ctx.Guild.Id))
            {
                if (Data.GetGuildRoles(ctx.Guild.Id).Contains(new Tuple<DiscordRole, role>(Role, role.admin)))
                {
                    var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was already added.",
                        Color = DiscordColor.Orange
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                    return;
                }
                else
                {
                    Data.AddGuildRole(ctx.Guild.Id, new Tuple<DiscordRole, role>(Role, role.admin));
                    var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was added to Admins.",
                        Color = DiscordColor.DarkGreen
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                    // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
                    return;
                }


            }
            else
            {
                List<Tuple<DiscordRole, role>> tup = new List<Tuple<DiscordRole, role>>();
                tup.Add(new Tuple<DiscordRole, role>(Role, role.admin));
                Data.SetGuildRoles(ctx.Guild.Id, tup);
                var SendRoleAlreadyAddedEmbed = new DiscordEmbedBuilder
                {
                    Description = $"Role ***{Role.Name}*** was added to Admins.",
                    Color = DiscordColor.DarkGreen
                };
                await ctx.Channel.SendMessageAsync(embed: SendRoleAlreadyAddedEmbed);
                return;
            }

        }
        [Command("removeadmin"), Description("3Set a Role as DJ"), Aliases("remadmin")]
        public async Task RemoveAdminAsync(CommandContext ctx, [Description("@Role Name")] String RoleName)
        {
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
                try { Role = ctx.Guild.GetRole(ulong.Parse(RoleName.Replace("<", "").Replace(">", "").Replace("&", "").Replace("@", ""))); } catch { }
                if (Role == null)
                {
                    await ctx.Channel.SendMessageAsync(embed: SendRoleNotFoundEmbed);
                    return;
                }
            }

            if (Data.CheckGuildRolesExists(ctx.Guild.Id))
            {
                if (Data.GetGuildRoles(ctx.Guild.Id).Contains(new Tuple<DiscordRole, role>(Role, role.admin)))
                {
                    Data.RemoveGuildRole(ctx.Guild.Id, new Tuple<DiscordRole, role>(Role, role.admin));
                    var SendRoleRemovedEmbed = new DiscordEmbedBuilder
                    {
                        Description = $"Role ***{Role.Name}*** was removed from Admins.",
                        Color = DiscordColor.Orange
                    };
                    DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendRoleRemovedEmbed);
                    // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
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
                    // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
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
            if (!Data.CheckGuildRolesExists(ctx.Guild.Id))
            {
                await ctx.Channel.SendMessageAsync(embed: SendAdminListEmptyEmbed);
                return;
            }
            String AdminList = String.Empty;
            foreach (Tuple<DiscordRole, role> entry in Data.GetGuildRoles(ctx.Guild.Id))
            {
                if (entry.Item2 == role.admin)
                {
                    AdminList += $"• {entry.Item1.Name} ({entry.Item1.Id})\n";
                }
            }
            if (AdminList == String.Empty)
            {
                await ctx.Channel.SendMessageAsync(embed: SendAdminListEmptyEmbed);
                return;
            }
            var SendAdminListEmbed = new DiscordEmbedBuilder
            {
                Title = "Admin List",
                Description = $"Roles:\n```{AdminList}```",
                Color = DiscordColor.DarkGreen
            };
            DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendAdminListEmbed);
            // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));

        }

        [Command("prefix"), Description("3Get and sets the Prefix.")]
        public async Task PrefixAsync(CommandContext ctx)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            // // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

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
                // DeletePool.Add(PrefixFirstMsg.Id, new DeleteMessage(ctx.Channel, PrefixFirstMsg));
                var abort = await ctx.Channel.SendMessageAsync(embed: AbortEmbed);
                await Task.Delay(5000);
                // DeletePool.Add(abort.Id, new DeleteMessage(ctx.Channel, abort));
                return;
            }

            // await PrefixFirstMsg.DeleteReactionAsync(result.Result.Emoji, result.Result.User);

            if (firstresult.Result.Emoji == SetNewEmoji)
            {
                // DeletePool.Add(PrefixFirstMsg.Id, new DeleteMessage(ctx.Channel, PrefixFirstMsg));
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
                    // DeletePool.Add(setnew.Id, new DeleteMessage(ctx.Channel, setnew));
                    var abort = await ctx.Channel.SendMessageAsync(embed: AbortEmbed);
                    //await Task.Delay(5000);
                    // DeletePool.Add(abort.Id, new DeleteMessage(ctx.Channel, abort));
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


                var success = await ctx.Channel.SendMessageAsync(embed: PrefixNewSuccessEmbed);
                var MainMsg = await ctx.Channel.GetMessageAsync(Data.GetBotChannelMainMessageId(ctx.Guild.Id));
                await ModifyMainMsgAsync(MainMsg, DiscordColor.Orange, Footer: $"Prefix for this Server is: {newprefix}");

            }

            if (firstresult.Result.Emoji == Crossed)
            {
                // DeletePool.Add(PrefixFirstMsg.Id, new DeleteMessage(ctx.Channel, PrefixFirstMsg));
                var abort = await ctx.Channel.SendMessageAsync(embed: AbortEmbed);
                await Task.Delay(5000);
                // DeletePool.Add(abort.Id, new DeleteMessage(ctx.Channel, abort));
                return;
            }

        }

        [Command("support"), Description("1If you have a problem, questions or just ideas, you can write us.")]
        public async Task SupportAsync(CommandContext ctx)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));


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
                Color = DiscordColor.DarkGreen,
                Description = "To get support you can simply write to us."
            };
            var lemix = await ctx.Client.GetUserAsync(267645496020041729);
            var michi = await ctx.Client.GetUserAsync(352508207094038538);
            CreditsEmbed.AddField("For technical support:", lemix.Mention);
            CreditsEmbed.AddField("For coopartions or further:", michi.Mention);
            CreditsEmbed.WithFooter("This bot is in beta and is constantly being developed.");
            var msg = await ctx.Channel.SendMessageAsync(embed: CreditsEmbed);
            //await Task.Delay(15000);
            ///// DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        [Command("invite"), Description("1To get an invitation link.")]
        public async Task InviteAsync(CommandContext ctx)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //// DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            DiscordEmbedBuilder InviteEmbed = new DiscordEmbedBuilder
            {
                Title = "Invitation Link",
                Color = DiscordColor.Purple,
                Description = "The link is there to invite the bot to other servers."
            };
            InviteEmbed.AddField("Link", $"[Click here](https://discord.com/oauth2/authorize?client_id={ctx.Client.CurrentUser.Id}&permissions=3271760&scope=bot)");
            InviteEmbed.WithFooter("This bot is in beta and is constantly being developed.");
            var msg = await ctx.Channel.SendMessageAsync(embed: InviteEmbed);
            //await Task.Delay(5000);
            // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        //Description first char:
        // 1 or nothing(please use 1 to provide errors) = everyone
        // 2 = dj
        // 3 = Admin
        // 4 = hidden
        [Command("help"), Description("4")]
        public async Task HelpAsync(CommandContext ctx)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

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
                Title = "Help Command",
                Color = DiscordColor.Orange
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
            //DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        [Command("help")]
        public async Task HelpAsync(CommandContext ctx, String Command)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (Command.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                await HelpAsync(ctx);
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
            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
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
                Description = $"Aliases: {AliasesText}",
                Color = DiscordColor.Orange
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
            //DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }

        [Command("credits"), Description("1Some information about the bot and the team.")]
        public async Task CreditsAsync(CommandContext ctx)
        {
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));


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
                Color = DiscordColor.Orange
            };
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            DateTime buildDate = new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.Revision * 2);
            var lemix = await ctx.Client.GetUserAsync(267645496020041729);
            var michi = await ctx.Client.GetUserAsync(352508207094038538);
            CreditsEmbed.AddField("Developer", lemix.Mention, true);
            CreditsEmbed.AddField("Distribution and Marketing", michi.Mention, true);
            CreditsEmbed.AddField("Bot Creation Time", ctx.Client.CurrentApplication.CreationTimestamp.ToString("dd.MM.yyyy HH:mm:ss"));
            CreditsEmbed.AddField("Version", version.ToString(), true);
            CreditsEmbed.AddField("Build Date", buildDate.ToString(), true);
            CreditsEmbed.WithFooter("This bot is in beta and is constantly being developed.");
            var msg = await ctx.Channel.SendMessageAsync(embed: CreditsEmbed);
            //await Task.Delay(5000);
            //DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }
        [Command("ping"), Description("1Get the Websocket Latency to the Discord API.")]
        public async Task PingAsync(CommandContext ctx)
        {

            if (CheckHasCooldown(ctx))
            {
                SendCooldownAsync(ctx);
                return;
            }
            if (CheckHasPermission(ctx, role.everyone))
                return;
            DiscordEmbedBuilder PingEmbed = new DiscordEmbedBuilder
            {
                Title = "Ping",
                Color = DiscordColor.Blurple
            };

            PingEmbed.AddField("Node", $"This Server running on Node {ctx.Client.ShardId}");
            PingEmbed.AddField("Websocket Latency", $"{ctx.Client.Ping} ms");
            PingEmbed.WithFooter("This bot is in beta and is constantly being developed.");
            await ctx.Channel.SendMessageAsync(embed: PingEmbed);
        }

        // !sendglobalmsg "Title" "TEXT" "color code without #" "footer text"
        [Command("sendglobalmsg"), Description("4")]
        public async Task SendGlobalMessageAsync(CommandContext ctx, string Title, string Description, string Color, string Footer)
        {

            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            // DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));

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
                await ctx.RespondAsync(embed: GlobalMsgFinishedEmbed);

            }

        }

        // !sendglobalmsg "Title" "TEXT" "color code without #" "footer text"
        [Command("sendglobalmsgtest"), Description("4")]
        public async Task SendGlobalMessageTestAsync(CommandContext ctx, string Title, string Description, string Color, string Footer)
        {

            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
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
                await ctx.Member.SendMessageAsync($"USAGE: {ctx.Prefix}{ctx.Command.Name} \"Title\" \"Text\" \"Colorcode without # (HexCode)\" \"FooterText\" ", embed: embed);

            }

        }

        [Command("stats"), Description("4Developer Command.")]
        public async Task StatsAsync(CommandContext ctx)
        {

            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
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
            //if (!DeletePool.ContainsKey(ctx.Message.Id))
            //DeletePool.Add(ctx.Message.Id, new DeleteMessage(ctx.Channel, ctx.Message));
            if (ctx.Member.Id == 267645496020041729 || ctx.Member.Id == 352508207094038538)
            {
                ctx.Client.Logger.LogInformation(new EventId(7700, "StatsCmd"), $"{ctx.Member.DisplayName} [{ctx.Member.Id}] executed Guildlist Command!");
                var guildsstring = new StringBuilder();
                guildsstring.Append("Guild list: ```");


                int totalmember = 0;
                int i = 0;
                foreach(DiscordClient entry1 in Variables.DiscordShardedClient.ShardClients.Values)
                {
                    foreach (DiscordGuild entry in entry1.Guilds.Values)
                    {
                        i++;
                        String t = String.Empty;
                        t = $"{i}. Name: {entry.Name}, Member Count: {entry.MemberCount}, Id: {entry.Id} Owner: {entry.Owner.Mention} Tier: {entry.PremiumTier} Subs: {entry.PremiumSubscriptionCount}";
                        totalmember = totalmember + entry.MemberCount;
                        guildsstring.Append(t).AppendLine();
                    }
                }

                guildsstring.Append($"Total Member: {totalmember}").Append("```");
                await ctx.Member.SendMessageAsync(guildsstring.ToString());
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
            if (showType)
                return $"{d:#,##0.00} {Units[u]}B";
            else
                return $"{d:#.##0.00}";
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
            //else if (track.Uri.AbsoluteUri.Contains("soundcloud", StringComparison.OrdinalIgnoreCase))
            //{
            //    return null;
            //}
            else if (track.Uri.AbsoluteUri.Contains("twitch", StringComparison.OrdinalIgnoreCase))
            {
                //twitch api need oauth to get avatar from channel
                return null;
            }
            else
            {
                return null;
            }

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
            if (tracks.Count <= 1)
            {
                Content += "\nJoin a voice channel and queue songs by name or url in here.";
                return Content;
            }
            else
            {
                int i = 0;
                String Time;
                List<String> tracktitles = new List<String>();
                List<String> temptracktitles = new List<String>();
                foreach (LavalinkTrack entry in tracks)
                {
                    if (i == 0)
                    {
                        i++;
                        continue;
                    }
                    if (entry.Length.Days != 0)
                        Time = entry.Length.ToString("d'd 'h'h 'm'm 's's'");
                    else if (entry.Length.Hours != 0)
                        Time = entry.Length.ToString("h'h 'm'm 's's'");
                    else
                        Time = entry.Length.ToString("m'm 's's'");
                    tracktitles.Add($"\n{i}. {entry.Title} [{Time}]");
                    i++;
                }

                foreach (String entry in tracktitles)
                {
                    if (Content.Length + entry.Length > 1925) // 2000 (max discord msg length) - upper message (75 chars)
                    {
                        int len = tracktitles.Count - temptracktitles.Count;
                        TimeSpan restlength = new TimeSpan();
                        string time2;
                        for (int i1 = tracktitles.Count - len - 1; i1 < tracktitles.Count - 1; i1++)
                        {
                            restlength = restlength.Add(tracks[i1].Length);
                        }
                        if (restlength.Days != 0)
                            time2 = restlength.ToString("d'd 'h'h 'm'm 's's'");
                        else if (restlength.Hours != 0)
                            time2 = restlength.ToString("h'h 'm'm 's's'");
                        else
                            time2 = restlength.ToString("m'm 's's'");

                        temptracktitles.Add($"\n*{len} more songs in the queue with a with a duration of {time2}*"); //75 chars 
                        break;
                    }
                    Content += entry;
                    temptracktitles.Add(entry);
                }
                Content = "";
                temptracktitles.Reverse();
                foreach (String entry in temptracktitles)
                {
                    Content += entry;
                }
                if (Content.Length > 2000)
                    Content = Content.Substring(0, 2000);
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
            if (Loopmodes[GuildId] == loopmode.off)
            {
                return "";
            }
            else if (Loopmodes[GuildId] == loopmode.loopqueue)
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
            try
            {
                if (Data.GetFavoritesTracksLists(GuildId) == null)
                {
                    Data.SetFavoritesTracksList(GuildId, new List<LavalinkTrack>());
                    return false;
                }
                else
                {
                    foreach (LavalinkTrack entry in Data.GetFavoritesTracksLists(GuildId))
                    {
                        if (track.Identifier == entry.Identifier)
                        {
                            return true;
                        }
                    }
                }

            }
            catch (Exception e)
            {
                Variables.Logger.LogError(new EventId(7789, "IsTrackFavorite"), e.Message);
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
            if (GuildId == 0)
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
        private async void SendCooldownAsync(CommandContext ctx, ulong GuildId = 0)
        {
            var cooldownembed = new DiscordEmbedBuilder
            {
                Title = "Cooldown",
                Description = "Please be patient.",
                Color = DiscordColor.Orange
            };
            if (GuildId != 0)
            {
                Task<DiscordChannel> result = null;
                try
                {
                    result = ctx.Client.GetChannelAsync(GuildId);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                if (result.Result != null)
                {
                    await result.Result.SendMessageAsync(embed: cooldownembed);
                }
                return;
            }
            try
            { DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: cooldownembed); }
            catch (UnauthorizedException)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Missing Permission!",
                    Description = "The bot does not have the necessary rights to execute the command!",
                    Color = DiscordColor.Red

                };
                embed.AddField("Missing permissions:", "Send messages");
                embed.WithFooter($"If you are not an admin of this server, please inform an admin. \nIf the error persists then please inform our support with {ctx.Prefix}support.");
                try
                {
                    var dmchannel = await ctx.Member.CreateDmChannelAsync();
                    await dmchannel.SendMessageAsync(embed: embed);
                }
                catch { }

            }

            //DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }
        private async void SendRestrictedChannelAsync(CommandContext ctx, ulong GuildId = 0)
        {
            if (GuildId == 0)
                GuildId = ctx.Guild.Id;
            DiscordChannel chn = null;
            try
            {
                chn = await ctx.Client.GetChannelAsync(Data.GetBotChannelId(ctx.Guild.Id));
            }
            catch (Exception e)
            {
                ctx.Client.Logger.LogError(new EventId(7778, "SendRestriChnMsg"), e.ToString());
                return;
            }

            var RestrictedChannelEmbed = new DiscordEmbedBuilder
            {
                Description = $"This command is restricted to {chn.Mention}.",
                Color = DiscordColor.Orange
            };
            try
            {
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: RestrictedChannelEmbed);
            }
            catch (UnauthorizedException)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Missing Permission!",
                    Description = "The bot does not have the necessary rights to execute the command!",
                    Color = DiscordColor.Red

                };
                embed.AddField("Missing permissions:", "Send messages");
                embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                try
                {
                    var dmchannel = await ctx.Member.CreateDmChannelAsync();
                    await dmchannel.SendMessageAsync(embed: embed);
                }
                catch { }

            }


        }
        private async void SendNotInSameChannelAsync(CommandContext ctx)
        {
            var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
            {
                Description = "You need to be in the same Channel with the Bot.",
                Color = DiscordColor.Orange
            };
            try
            {
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
            }
            catch (UnauthorizedException)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Missing Permission!",
                    Description = "The bot does not have the necessary rights to execute the command!",
                    Color = DiscordColor.Red

                };
                embed.AddField("Missing permissions:", "Send messages");
                embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                try
                {
                    var dmchannel = await ctx.Member.CreateDmChannelAsync();
                    await dmchannel.SendMessageAsync(embed: embed);
                }
                catch { }

            }

        }
        private async void SendNotConnectedAsync(CommandContext ctx)
        {
            var SendNotConnectedEmbed = new DiscordEmbedBuilder
            {
                Description = "The Bot is not connected.",
                Color = DiscordColor.Orange
            };
            try
            {
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotConnectedEmbed);
            }
            catch (UnauthorizedException)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Missing Permission!",
                    Description = "The bot does not have the necessary rights to execute the command!",
                    Color = DiscordColor.Red

                };
                embed.AddField("Missing permissions:", "Send messages");
                embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                try
                {
                    var dmchannel = await ctx.Member.CreateDmChannelAsync();
                    await dmchannel.SendMessageAsync(embed: embed);
                }
                catch { }

            }
        }
        private async void SendNeedSetupAsync(CommandContext ctx)
        {
            var SendNotInSameChannelEmbed = new DiscordEmbedBuilder
            {
                Description = $"You need to execute the setup before you can use this command. \n For more information use ***{ctx.Prefix}help setup***.",
                Color = DiscordColor.Orange
            };
            try
            {
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInSameChannelEmbed);
            }
            catch (UnauthorizedException)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Missing Permission!",
                    Description = "The bot does not have the necessary rights to execute the command!",
                    Color = DiscordColor.Red

                };
                embed.AddField("Missing permissions:", "Send messages");
                embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                try
                {
                    var dmchannel = await ctx.Member.CreateDmChannelAsync();
                    await dmchannel.SendMessageAsync(embed: embed);
                }
                catch { }

            }
        }
        private async void SendNotInAVoiceChannelAsync(CommandContext ctx)
        {
            var SendNotInAVoiceChannelEmbed = new DiscordEmbedBuilder
            {
                Description = "You are not in a voicechannel or have specified a channel.",
                Color = DiscordColor.Orange
            };
            try
            {
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNotInAVoiceChannelEmbed);
            }
            catch (UnauthorizedException)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Missing Permission!",
                    Description = "The bot does not have the necessary rights to execute the command!",
                    Color = DiscordColor.Red

                };
                embed.AddField("Missing permissions:", "Send messages");
                embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                try
                {
                    var dmchannel = await ctx.Member.CreateDmChannelAsync();
                    await dmchannel.SendMessageAsync(embed: embed);
                }
                catch { }

            }

            // DeletePool.Add(msg.Id, new DeleteMessage(ctx.Channel, msg));
        }
        private async void SendQueueIsEmptyAsync(CommandContext ctx)
        {
            var SendQueueIsEmptyEmbed = new DiscordEmbedBuilder
            {
                Description = $"The queue is empty.",
                Color = DiscordColor.Orange
            };

            try
            {
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendQueueIsEmptyEmbed);
            }
            catch (UnauthorizedException)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Missing Permission!",
                    Description = "The bot does not have the necessary rights to execute the command!",
                    Color = DiscordColor.Red

                };
                embed.AddField("Missing permissions:", "Send messages");
                embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                try
                {
                    var dmchannel = await ctx.Member.CreateDmChannelAsync();
                    await dmchannel.SendMessageAsync(embed: embed);
                }
                catch { }

            }
        }
        private async void SendNoPermssionAsync(CommandContext ctx)
        {
            var SendNoPermssionEmbed = new DiscordEmbedBuilder
            {
                Description = $"You are not allowed to execute the command!",
                Color = DiscordColor.Red
            };
            try
            {
                DiscordMessage msg = await ctx.Channel.SendMessageAsync(embed: SendNoPermssionEmbed);
            }
            catch (UnauthorizedException)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Missing Permission!",
                    Description = "The bot does not have the necessary rights to execute the command!",
                    Color = DiscordColor.Red

                };
                embed.AddField("Missing permissions:", "Send messages");
                embed.WithFooter($"If you are not an admin of this server, please inform an admin.\nIf the error persists then please inform our support with {ctx.Prefix}support.");
                try
                {
                    var dmchannel = await ctx.Member.CreateDmChannelAsync();
                    await dmchannel.SendMessageAsync(embed: embed);
                }
                catch { }

            }
        }
        private bool CheckHasPermission(CommandContext ctx, role NeededRole, DiscordMember member = null, ulong GuildId = 0)
        {
            if (GuildId == 0)
                GuildId = ctx.Guild.Id;
            if (member == null)
                member = ctx.Member;
            if (member.IsOwner || role.everyone == NeededRole || member.PermissionsIn(ctx.Channel).HasFlag(Permissions.All) || member.PermissionsIn(ctx.Channel).HasFlag(Permissions.Administrator) || member.PermissionsIn(ctx.Channel).HasFlag(Permissions.ManageGuild) || member.PermissionsIn(ctx.Channel).HasFlag(Permissions.ManageRoles))
            {
                return false;
            }
            if (Data.CheckGuildRolesExists(GuildId))
                foreach (Tuple<DiscordRole, role> entry in Data.GetGuildRoles(ctx.Guild.Id))
                {
                    if (entry.Item2 == NeededRole || entry.Item2 == role.admin)
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
