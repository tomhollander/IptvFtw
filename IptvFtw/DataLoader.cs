using IptvFtw.Models;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.UI.Xaml.Shapes;
using System.Globalization;

namespace IptvFtw
{
    internal static class DataLoader
    {
        public static async Task LoadChannelsFromTvIrlPlaylist(Playlist playlist)
        {
            var channels = new List<Channel>();
            var client = new HttpClient();

            var playlistContent = await client.GetStringAsync(playlist.Url);

            var playlistLines = playlistContent.Split('\n');
            int i = 0;
            while (i < playlistLines.Length)
            {
                if (playlistLines[i].StartsWith("#EXTM3U"))
                {
                    playlist.EpgUrl = GetNamedMetadataAttribute(playlistLines[i], "x-tvg-url");
                }

                var line = playlistLines[i];
                if (line.StartsWith("#EXTINF:-1 "))
                {
                    var lastComma = line.LastIndexOf(',');
                    var urlLine = playlistLines[++i];
                    var splitUrlLine = urlLine.Split("|");

                    var channel = new Channel()
                    {
                        Id = GetNamedMetadataAttribute(line, "channel-id") ?? GetNamedMetadataAttribute(line, "tvg-id"),
                        DisplayName = line.Substring(lastComma + 1),
                        GuideId = GetNamedMetadataAttribute(line, "tvg-id"),
                        ChannelNumber = GetNamedMetadataAttribute(line, "tvg-chno"),
                        IconUrl = GetNamedMetadataAttribute(line, "tvg-logo"),
                        StreamUrl = splitUrlLine[0],
                        UserAgent = splitUrlLine.Length > 1 ? GetNamedUrlAttribute(splitUrlLine[1], "user-agent") : null,
                        Referer = splitUrlLine.Length > 1 ? GetNamedUrlAttribute(splitUrlLine[1], "referer") : null,
                        Included = true,
                    };

                    // Only show a max of 250 channels in this app, as some playlists are really big.
                    //if (!channel.DisplayName.EndsWith(" Alt") && channels.Count < 250) 
                    {
                        channels.Add(channel);
                    }
                    
                    
                }
                i++;

            }
            playlist.Channels = new System.Collections.ObjectModel.ObservableCollection<Channel>(channels.OrderBy(c => c.ChannelNumber ?? "zzz").ThenBy(c => c.DisplayName).ToList());

        }

        private static string GetNamedMetadataAttribute(string line, string key)
        {
            var regExp = new Regex($"{key}=\\\"([^\\\"]*)\\\"");
            var match = regExp.Match(line);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        private static string GetNamedUrlAttribute(string line, string key)
        {
            var regExp = new Regex($"{key}=([^&]*)");
            var match = regExp.Match(line);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        public static async Task LoadTvPrograms(MainModel model)
        {

            model.TvPrograms = new List<TvProgram>();
            var splitUrls = model.EpgUrl.Split(",");
            Parallel.ForEach(splitUrls, async url =>
            {
                try
                {
                    XDocument epgDoc = await GetEpg(url);
                    model.TvPrograms.AddRange(GetTvPrograms(epgDoc));
                }
                catch
                {

                }
            });

        }
        

        private static async Task<XDocument> GetEpg(string epgUri)
        {
            using (var client = new HttpClient())
            {
                byte[] data = await client.GetByteArrayAsync(new Uri(epgUri));
                
                if (epgUri.EndsWith(".gz"))
                {
                    using (MemoryStream decompressedStream = new MemoryStream())
                    {
                        // Use a GZipStream to decompress the file
                        using (GZipStream gzipStream = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
                        {
                            // Copy the decompressed data to the output stream
                            gzipStream.CopyTo(decompressedStream);

                            // Reset the position of the output stream to the beginning
                            decompressedStream.Position = 0;

                            // Load the decompressed data into an XDocument
                            return await XDocument.LoadAsync(decompressedStream, LoadOptions.None, new System.Threading.CancellationToken());
                        }
                    }
                }
                else
                {
                    using (var stream = new MemoryStream(data))
                    {
                        return await XDocument.LoadAsync(stream, LoadOptions.None, new System.Threading.CancellationToken());
                    }
                    
                }    

            }
        }
        private static List<TvProgram> GetTvPrograms(XDocument epgDocument)
        {
            var tvPrograms = new List<TvProgram>();
            var programs = epgDocument.Descendants("programme").ToList();
            foreach (var program in programs)
            {
                try
                {
                    tvPrograms.Add(new TvProgram()
                    {
                        ChannelId = program.Attribute("channel").Value,
                        Title = program.Descendants("title").SingleOrDefault()?.Value,
                        Subtitle = program.Descendants("sub-title").SingleOrDefault()?.Value,
                        Description = program.Descendants("desc").SingleOrDefault()?.Value,
                        Start = ParseEpgDate(program.Attribute("start").Value),
                        End = ParseEpgDate(program.Attribute("stop").Value),

                    });
                }
                catch
                {

                }

            }
            return tvPrograms;

        }

        private static DateTime ParseEpgDate(string dateString)
        {
            if (dateString == null)
            {
                return DateTime.MinValue;
            }
            return DateTime.ParseExact(dateString, "yyyyMMddHHmmss zzz", DateTimeFormatInfo.CurrentInfo, DateTimeStyles.AssumeLocal);
        }
    }


}
