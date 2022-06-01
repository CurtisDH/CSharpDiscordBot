using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.Commands;
using SpotifyAPI.Web;
using Swan;

namespace DiscordBot.Modules
{
    public class SpotifyDownloadModule : ModuleBase
    {
        [Command("downloadSpotify")]
        private async Task DownloadSpotifyPlaylist(string url)
        {
            var audioModule = new AudioModule();
            var spotify = new SpotifyClient(Program.SpotifyToken);
            var fullPlaylist = await spotify.Playlists.Get(GetIDFromSpotifyURL(url));
            List<Root> tracks = new List<Root>();
            var options = new JsonSerializerOptions {WriteIndented = true};
            foreach (var track in fullPlaylist.Tracks.Items)
            {
                var json = track.ToJson();
                tracks.Add(JsonSerializer.Deserialize<Root>(json));
            }

            Console.WriteLine(tracks.Count);
            foreach (var root in tracks)
            {
                Console.WriteLine(root.Track.Name);
                Console.WriteLine(root.Track.Artists[0].Name);

                string[] searchTerm = {root.Track.Name, root.Track.Artists[0].Name + " Lyrics"};
                var fileName = root.Track.Name + "-" + root.Track.Artists[0].Name + ".tmp";
                //The following characters created issues
                var fn = fileName.Replace(" ", "");
                fn = fn.Replace("(", "");
                fn = fn.Replace(")", "");
                fn = fn.Replace("/", "");
                fn = fn.Replace("\"", "");
                fn = fn.Replace("\'", "");
                fn = fn.Replace("#", "");
                fn = fn.Replace(":", "");
                var outputDir = Path.Combine(AppContext.BaseDirectory, Context.Guild.Id.ToString(),
                    "media", GetIDFromSpotifyURL(url), fn);
                Console.WriteLine("######################################");
                if (File.Exists(Path.GetDirectoryName(outputDir) + "/" + Path.GetFileNameWithoutExtension(outputDir) +
                                ".mp3"))
                {
                    Console.WriteLine("File exists. Skipping!");
                    continue;
                }

                var vidInfo = await AudioModule.GetVideoInfoFromSearchTerm(searchTerm);
                await audioModule.DownloadAudio("yt-dlp", vidInfo.Url, outputDir, "bestaudio");
            }

            Program.Print($"Completed downloading songs total: {tracks.Count}");
        }

        private string GetIDFromSpotifyURL(string url)
        {
            var splitstring = url.Split("https://open.spotify.com/playlist/");
            var newstring = splitstring[1].Split("?si=");

            if (newstring.Length > 0)
            {
                Console.WriteLine($"returning {newstring[0]}");
                return newstring[0];
            }

            Console.WriteLine($"returning {url}");
            return url;
        }
    }
}


// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
public class ExternalUrls
{
    public string spotify { get; set; }
}

public class AddedBy
{
    public object DisplayName { get; set; }
    public ExternalUrls ExternalUrls { get; set; }
    public object Followers { get; set; }
    public string Href { get; set; }
    public string Id { get; set; }
    public object Images { get; set; }
    public string Type { get; set; }
    public string Uri { get; set; }
}

public class Artist
{
    public ExternalUrls ExternalUrls { get; set; }
    public string Href { get; set; }
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Uri { get; set; }
}

public class Image
{
    public int Height { get; set; }
    public int Width { get; set; }
    public string Url { get; set; }
}

public class Album
{
    public object AlbumGroup { get; set; }
    public string AlbumType { get; set; }
    public List<Artist> Artists { get; set; }
    public List<object> AvailableMarkets { get; set; }
    public ExternalUrls ExternalUrls { get; set; }
    public string Href { get; set; }
    public string Id { get; set; }
    public List<Image> Images { get; set; }
    public string Name { get; set; }
    public string ReleaseDate { get; set; }
    public string ReleaseDatePrecision { get; set; }
    public object Restrictions { get; set; }
    public int TotalTracks { get; set; }
    public string Type { get; set; }
    public string Uri { get; set; }
}

public class ExternalIds
{
    public string isrc { get; set; }
}

public class Track
{
    public Album Album { get; set; }
    public List<Artist> Artists { get; set; }
    public List<object> AvailableMarkets { get; set; }
    public int DiscNumber { get; set; }
    public int DurationMs { get; set; }
    public bool Explicit { get; set; }
    public ExternalIds ExternalIds { get; set; }
    public ExternalUrls ExternalUrls { get; set; }
    public string Href { get; set; }
    public string Id { get; set; }
    public bool IsPlayable { get; set; }
    public object LinkedFrom { get; set; }
    public object Restrictions { get; set; }
    public string Name { get; set; }
    public int Popularity { get; set; }
    public object PreviewUrl { get; set; }
    public int TrackNumber { get; set; }
    public int Type { get; set; }
    public string Uri { get; set; }
    public bool IsLocal { get; set; }
}

public class Root
{
    public DateTime AddedAt { get; set; }
    public AddedBy AddedBy { get; set; }
    public bool IsLocal { get; set; }
    public Track Track { get; set; }
}