using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }

        public Startup(string[] args)
        {
            string dir = AppContext.BaseDirectory + "/_config.yml";
            if (!File.Exists(dir))
            {
                Program.Print("Warning!! Config file doesn't exist.. Creating one:"+dir);
                File.WriteAllText(dir,"prefix: '!'" +
                                      "\n" +
                                      "tokens:\n" +
                                      "discord: --Ensure this line is indented");
            }
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("_config.yml");
            Configuration = configBuilder.Build();
        }
        
        public static async Task RunAsync(string[] args)
        {
            var bot = new Startup(args);
            await bot.RunAsync();
        }

        public async Task RunAsync()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            var provider = services.BuildServiceProvider();
            provider.GetRequiredService<CommandHandler>();

            await provider.GetRequiredService<StartupService>().StartAsync();
            await Task.Delay(-1);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = Discord.LogSeverity.Verbose,
                MessageCacheSize = 1000
            }))
                .AddSingleton(new CommandService(new CommandServiceConfig()
            {
                LogLevel = Discord.LogSeverity.Verbose,
                DefaultRunMode = RunMode.Async,
                CaseSensitiveCommands = false
            }))
                .AddSingleton<CommandHandler>()
                .AddSingleton<StartupService>()
                .AddSingleton(Configuration);
        }
    }

    public class ServerConfig
    {
        public string[] Commands { get; set; }
        public string Prefix { get; set; }
        public string[] IgnoreListChannelIDs { get; set; }
    }
}