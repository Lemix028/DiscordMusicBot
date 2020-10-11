using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using LemixDiscordMusikBot.Commands;
using Newtonsoft.Json;
using System;
using DSharpPlus.Lavalink;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Net;
using DSharpPlus.Lavalink.EventArgs;
using System.Collections.Generic;
using DSharpPlus.Interactivity;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace LemixDiscordMusikBot
{
    public class DiscordBot
    {
        public DiscordShardedClient Client { get; private set; }
        public int GuildCount = 0;

        public Dictionary<ulong, List<String>> Prefixes = new Dictionary<ulong, List<String>>();
        public IReadOnlyDictionary<int, CommandsNextExtension> CommandsTask { get; set;}

        public Boolean debug = true;

        public Config configJson;

        public async Task StartAsync()
        {

            var json = string.Empty;
           // Console.WriteLine(JsonConvert.SerializeObject(new StatusItem {Activity = ActivityType.Playing, StatusType = UserStatus.DoNotDisturb, Text = "asdfcuzghuzhgodsaf" }));
            try
            {
                using (var fs = File.OpenRead("config.json"))
                using (StreamReader sr = new StreamReader(fs, new UTF8Encoding(false)))
                    json = await sr.ReadToEndAsync().ConfigureAwait(false);

                configJson = JsonConvert.DeserializeObject<Config>(json);
            }
            catch(FileNotFoundException e)
            {
                if (debug)
                {
                    Console.WriteLine(e);
                } 
                else
                {
                    Console.WriteLine("Config konnte nicht gefunden werden!");
                }
            }
            catch (UnauthorizedAccessException e)
            {
                if (debug)
                {
                    Console.WriteLine(e);
                }
                else
                {
                    Console.WriteLine("Config kann nicht gelesen werden! Unzureichende Rechte!");
                }
            }
            catch (Exception e)
            {
                if (debug)
                {
                    Console.WriteLine(e);
                }
                else
                {
                    Console.WriteLine("Unbekannter Fehler beim Lesen der Config!");
                }
            }

            DiscordConfiguration dconfig = new DiscordConfiguration()
            {
                Token = configJson.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                LogTimestampFormat = "dd-MM-yyyy HH:mm:ss",
                MinimumLogLevel = LogLevel.Debug
                
                

            };
            Client = new DiscordShardedClient(dconfig);
            
            Client.Ready += async (s, e) => { await OnClientReady(s, e); };
            Client.GuildCreated += async (s, e) => { await OnGuildCreated(s, e); };
            Client.GuildAvailable += async (s, e) => { await OnGuildAvailable(s, e); };
            Client.GuildDeleted += async (s, e) => { await OnGuildDeleted(s, e); };
            Client.ClientErrored += async (s, e) => { await OnClientError(s, e); }; 
            Client.MessageReactionAdded += async (s, e) => { await OnMessageReactionAdded(s, e); };


            CommandsNextConfiguration commandsConfig = new CommandsNextConfiguration
            {
                StringPrefixes = configJson.Prefix,
                EnableDms = false,
                DmHelp = false,
             //   PrefixResolver = CustomResolvePrefixAsync,
                EnableMentionPrefix = true,
                EnableDefaultHelp = false
                

            };
            CommandsTask = await Client.UseCommandsNextAsync(commandsConfig);
            foreach (KeyValuePair<int, CommandsNextExtension> entry in CommandsTask)
            {
                var cmd = entry.Value;
                
                cmd.RegisterCommands<Lava>();
                cmd.CommandExecuted += async (s, e) => { await OnCommandExecuted(s, e); }; ;
                cmd.CommandErrored += async (s, e) => { await OnCommandError(s, e); }; ;
            }
            //Error log fehlt
            InteractivityConfiguration icfg = new InteractivityConfiguration
            {
                Timeout = TimeSpan.FromMinutes(1)
            };

            await Client.UseInteractivityAsync(icfg);


            LavalinkConfiguration lcfg = new LavalinkConfiguration
            {
                SocketEndpoint =  new ConnectionEndpoint(configJson.LavalinkServerIP, configJson.LavalinkServerPort),
                RestEndpoint = new ConnectionEndpoint(configJson.LavalinkServerIP, configJson.LavalinkServerPort),
                Password = configJson.LavalinkServerPassword
            };
            
            await Client.StartAsync().ConfigureAwait(false);
            Client.Logger.LogInformation(new EventId(7777, "LavalinkStartup"), $"Try to connect to {configJson.LavalinkServerIP}:{configJson.LavalinkServerPort}");
            var LavaTask = Client.UseLavalinkAsync();
            foreach (KeyValuePair<int, LavalinkExtension> entry in LavaTask.Result)
            {
                var l = entry.Value;
                var la = await l.ConnectAsync(lcfg);
                la.LavalinkSocketErrored += async (s, e) => { await LavaLinkSocketError(s, e, Client); }; ;
                l.NodeDisconnected += async (s, e) => { await LavaLinkNodeDisconnect(s, e); }; ;
            }
            await Task.Delay(-1);

        }

        private Task OnMessageReactionAdded(DiscordClient s, MessageReactionAddEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task LavaLinkNodeDisconnect(LavalinkNodeConnection s, NodeDisconnectedEventArgs e)
        { 
            return Task.CompletedTask;
        }

        private Task LavaLinkSocketError(LavalinkNodeConnection s, SocketErrorEventArgs e, DiscordShardedClient client)
        {
            client.Logger.LogCritical(new EventId(7777, "LavaLinkSocketError"), $"Lavalink Socket Error {e.Exception.Message}");
            return Task.CompletedTask;
        }

        private async Task OnCommandError(CommandsNextExtension s, CommandErrorEventArgs e)
        {
            if (e.Exception is ArgumentException && e.Context.RawArgumentString == String.Empty)
            {
                return;
            }
            
            e.Context.Client.Logger.LogInformation(new EventId(7777, "CommandError"), $"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message ?? "<no message>"}");
            Console.WriteLine(e.Exception.StackTrace);
            if (e.Exception is ChecksFailedException)
            {

                var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");

                var embed = new DiscordEmbedBuilder
                {
                    Title = $"Zugriff verweigert {emoji}",
                    Description = $"Du oder der Bot hat nicht die nötigen Rechte um den Befehl auszuführen!",
                    Color = DiscordColor.Red
                   
                };
                try
                {
                    await e.Context.RespondAsync(embed: embed);
                }
                catch { }
               
            }
        }

        private Task OnCommandExecuted(CommandsNextExtension s, CommandExecutionEventArgs e)
        {
            e.Context.Client.Logger.LogInformation(new EventId(7777, "CommandExecuted"), $"{e.Context.User.Username} successfully executed '{e.Command.QualifiedName}'");
            return Task.CompletedTask;
        }

        private Task OnClientError(DiscordClient s, ClientErrorEventArgs e)
        {
            s.Logger.LogCritical(new EventId(7777, "ClientError"), $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}");
            return Task.CompletedTask;
        }
        //GUILD COUNT 
        private Task OnGuildCreated(DiscordClient s, GuildCreateEventArgs e)
        {
            
            s.Logger.LogInformation(new EventId(7777, "GuildCreated"), $"Guild available: {e.Guild.Name}");
            GuildCount++;
            // await e.Client.UpdateStatusAsync(new DiscordActivity($"auf {GuildCount} Servern und {e.Client.ShardCount} Sharded Clients | Made by Lemix | In Development", ActivityType.Playing), UserStatus.DoNotDisturb);
            return Task.CompletedTask;
        }
        private Task OnGuildAvailable(DiscordClient s, GuildCreateEventArgs e)
        {
            s.Logger.LogInformation(new EventId(7777, "GuildAvailable"), $"Guild available: {e.Guild.Name}");
            GuildCount++;
            return Task.CompletedTask;
        }
        private Task OnGuildDeleted(DiscordClient s, GuildDeleteEventArgs e)
        {
                s.Logger.LogInformation(new EventId(7777, "GuildDeleted"), $"Guild unavailable: {e.Guild.Name}");
                GuildCount--;
            return Task.CompletedTask;
        }

        private async Task<Task> OnClientReady(DiscordClient s, ReadyEventArgs e)
        {
            try
            {
                await s.UpdateCurrentUserAsync("Crowmusic");
                s.Logger.LogInformation(new EventId(7777, "ClientReady"), "Client is ready to process events");
                // await e.Client.UpdateStatusAsync(new DiscordActivity($"auf {GuildCount} Servern und auf {e.Client.ShardCount} Sharded Clients | Made by Lemix | In Development", ActivityType.Playing), UserStatus.DoNotDisturb);
                await Task.Factory.StartNew(() => UpdateStatus(s, e));
                
            }
            catch (Exception e1)
            {
                Console.WriteLine(e1);
            }
            return Task.CompletedTask;

        }
        private async Task<Task> UpdateStatus(DiscordClient s, ReadyEventArgs e)
        {
            try
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                DateTime buildDate = new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.Revision * 2);
                int i = 0;
                await Task.Delay(500);
                var guild = CommandsTask.Values.First().Client.GetGuildAsync(696753707479597086);
                var chn = await guild.Result.GetMemberAsync(267645496020041729);
                DiscordEmbedBuilder BootedEmbed = new DiscordEmbedBuilder
                {
                    Title = "Bot succesfully booted!",
                    Description = $"Botname: `{s.CurrentUser.Username}`\nId: `{s.CurrentUser.Id}`\nDSharpPlus Version: `{s.VersionString}`\nVersion: `{version}`\nBuild Date: `{buildDate}`", 
                    Color = DiscordColor.DarkGreen
                };
                BootedEmbed.WithFooter("Programmed by Lemix | Powered by DSharpPlus");
                BootedEmbed.WithThumbnail(s.CurrentUser.AvatarUrl);
                var msg = await chn.SendMessageAsync("", embed: BootedEmbed);
                var ctx = CommandsTask.Values.First().CreateContext(msg, "!", CommandsTask.Values.First().RegisteredCommands.ToList().Find(x => x.Key.Equals("init", StringComparison.OrdinalIgnoreCase)).Value);
                await CommandsTask.Values.First().ExecuteCommandAsync(ctx);
                DateTime LastRefresh = DateTime.Now;
                int StatusCount = 0;
                while (true)
                {
                    StatusItem entry;
                    if (StatusCount >= configJson.StatusItems.Length)
                        StatusCount = 0;
                        entry = configJson.StatusItems[StatusCount];
                        StatusCount++;

                    await s.UpdateStatusAsync(new DiscordActivity($"{String.Format(entry.Text, GuildCount)}", entry.Activity), entry.StatusType);
                        
                        i = GuildCount;
                    await Task.Delay(configJson.StatusRefreshTimer);
                }
            }
            catch
            {
                return Task.CompletedTask;
            }
        }


        /*       private Task<int> CustomResolvePrefixAsync(DiscordMessage msg)
               {

                   msg.Channel.gui
                   var gld = msg.Channel.Guild;
                   if (gld == null)
                       return Task.FromResult(-1);

                   var gldId = (long)gld.Id;
                   var db = new DatabaseContext(this.ConnectionStringProvider);
                   var gpfix = db.Prefixes.SingleOrDefault(x => x.GuildId == gldId);
                   if (gpfix != null)
                   {
                       foreach (var pfix in gpfix.Prefixes)
                       {
                           var pfixLocation = msg.GetStringPrefixLength(pfix);
                           if (pfixLocation != -1)
                               return Task.FromResult(pfixLocation);
                       }

                       if (gpfix.EnableDefault != true)
                           return Task.FromResult(-1);
                   }

                   foreach (var pfix in this.Configuration.Discord.DefaultPrefixes)
                   {
                       var pfixLocation = msg.GetStringPrefixLength(pfix);
                       if (pfixLocation != -1)
                           return Task.FromResult(pfixLocation);
                   }

                   return Task.FromResult(-1);
               }*/
    }
}
