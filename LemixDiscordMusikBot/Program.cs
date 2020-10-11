using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace LemixDiscordMusikBot
{
    class Program
    {

        public static void Main(string[] args)
        {
            
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            DateTime buildDate = new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.Revision * 2);
            string Version = $"{version}p \n   Build Date: ({buildDate})";
            IEnumerable<string> DefaultConfig = new string[] { "{\"token\": \"\",\"prefix\": [\"!\", \"?\"],\"LavalinkServerIP\": \"127.0.0.1\",\"LavalinkServerPort\": 2333,\"LavalinkServerPassword\": \"youshallnotpass\",\"DatabaseHostname\": \"localhost\",\"DatabaseDbName\": \"dbot\",\"DatabaseUid\": \"root\",\"DatabasePassword\": \"pwd\",\"DatabasePort\": 3306, \"StatusItems\": [ {\"Text\":\"auf {0} Servern | Made by Lemix\", \"Activity\":1,\"StatusType\":\"dnd\"}, {\"Text\":\"asdfcuzghuzhgodsaf\",\"Activity\":0,\"StatusType\":\"dnd\"}],\"StatusRefreshTimer\" : 120000,\"NoSongPicture\" : @\"https://www.bund.net/fileadmin/user_upload_bund/bilder/tiere_und_pflanzen/bedrohte_arten/fischotter.jpg\",\"DefaultVolume\" : 10}" };
            
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
                if (!File.Exists("config.json"))
                {                   
                    Console.ForegroundColor = ConsoleColor.Red;
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        File.WriteAllLines("config.json", DefaultConfig);
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
                        Console.WriteLine("Unbekannter Fehler beim Lesen der Config! "+ e);
                        Console.WriteLine("Press any Key to close the Window");
                        Console.ReadKey();
                    }
                }
                else
                {
                    bot.StartAsync().GetAwaiter().GetResult();
                }
                
            }
            catch(Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.WriteLine("Press any Key to close the Window");
                Console.ReadKey();
            }
           
        }
    }
}
