using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace DiscordBot
{
    public class StartupService
    {
        public static IServiceProvider Provider;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;

        public StartupService(IServiceProvider provider,
            DiscordSocketClient client,
            CommandService commands,
            IConfigurationRoot config)
        {
            Provider = provider;
            _client = client;
            _commands = commands;
            _config = config;
        }

        public async Task StartAsync()
        {
            string token = _config["tokens:discord"];
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Discord token empty..." +
                                  " Please configure the _config.yml");
                return;
            }

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), Provider);
        }
    }
}