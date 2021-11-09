using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

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

        [Command("play")] //TODO allow for search terms.
        public async Task Play(string url)
        {
            //Youtube throttles downloads to 50-70kb/s.Choosing worst quality until I discover a more elaborate workaround
            var fileName = "audio.mp3";
            var outputDir = Path.Combine(AppContext.BaseDirectory, Context.Guild.Id.ToString(), "media", fileName);
            if (File.Exists(outputDir))
            {
                File.Delete(outputDir);
            }

            var processInfo = new ProcessStartInfo("youtube-dl.exe", $"-f worstaudio {url} -o {outputDir}");
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            Program.Print("Processing..");
            var msg = await Context.Channel.SendMessageAsync("Processing request...");

            var process = Process.Start(processInfo);

            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                Program.Print($"output Server:{Context.Guild.Name}>>{e.Data}");
            process.BeginOutputReadLine();
            Program.Print("Done..");
            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                Program.Print("error>>" + e.Data);
            process.BeginOutputReadLine();

            process.WaitForExit();
            await DeleteMessage(msg, 0);
            msg = await Context.Channel.SendMessageAsync("Completed");
            await DeleteMessage(msg, 2500);

            Console.WriteLine("ExitCode: {0}", process.ExitCode);
            process.Close();
        }

        private string GetIdFromURL(string url)
        {
            var response = url.Split("https://www.youtube.com/watch?v=");

            if (response.Length > 1)
            {
                Console.WriteLine(response[1]);
                return response[1];
            }

            Console.WriteLine(response[0]);
            return response[0];
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