using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.Commands;
using YoutubeSearchApi.Net;
using YoutubeSearchApi.Net.Backends;
using YoutubeSearchApi.Net.Objects;

namespace DiscordBot.Modules
{
    public class AudioModule : ModuleBase
    {
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

            await General.DeleteMessage(Context.Message, 0);
            var fileName = "audio.tmp";
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
            Console.WriteLine("downloading audio");
            await DownloadAudio("yt-dlp", url, outputDir, "worstaudio");
            Console.WriteLine("connecting to voice");
            await JoinVoiceChannel();
        }
        [Command("join")]
        private async Task JoinVoiceChannel()
        {
            var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel;
            if (voiceChannel != null)
            {
                Console.WriteLine("voice channel is not null");
                var audioClient = await voiceChannel.ConnectAsync();
            }
        }

        public static async Task DownloadAudio(string processName, string url, string outputDir, string quality)
        {
            var arguments = $"-f {quality} {url} -o {outputDir}";
            Console.WriteLine(arguments);
            var processInfo = new ProcessStartInfo(processName, arguments);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            var process = Process.Start(processInfo);

            //These show the progress of the download and any errors that occur.
            //However, it also stops the process from reaching the exit code.
            // process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            //     Program.Print($"output Server:{Context.Guild.Name}>>{e.Data}");
            // process.BeginOutputReadLine();
            // process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            //     Program.Print("error>>" + e.Data);
            // process.BeginOutputReadLine();

            await process.WaitForExitAsync();
            Console.WriteLine("ExitCode: {0}", process.ExitCode);
            if (process.ExitCode == 0)
            {
                await ConvertToMp3(outputDir);
            }

            process.Close();
        }

        private static async Task ConvertToMp3(string filePath)
        {
            Program.Print("Converting to Mp3");
            string fileName = string.Empty;
            string directory = Path.GetDirectoryName(filePath);

            if (File.Exists(filePath))
            {
                fileName = Path.GetFileNameWithoutExtension(filePath);
            }

            string output = $"{directory}/{fileName}.mp3";
            var arguments = $"-i {filePath} -acodec mp3 {output}";
            Console.WriteLine(arguments);
            var processInfo = new ProcessStartInfo("ffmpeg", arguments);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;
            var process = Process.Start(processInfo);
            await process.WaitForExitAsync();
            Console.WriteLine("ExitCode: {0}", process.ExitCode);
            if (process.ExitCode == 0)
            {
                File.Delete(filePath);
                //Floods the console too much.
                //Console.WriteLine($"Successfully converted file from: {filePath} to: {output}");
                Program.Print($"Successfully downloaded and converted {fileName}");
            }

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

        public static async Task<YoutubeVideo>
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
    }
}