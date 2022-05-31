using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Swan;
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
            string fileName = "audio.tmp";
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
                fileName = "audio";
                Program.Print($"Search successful URL:{url}");
            }

            await General.DeleteMessage(Context.Message, 0);
            var outputDir = Path.Combine(AppContext.BaseDirectory, Context.Guild.Id.ToString(), "media", fileName);
            if (File.Exists(outputDir))
            {
                File.Delete(outputDir);
            }

            if (videoInfo == null)
            {
                //stripping the original args as the URL should already be set at this point. 
                //This prevents errors if the user adds text at the end of a url
                args = new[] { url };
                videoInfo = await GetVideoInfoFromSearchTerm(args);
            }

            var embeddedMessage = GetEmbeddedMessageFromVideoInfo(videoInfo);
            await Context.Channel.SendMessageAsync("", false, embeddedMessage);

            //https://github.com/yt-dlp/yt-dlp 
            Console.WriteLine("downloading audio");
            await DownloadAudio("yt-dlp", url, outputDir, "worstaudio", fileName);
            Console.WriteLine("connecting to voice");
            await JoinVoiceChannel();
            //TODO bot joins and leave immediately?? maybe it needs a task or something to retain?
        }

        [Command("join", RunMode = RunMode.Async)]
        private async Task JoinVoiceChannel()
        {
            var voiceChannel = (Context.User as IVoiceState).VoiceChannel;
            if (voiceChannel == null)
            {
                Console.WriteLine("Voice channel is null. Returning");
                return;
            }
            
            Console.WriteLine("voice channel is not null");
            var audioClient = await voiceChannel.ConnectAsync();
            await SendAsync(audioClient,
                Path.Combine(AppContext.BaseDirectory, Context.Guild.Id.ToString(), "media", "audio.mp3"));
            await Task.Delay(5000);
        }

        private async Task SendAsync(IAudioClient client, string path)
        {
            // Create FFmpeg using the previous example
            using (var ffmpeg = CreateStream(path))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = client.CreatePCMStream(AudioApplication.Mixed))
            {
                try
                {
                    await output.CopyToAsync(discord);
                }
                finally
                {
                    await discord.FlushAsync();
                }
            }
        }

        private Process CreateStream(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }

        public static async Task DownloadAudio(string processName, string url, string outputDir, string quality,
            string fileName = "temp")
        {
            outputDir = $"\"{outputDir}.tmp\"";
            var arguments = $"-f {quality} {url} -o {outputDir}";
            Console.WriteLine(processName + " " + arguments);
            var processInfo = new ProcessStartInfo(processName, arguments);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;


            var process = Process.Start(processInfo);
            
            await process.WaitForExitAsync();
            Console.WriteLine("ExitCode: {0}", process.ExitCode);
            Program.Print($":{process.StandardError.ReadToEnd()}");
            if (process.ExitCode == 0)
            {
                await ConvertToMp3(outputDir, fileName);
            }

            process.Close();
        }

        private static async Task ConvertToMp3(string filePath, string fileName)
        {
            Program.Print("Converting to Mp3");

            string directory = Path.GetDirectoryName(filePath);
            string output = $"{directory}/{fileName}.mp3\"";
            var arguments = $"-i {filePath} -acodec mp3 {output}";
            var processInfo = new ProcessStartInfo("ffmpeg", arguments);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;
            var process = Process.Start(processInfo);
            await process.WaitForExitAsync();
            Console.WriteLine("ExitCode: {0}", process.ExitCode);
            Program.Print($":{process.StandardError.ReadToEnd()}");
            if (process.ExitCode == 0)
            {
                // Dunno why this doesn't run
                File.Delete(filePath);
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
                    var video = (YoutubeVideo)responseResult;
                    return video;
                }

                return null;
            }
        }
    }
}