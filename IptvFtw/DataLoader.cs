using IptvFtw.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IptvFtw
{
    internal static class DataLoader
    {
        public static async Task<List<Channel>> LoadChannelsFromTvIrlPlaylist(string playlistUrl)
        {
            var channels = new List<Channel>();
            var client = new HttpClient();
            var playlistContent = await client.GetStringAsync(playlistUrl);

            var playlistLines = playlistContent.Split('\n');
            int i = 0;
            while (i < playlistLines.Length)
            {
                var line = playlistLines[i];
                if (line.StartsWith("#EXTINF:-1 "))
                {
                    var lastComma = line.LastIndexOf(',');
                    var urlLine = playlistLines[++i];
                    var splitUrlLine = urlLine.Split("|");

                    var channel = new Channel()
                    {
                        Id = GetNamedMetadataAttribute(line, "channel-id"),
                        DisplayName = line.Substring(lastComma + 1),
                        GuideId = GetNamedMetadataAttribute(line, "tvg-id"),
                        ChannelNumber = GetNamedMetadataAttribute(line, "tvg-chno"),
                        IconUrl = GetNamedMetadataAttribute(line, "tvg-logo"),
                        StreamUrl = splitUrlLine[0],
                        UserAgent = splitUrlLine.Length > 1 ? GetNamedUrlAttribute(splitUrlLine[1], "user-agent") : null,
                        Referer = splitUrlLine.Length > 1 ? GetNamedUrlAttribute(splitUrlLine[1], "referer") : null,
                    };

                    if (!channel.DisplayName.EndsWith(" Alt"))
                    {
                        channels.Add(channel);
                    }
                    
                    
                }
                i++;

            }
            return channels.OrderBy(c => c.ChannelNumber ?? "zzz").ThenBy(c => c.DisplayName).ToList();

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
    }


}
