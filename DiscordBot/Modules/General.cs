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

        [Command("play")] //TODO allow for search terms.
        public async Task Play(params string[] args)
        {
            string url = args[0];
            YoutubeVideo videoInfo = null;
            if (!args[0].Contains("https://www.youtube.com/watch?v="))
            {
                Program.Print("No URL found. Attempting to search..");
                videoInfo = await GetVideoInfoFromSearchTerm(args);
                if (videoInfo == null)
                {
                    await Context.Channel.SendMessageAsync($"No search results found");
                    Program.Print("No search results found returning..");
                    return;
                }

                url = videoInfo.Url;
                Program.Print($"Search successful URL:{url}");
            }

            var fileName = "audio.mp3";
            var outputDir = Path.Combine(AppContext.BaseDirectory, Context.Guild.Id.ToString(), "media", fileName);
            if (File.Exists(outputDir))
            {
                File.Delete(outputDir);
            }

            if (videoInfo == null)
            {
                //stripping the original args as the URL should already be set at this point. 
                //This prevents errors if the user adds text at the end of a url
                args = new[] {url};
                videoInfo = await GetVideoInfoFromSearchTerm(args);
            }

            var embeddedMessage = GetEmbeddedMessageFromVideoInfo(videoInfo);
            await Context.Channel.SendMessageAsync("", false, embeddedMessage);

            //https://github.com/yt-dlp/yt-dlp 
            DownloadAudio("yt-dlp", url, outputDir);
        }

        private void DownloadAudio(string processName, string url, string outputDir)
        {
            var arguments = $"-f worstaudio {url} -o {outputDir}";
            Console.WriteLine(arguments);
            var processInfo = new ProcessStartInfo(processName, arguments);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            var process = Process.Start(processInfo);

            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                Program.Print($"output Server:{Context.Guild.Name}>>{e.Data}");
            process.BeginOutputReadLine();
            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                Program.Print("error>>" + e.Data);
            process.BeginOutputReadLine();

            process.WaitForExit();
            Console.WriteLine("ExitCode: {0}", process.ExitCode);
            process.Close();
        }

        private Embed GetEmbeddedMessageFromVideoInfo(YoutubeVideo videoInfo)
        {
            var eb = new EmbedBuilder();
            eb.AddField($"Video:", videoInfo.Title, true);
            eb.AddField($"Duration:", videoInfo.Duration, true);
            eb.AddField($"URL:", videoInfo.Url, true);
            eb.WithThumbnailUrl(videoInfo.ThumbnailUrl);
            eb.WithColor(Color.Green);
            return eb.Build();
        }

        private async Task<YoutubeVideo>
            GetVideoInfoFromSearchTerm(string[] searchTerms) //TODO write own search package
        {
            //https://www.youtube.com/results?search_query=search+test
            using (var httpClient = new HttpClient())
            {
                string searchTerm = String.Empty;
                foreach (var s in searchTerms)
                {
                    searchTerm += s + " ";
                }

                DefaultSearchClient client = new DefaultSearchClient(new YoutubeSearchBackend());
                var responseObject = await client.SearchAsync(httpClient, searchTerm, maxResults: 1);
                foreach (var responseResult in responseObject.Results)
                {
                    var video = (YoutubeVideo) responseResult;
                    return video;
                }

                return null;
            }
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