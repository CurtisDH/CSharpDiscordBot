using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DiscordBot.Modules
{
    public class General : ModuleBase
    {
        private static readonly HttpClient Client = new HttpClient();
        private static readonly HashSet<string> TempFiles = new HashSet<string>();

        [Command("IgnoreChannel")]
        public async Task IgnoreChannel(string id)
        {
            Program.DebugPrint("IgnoreChannel");
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
            Program.DebugPrint($"PardonChannel:{id}");
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

        [Command("ipv4")]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        public async Task GetIPV4()
        {
            var host = await Dns.GetHostEntryAsync(Dns.GetHostName());
            var messageList = new List<IUserMessage>();
            messageList.Add(Context.Message);
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily != AddressFamily.InterNetwork) continue;
                
                var msg = await Context.Channel.SendMessageAsync(
                    $"Found address: {ip}");
                messageList.Add(msg);

            }

            await Task.Delay(6000);
            await (Context.Channel as SocketTextChannel).DeleteMessagesAsync(messageList.ToArray());
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
                Program.DebugPrint(channel.Id.ToString());
                if (channel.Id.ToString() == id)
                {
                    return true;
                }
            }

            var errorMsg = await Context.Channel.SendMessageAsync($"Invalid ID:'{id}'");
            Program.DebugPrint($"IsChannelValid:: Invalid ID:'{id}'");
            await DeleteMessage(errorMsg, 5000);
            await DeleteMessage(Context.Message, 0);
            return false;
        }
    }
}