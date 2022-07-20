using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Modules;
using Microsoft.Extensions.Configuration;

namespace DiscordBot
{
    public class CommandHandler
    {
        private static IServiceProvider _provider;
        private static DiscordSocketClient _client;
        private static CommandService _commands;
        private static IConfigurationRoot _config;
        private char _prefix;

        public CommandHandler(DiscordSocketClient client,
            CommandService commands,
            IConfigurationRoot config,
            IServiceProvider provider)
        {
            _provider = provider;
            _client = client;
            _commands = commands;
            _config = config;

            _client.Ready += OnReady;
            _client.MessageReceived += OnMessageReceived;
        }

        private async Task OnMessageReceived(SocketMessage msg)
        {
            var message = (SocketUserMessage) msg;
            if (message.Author.IsBot)
            {
                return;
            }

            char prefix = _prefix;
            var context = new SocketCommandContext(_client, message);
            ServerConfig config = Program.GetConfigFromServerId(context.Guild.Id.ToString());
            //Setup prefix from the config file
            if (!String.IsNullOrEmpty(config.Prefix))
            {
                prefix = Char.Parse(config.Prefix);
            }

            int pos = 0;
            if (message.HasCharPrefix(prefix, ref pos))
            {
                if (IsChannelValid(config, message))
                {
                    Program.DebugPrint($"Valid Channel");
                    var result = await _commands.ExecuteAsync(context, pos, _provider);
                    if (!result.IsSuccess)
                    {
                        var errorMessage = await context.Channel.SendMessageAsync("Error:" + result.ErrorReason);
                        await General.DeleteMessage(errorMessage, 5000);
                        await General.DeleteMessage(message, 0);
                    }

                    return;
                }

                Program.DebugPrint($"Ignoring Channel:{message.Channel.Name} from server{context.Guild.Name}");
            }
        }

        private bool IsChannelValid(ServerConfig cfg, SocketMessage message)
        {
            foreach (var channel in cfg.IgnoreListChannelIDs)
            {
                if (channel == message.Channel.Id.ToString())
                {
                    return false;
                }
            }

            return true;
        }

        private Task OnReady()
        {
            Program.DebugPrint($"Connected as {_client.CurrentUser.Username}");
            Program.CreateDirectoryAndConfigForConnectedServers(_client);
            _prefix = Program.SetupPrefix(_config);
            Program.DebugPrint("Finished setup");
            return Task.CompletedTask;
        }
    }
}