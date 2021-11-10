using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using YoutubeSearchApi.Net;
using YoutubeSearchApi.Net.Backends;
using YoutubeSearchApi.Net.Objects;

namespace DiscordBot.Modules
{
    public class General : ModuleBase
    {
        private static readonly HttpClient Client = new HttpClient();
        private static readonly HashSet<string> TempFiles = new HashSet<string>();

        [Command("IgnoreChannel")]
        public async Task IgnoreChannel(string id)
        {
            Program.Print("IgnoreChannel");
            var channelId = Context.Message.Channel.Id.ToString();
            if (!String.IsNullOrEmpty(id))
            {
                var channels = await Context.Guild.GetChannelsAsync();
                if (!await IsChannelValid(id, channels))
                {
                    return;
                }

                channelId = id;
            }

            if (String.IsNullOrEmpty(channelId))
            {
                return;
            }

            var config = Program.GetConfigFromServerId(Context.Guild.Id.ToString());
            var ignoreIDs = config.IgnoreListChannelIDs.ToList();

            ignoreIDs.Add(channelId);


            var distinctIds = ignoreIDs.Distinct();
            var distinctIdsArray = distinctIds.ToArray();

            config.IgnoreListChannelIDs = distinctIdsArray;
            Program.UpdateServerConfig(Context.Guild.Id.ToString(), config);
            Console.WriteLine($"Modified ignore list for Guild:{Context.Guild.Name}");
            var msg =
                await Context.Channel.SendMessageAsync($"Added:{Context.Message.Channel.Name} to ignore list");
            await DeleteMessage(msg, 2500);
            await DeleteMessage(Context.Message, 0);
        }

//TODO CACHE THE CONFIG ONCE USED 
        [Command("pardonChannel")]
        public async Task PardonChannel(string id)
        {
            Program.Print($"PardonChannel:{id}");
            if (String.IsNullOrEmpty(id))
            {
                return;
            }

            if (await IsChannelValid(id, await Context.Guild.GetChannelsAsync()))
            {
                var config = Program.GetConfigFromServerId(Context.Guild.Id.ToString());
                var ignoreIDs = config.IgnoreListChannelIDs.ToList();

                ignoreIDs.Remove(id);


                var distinctIds = ignoreIDs.Distinct();
                var distinctIdsArray = distinctIds.ToArray();

                config.IgnoreListChannelIDs = distinctIdsArray;
                Program.UpdateServerConfig(Context.Guild.Id.ToString(), config);
                Console.WriteLine($"Modified ignore list for Guild:{Context.Guild.Name}");
                var msg = await Context.Channel.SendMessageAsync(
                    $"Removed:{Context.Message.Channel.Name} from ignore list");
                await DeleteMessage(msg, 2500);
                await DeleteMessage(Context.Message, 0);
                return;
            }
        }

        [Command("setPrefix")]
        public async Task SetPrefix(string prefix)
        {
            string guildId = Context.Guild.Id.ToString();
            var config = Program.GetConfigFromServerId(guildId);
            config.Prefix = prefix;
            char result;
            if (!Char.TryParse(config.Prefix, out result))
            {
                return;
            }

            Program.UpdateServerConfig(guildId, config);
            var message = await Context.Channel.SendMessageAsync($"Prefix successfully set to '{result}'");
            await DeleteMessage(message, 2500);
            await DeleteMessage(Context.Message, 0);
        }

        [Command("prune")]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        public async Task Prune(int amount)
        {
            var messages = await Context.Channel.GetMessagesAsync(amount + 1)
                .Flatten().ToArrayAsync();
            await (Context.Channel as SocketTextChannel).DeleteMessagesAsync(messages);
        }

        public static async Task DeleteMessage(IUserMessage msg, int msDelay)
        {
            await Task.Delay(msDelay);
            await msg.DeleteAsync();
        }

        private async Task<bool> IsChannelValid(string id, IReadOnlyCollection<IGuildChannel> channels)
        {
            foreach (var channel in channels)
            {
                Program.Print(channel.Id.ToString());
                if (channel.Id.ToString() == id)
                {
                    return true;
                }
            }

            var errorMsg = await Context.Channel.SendMessageAsync($"Invalid ID:'{id}'");
            Program.Print($"IsChannelValid:: Invalid ID:'{id}'");
            await DeleteMessage(errorMsg, 5000);
            await DeleteMessage(Context.Message, 0);
            return false;
        }
    }
}