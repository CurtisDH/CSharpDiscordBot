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
        private static bool _bCurrentlyPlayingMedia;
        private static AudioOutStream _audioOutStream = null;

        [Command("stop")]
        public async Task Stop()
        {
            _audioOutStream.Clear();
        }

        [Command("play")] //TODO allow for search terms.
        public async Task Play(params string[] args)
        {
            Console.WriteLine("Entry");
            Console.WriteLine(_bCurrentlyPlayingMedia);
            while (_bCurrentlyPlayingMedia)
            {
                Program.Print("Song playing, waiting.");
                await Task.Delay(3000);
            }

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
                args = new[] { url };
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

        // WARNING CONNECTION IS ONLY SUSTAINED IF THESE DLL'S ARE INCLUDED
        // https://github.com/discord-net/Discord.Net/tree/dev/voice-natives
        // libopus needs to be renamed to opus
        [Command("join")]
        private async Task JoinVoiceChannel()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, Context.Guild.Id.ToString(), "media", "audio.mp3");
            var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel;
            if (voiceChannel != null)
            {
                Console.WriteLine("voice channel is not null");
                var audioClient = await voiceChannel.ConnectAsync();
                if (File.Exists(dir))
                    await SendAsync(audioClient, dir);
            }
        }

        [Command("leave")]
        private async Task LeaveVoiceChannel()
        {
            var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel;
            if (voiceChannel != null)
            {
                Console.WriteLine("voice channel is not null");
                _audioOutStream.Clear();
                _bCurrentlyPlayingMedia = false;
                await voiceChannel.DisconnectAsync();
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


        public async Task DownloadAudio(string processName, string url, string outputDir, string quality)
        {
            var arguments = $"-f {quality} {url} -o \"{outputDir}\"";
            Console.WriteLine(arguments);
            var processInfo = new ProcessStartInfo(processName, arguments);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            var process = Process.Start(processInfo);

            await process.WaitForExitAsync();
            Console.WriteLine("ExitCode: {0}", process.ExitCode);
            if (process.ExitCode == 0)
            {
                await ConvertToMp3(outputDir);
            }

            process.Close();
        }

        private async Task ConvertToMp3(string filePath)
        {
            Program.Print("Converting to Mp3");
            Program.Print(filePath);
            string fileName = string.Empty;
            string directory = Path.GetDirectoryName(filePath);

            if (File.Exists(filePath))
            {
                fileName = Path.GetFileNameWithoutExtension(filePath);
            }

            string output = $"\"{directory}/{fileName}.mp3\"";
            if (File.Exists(output))
            {
                _audioOutStream?.Clear();
                File.Delete(output);
            }

            var arguments = $"-i \"{filePath}\" -acodec mp3 {output}";
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
                    var video = (YoutubeVideo)responseResult;
                    return video;
                }

                return null;
            }
        }

        private async Task SendAsync(IAudioClient client, string path)
        {
            _bCurrentlyPlayingMedia = true;
            Console.WriteLine(_bCurrentlyPlayingMedia);
            using (var ffmpeg = CreateStream(path))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = client.CreatePCMStream(AudioApplication.Mixed))
            {
                _audioOutStream = discord;
                try
                {
                    await output.CopyToAsync(discord);
                }
                finally
                {
                    await discord.FlushAsync();
                    _bCurrentlyPlayingMedia = false;
                    Console.WriteLine(_bCurrentlyPlayingMedia);
                }
            }
        }
    }
}