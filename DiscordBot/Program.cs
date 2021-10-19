using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace DiscordBot
{
    class Program
    {
        public static async Task Main(string[] args) => await Startup.RunAsync(args);
        public static string serverConfigName = "config.json";
        public static void CreateJsonObject(string path,string json)
        {
            File.WriteAllText(path,json);
        }

        public static ServerConfig GetConfigFromServerId(string id)
        {
            string path = $"{AppContext.BaseDirectory}/{id}/{serverConfigName}";
            if (File.Exists(path))
            {
                string jsonString = File.ReadAllText(path);
                if (String.IsNullOrEmpty(jsonString))
                {
                    Print("");
                }
                var cfg = JsonSerializer.Deserialize<ServerConfig>(jsonString);
                return cfg;
            }
            Print($"Config file not found at:{path}");
            
            Print($"Creating one...");
            CreateConfigFromServerId(id);
            return null;
        }
        public static void Print(string msg)
        {
            Console.WriteLine($"{DateTime.Now} {msg}");
        }
        private static void CreateConfigFromServerId(string id)
        {
            string directory = AppContext.BaseDirectory;
            var configName = serverConfigName;
            var guildDirectory = directory + "/" + id;
            string configDirectory = $"{guildDirectory}/{configName}";
            if (!Directory.Exists(guildDirectory))
            {
                Program.Print($"Directory not found.. Creating Directory at:{guildDirectory}");
                Directory.CreateDirectory(guildDirectory);
            }
            Program.Print($"Creating config at directory: {configDirectory}");
            ServerSideConfigSetup(configDirectory);
        }
        public static void ServerSideConfigSetup(string path)
        {
            if (File.Exists(path))
            {

                Print($"File exists{path}");
                return;
            }
            var config = new ServerConfig
            {
                Prefix = "!", Commands = new[] {"Test", "Test123"},
                IgnoreListChannelIDs = new string[] {"899202882769915909", "899202882769915909"}
            };
            var options = new JsonSerializerOptions {WriteIndented = true};
            string json = JsonSerializer.Serialize(config, options);
            CreateJsonObject(path,json);
            Print($"Successfully created config at:{path}");
            return;
        }

        public static void UpdateServerConfig(string serverId,ServerConfig config)
        {
            var directory = AppContext.BaseDirectory;
            var configName = serverConfigName;
            var path = $"{directory}/{serverId}/{configName}";
            var options = new JsonSerializerOptions {WriteIndented = true};
            string json = JsonSerializer.Serialize(config, options);
            CreateJsonObject(path,json);
            Print($"Successfully updated config at:{path}");
            return;
        }
        
        public static void CreateDirectoryAndConfigForConnectedServers(DiscordSocketClient client)
        {
            Program.Print("Checking Directory for new servers");
            string directory = AppContext.BaseDirectory;
            string configName = Program.serverConfigName;
            foreach (var guild in client.Guilds)
            {
                string guildDirectory = $"{directory}/{guild.Id}";
                string configDirectory = $"{guildDirectory}/{configName}";
                if (Directory.Exists(guildDirectory))
                {
                    Program.Print($"Found Existing Directory:{guildDirectory}");
                    Program.Print($"Checking for config");
                    if (File.Exists(configDirectory))
                    {
                        Program.Print($"Found Existing Config:{configDirectory}");
                        continue;
                    }

                    Program.Print($"Config not found, creating config...");
                    Program.ServerSideConfigSetup(configDirectory);
                    Program.Print($"Config created at:{configDirectory}");
                    continue;
                }

                Program.Print($"Directory not found.. Creating Directory at:{guildDirectory}");
                Directory.CreateDirectory(guildDirectory);
                Program.Print($"Creating config at directory: {configDirectory}");
                Program.ServerSideConfigSetup(configDirectory);
            }

            Program.Print("Done");
        }


        public static char SetupPrefix(IConfigurationRoot config)
        {
            Program.Print("Setting up prefix...");
            string prefixString = config["prefix"];
            if (String.IsNullOrEmpty(prefixString))
            {
                Program.Print("Couldn't find prefix returning default '!'");
                return '!';
            }
            Program.Print("Found prefix returning:"+prefixString);
            return char.Parse(prefixString);
        }
        
        
    }
    
    
}