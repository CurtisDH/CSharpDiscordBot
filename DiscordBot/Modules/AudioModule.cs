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
        private static Dictionary<ulong, bool> _bCurrentlyPlayingMedia = new();
        private static Dictionary<ulong, AudioOutStream> _audioOutStream = new();
        private static Dictionary<ulong, Queue<YoutubeVideo>> _musicQueue = new();

        // If two media are processed at the same time ffmpeg hangs and the bot refuses to play music
        private static bool _bProcessingMedia = false; // Such a horrible way this is gonna be so slow.

        [Command("stop")]
        public async Task Stop()
        {
            _audioOutStream[Context.Guild.Id].Clear();
            _bCurrentlyPlayingMedia[Context.Guild.Id] = false;
            General.DeleteMessage(Context.Message, 0);
        }

        [Command("queue")]
        public async Task ViewQueue()
        {
            General.DeleteMessage(Context.Message, 0);
            var eb = new EmbedBuilder();
            int counter = 1;
            eb.AddField("Total Queue Size", _musicQueue[Context.Guild.Id].Count);
            foreach (var song in _musicQueue[Context.Guild.Id])
            {
                eb.AddField($"Video:", counter + ": " + song.Title, false);
                eb.WithColor(Color.Purple);
                Program.DebugPrint(song.Title);
                counter++;
            }

            var msg = eb.Build();
            var message = await Context.Channel.SendMessageAsync("", false, msg);
            General.DeleteMessage(message, 10000);
        }

        [Command("skip")]
        public async Task Skip(params string[] args) //TODO bug if queue size is 1 when skipped bot leaves 
        {
            bool isNumeric = int.TryParse(args[0], out int selectedSongCount);
            IUserMessage msg = null;
            if (!isNumeric)
            {
                msg = await Context.Channel.SendMessageAsync($"Choice:'{args[0]}' is not numeric.");
                General.DeleteMessage(msg, 0);
                return;
            }

            if (selectedSongCount > _musicQueue[Context.Guild.Id].Count)
            {
                msg = await Context.Channel.SendMessageAsync($"Choice:'{selectedSongCount}' is out of range.");
                General.DeleteMessage(msg, 0);
                return;
            }

            msg = (await Context.Channel.SendMessageAsync($"Skipping {selectedSongCount} songs"));
            General.DeleteMessage(msg, 0);
            for (int i = 0; i < selectedSongCount; i++)
            {
                _musicQueue[Context.Guild.Id].Dequeue();
            }

            msg = (await Context.Channel.SendMessageAsync($"New Queue:"));
            General.DeleteMessage(msg, 0);
            await ViewQueue();
            _audioOutStream[Context.Guild.Id].Clear();
            _bCurrentlyPlayingMedia[Context.Guild.Id] = false;
            await JoinVoiceChannel();
            await General.DeleteMessage(Context.Message, 0);
        }

        [Command("play")]
        public async Task Play(params string[] args)
        {
            //TODO
            // Async download the entire queue starting with the initial song.
            // If songs are added to the queue download them so we can immediately play after finishing the song
            // delete played once they are removed from queue.
            if (args.Length == 0)
            {
                await JoinVoiceChannel();
                return;
            }

            if (!_bCurrentlyPlayingMedia.ContainsKey(Context.Guild.Id))
            {
                _bCurrentlyPlayingMedia.Add(Context.Guild.Id, false);
            }

            if (!_audioOutStream.ContainsKey(Context.Guild.Id))
            {
                _audioOutStream.Add(Context.Guild.Id, null);
            }

            if (!_musicQueue.ContainsKey(Context.Guild.Id))
            {
                _musicQueue.Add(Context.Guild.Id, new Queue<YoutubeVideo>());
            }

            string url = args[0];
            YoutubeVideo videoInfo = null;
            if (!args[0].Contains("https://www.youtube.com/watch?v="))
            {
                Program.DebugPrint("No URL found. Attempting to search..");
                videoInfo = await GetVideoInfoFromSearchTerm(args);
                if (videoInfo == null)
                {
                    await Context.Channel.SendMessageAsync($"No search results found");
                    Program.DebugPrint($"No search results found from provided args:{args} returning..");
                    return;
                }

                url = videoInfo.Url;
                Program.DebugPrint($"Search successful URL:{url}");
            }

            await General.DeleteMessage(Context.Message, 0);
            if (videoInfo == null)
            {
                //stripping the original args as the URL should already be set at this point. 
                //This prevents errors if the user adds text at the end of a url
                args = new[] { url };
                videoInfo = await GetVideoInfoFromSearchTerm(args);
            }

            Console.WriteLine("here");
            var fileName = $"{videoInfo.Id}";
            var outputDir = Path.Combine(AppContext.BaseDirectory, Context.Guild.Id.ToString(), "media", fileName);
            string videoMp3Path = outputDir + ".mp3";
            outputDir += ".tmp";
            Console.WriteLine("here");

            // Clean up any previous temp files if they exist
            if (File.Exists(outputDir))
            {
                File.Delete(outputDir);
            }

            Console.WriteLine("here");


            if (!File.Exists(videoMp3Path))
            {
                //https://github.com/yt-dlp/yt-dlp 
                Program.DebugPrint("downloading audio");
                await DownloadAudio("yt-dlp", url, outputDir, "worstaudio");
                // does this have to be a dictionary of queues?
            }

            _musicQueue[Context.Guild.Id].Enqueue(videoInfo);
            var embeddedMessage = GetEmbeddedMessageFromVideoInfo(videoInfo);
            var addedMessage = await Context.Channel.SendMessageAsync("", false, embeddedMessage);

            General.DeleteMessage(addedMessage, 2500);

            Program.DebugPrint("Current Music Queue");
            foreach (var video in _musicQueue[Context.Guild.Id])
            {
                Program.DebugPrint(video.Title);
                Program.DebugPrint(_bCurrentlyPlayingMedia.ToString());
            }

            if (!_bCurrentlyPlayingMedia[Context.Guild.Id])
            {
                Program.DebugPrint("connecting to voice");
                await JoinVoiceChannel();
            }
        }

        // WARNING CONNECTION IS ONLY SUSTAINED IF THESE DLL'S ARE INCLUDED
        // https://github.com/discord-net/Discord.Net/tree/dev/voice-natives
        // libopus needs to be renamed to opus
        // TODO change to different api wrapper so we don't need extra dlls for voice transmission
        [Command("join")]
        private async Task JoinVoiceChannel()
        {
            while (_musicQueue.Count > 0)
            {
                _bCurrentlyPlayingMedia[Context.Guild.Id] = true;
                var audio = _musicQueue[Context.Guild.Id].Dequeue();
                Program.DebugPrint("################# Dequeuing ####################");
                var dir = Path.Combine(AppContext.BaseDirectory,
                    Context.Guild.Id.ToString(), "media", $"{audio.Id}.mp3");
                var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel;
                if (voiceChannel == null) continue;
                Program.DebugPrint("voice channel is not null");
                var audioClient = await voiceChannel.ConnectAsync();
                if (File.Exists(dir))
                {
                    Program.DebugPrint("Playing music.");
                    await SendAsync(audioClient, dir);
                    Program.DebugPrint("END");
                }

                Program.DebugPrint("END1");
            }

            Program.DebugPrint("No music in queue.");
            await Context.Channel.SendMessageAsync("Queue is empty.");
            await General.DeleteMessage(Context.Message, 500);
            await LeaveVoiceChannel();
            _bCurrentlyPlayingMedia[Context.Guild.Id] = false;
            return;
        }

        [Command("leave")]
        private async Task LeaveVoiceChannel()
        {
            var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel;
            if (voiceChannel != null)
            {
                Console.WriteLine("voice channel is not null");
                _audioOutStream[Context.Guild.Id].Clear();
                _bCurrentlyPlayingMedia[Context.Guild.Id] = false;
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
            while (_bProcessingMedia)
            {
                Program.DebugPrint("Media already being processed sleeping 1000ms..");
                await Task.Delay(1000);
            }
            Program.DebugPrint("FFMPEG: Started processing media");

            _bProcessingMedia = true;
            Program.DebugPrint("Converting to Mp3");
            Program.DebugPrint(filePath);
            string fileName = string.Empty;
            string directory = Path.GetDirectoryName(filePath);

            if (File.Exists(filePath))
            {
                fileName = Path.GetFileNameWithoutExtension(filePath);
            }

            string output = $"\"{directory}/{fileName}.mp3\"";
            if (File.Exists(output))
            {
                _audioOutStream[Context.Guild.Id]?.Clear();
                File.Delete(output);
            }

            var arguments = $"-y -i \"{filePath}\" -acodec mp3 {output}";
            Console.WriteLine(arguments);
            var processInfo = new ProcessStartInfo("ffmpeg", arguments);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;
            var process = Process.Start(processInfo);
            await process.WaitForExitAsync();
            Program.DebugPrint("Media finished processing");
            _bProcessingMedia = false;
            Console.WriteLine("Ffmpeg ExitCode: {0}", process.ExitCode);
            Console.WriteLine("########################################################");
            if (process.ExitCode == 0)
            {
                File.Delete(filePath);
                //Floods the console too much.
                //Console.WriteLine($"Successfully converted file from: {filePath} to: {output}");
                Program.DebugPrint($"Successfully downloaded and converted {fileName}");
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
                    Program.DebugPrint("Returning VideoInfo");

                    return video;
                }

                Program.DebugPrint("VideoInfo Returning null");
                return null;
            }
        }

        private async Task SendAsync(IAudioClient client, string path)
        {
            _bCurrentlyPlayingMedia[Context.Guild.Id] = true;
            Console.WriteLine(_bCurrentlyPlayingMedia);
            using (var ffmpeg = CreateStream(path))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = client.CreatePCMStream(AudioApplication.Mixed))
            {
                _audioOutStream[Context.Guild.Id] = discord;
                try
                {
                    await output.CopyToAsync(discord);
                }
                finally
                {
                    await discord.FlushAsync();
                    _bCurrentlyPlayingMedia[Context.Guild.Id] = false;
                    Console.WriteLine("##HERE##");
                }
            }
        }
    }
}