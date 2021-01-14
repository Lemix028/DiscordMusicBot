using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace LemixDiscordMusikBot
{
    class Program
    {
        public static string[] Arguments;
        public static void Main(string[] args)
        {
            Arguments = args;

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            DateTime buildDate = new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.Revision * 2);
            string Version = $"{version} \n   Build Date: ({buildDate})";

            Config NewConfig = new Config();
            NewConfig.Token = "";
            NewConfig.Prefix = new string[] { "!", "?" };
            NewConfig.LavalinkServerIP = "127.0.0.1";
            NewConfig.LavalinkServerPassword = "youshallnotpass";
            NewConfig.LavalinkServerPort = 2333;
            NewConfig.BotUsername = "TotallyLemixBot";
            NewConfig.DatabaseHostname = "localhost";
            NewConfig.DatabaseDbName = "";
            NewConfig.DatabaseUid = "root";
            NewConfig.DatabasePassword = "secretpassword";
            NewConfig.DatabasePort = 3306;
            NewConfig.StatusRefreshTimer = 600;
            NewConfig.StatusItems = new StatusItem[] { new StatusItem() { Activity = DSharpPlus.Entities.ActivityType.Playing, StatusType = DSharpPlus.Entities.UserStatus.Online, Text = "Placeholder text here" } };
            NewConfig.DefaultVolume = 10;
            NewConfig.NoSongPicture = "https://www.bund.net/fileadmin/user_upload_bund/bilder/tiere_und_pflanzen/bedrohte_arten/fischotter.jpg";
            NewConfig.BannerPicture = "https://www.fotor.com/blog/wp-content/uploads/2017/09/1-2.jpg";
            NewConfig.BotChannelRebuild = false;

            Console.Title = $"Discordbot by Lemix {Version}";

            DiscordBot bot = new DiscordBot();
            try
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write(
@$" ______                 _____        
 ___  / ____________ ______(_)___  __
 __  /  _  _ \_  __ `__ \_  /__  |/_/
 _  /___/  __/  / / / / /  / __>  <  
 /_____/\___//_/ /_/ /_//_/  /_/|_|
        Programmed by Lemix
       Powered by DSharpPlus
      Version: {Version}
        

");
                Console.ForegroundColor = ConsoleColor.Gray;
                if (args.Length != 0)
                {
                    if (File.Exists(args[0]))
                    {
                        bot.StartAsync().GetAwaiter().GetResult();
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Config konnte nicht gefunden werden!");
                        Console.WriteLine("Press any Key to close the Window");
                        Console.ReadKey();
                    }

                }
                else
                {
                    if (File.Exists("config.json"))
                    {
                        bot.StartAsync().GetAwaiter().GetResult();
                        return;
                    }
                }

                Console.ForegroundColor = ConsoleColor.Red;
                try
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    TextWriter writer = null;
                    try
                    {
                        writer = new StreamWriter("config.json", false);
                        writer.Write(JsonConvert.SerializeObject(NewConfig, Formatting.Indented));
                    }
                    finally
                    {
                        if (writer != null)
                            writer.Close();
                    }
                    Console.Write("New config file created!\nPlease change the default values in config.json and restart the bot.\n");
                    Console.WriteLine("Press any Key to close the Window");
                    Console.ReadKey();
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("Config konnte nicht gefunden werden!");
                    Console.WriteLine("Press any Key to close the Window");
                    Console.ReadKey();
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("Config kann nicht gelesen werden! Unzureichende Rechte!");
                    Console.WriteLine("Press any Key to close the Window");
                    Console.ReadKey();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unbekannter Fehler beim Lesen der Config! " + e);
                    Console.WriteLine("Press any Key to close the Window");
                    Console.ReadKey();
                }

            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.WriteLine("Press any Key to close the Window");
                Console.ReadKey();
            }

        }
    }
}
