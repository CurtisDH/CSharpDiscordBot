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
        private string token;

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
            token = _config["tokens:discord"];
            if (string.IsNullOrEmpty(token))
            {
                ConfigureToken();
            }

            await VerifyTokenAndLogin();
            await _client.StartAsync();
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), Provider);
        }

        private async Task VerifyTokenAndLogin()
        {
            bool verified = false;
            while (verified == false)
            {
                try
                {
                    await _client.LoginAsync(TokenType.Bot, token);
                    verified = true;
                    Console.WriteLine("Token accepted.");
                }
                catch
                {
                    Console.WriteLine("Invalid Token:please enter a valid token");
                    verified = false;
                    ConfigureToken();
                }
            }
        }

        private void ConfigureToken()
        {
            while (true)
            {
                Console.WriteLine("Enter discord bot token, or configure the '_config.yml' manually and restart");
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                token = input;
                break;
            }
        }
    }
}