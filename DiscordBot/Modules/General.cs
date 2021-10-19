using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DiscordBot.Modules
{
    public class General : ModuleBase
    {
        [Command("IgnoreChannel")]
        public async Task IgnoreChannel(string id)
        {
            Program.Print("IgnoreChannel");
            var channelId = Context.Message.Channel.Id.ToString();
            if (!String.IsNullOrEmpty(id))
            {
                var channels = await Context.Guild.GetChannelsAsync();
                if (!await IsChannelValid(id,channels))
                {
                    return;
                }
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

        [Command("pardonChannel")]
        public async Task PardonChannel(string id)
        {
            Program.Print($"PardonChannel:{id}");
            if (String.IsNullOrEmpty(id))
            {
                return;
            }

            if (await IsChannelValid(id,await Context.Guild.GetChannelsAsync()))
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
            }
            
        }

        public static async Task DeleteMessage(IUserMessage msg, int msDelay)
        {
            await Task.Delay(msDelay);
            await msg.DeleteAsync();
        }

        private async Task<bool> IsChannelValid(string id,IReadOnlyCollection<IGuildChannel> channels)
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