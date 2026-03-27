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
using System.Text.Json.Nodes;
using Windows.Gaming.Input;

namespace IptvFtw
{
    internal static class DataLoader
    {
        public static async Task LoadChannelsFromTvIrlPlaylist(Playlist playlist)
        {
            try
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
                    if (line.StartsWith("#EXTINF:-1"))
                    {
                        var lastComma = line.LastIndexOf(',');
                        string urlLine = null;
                        while (!playlistLines[++i].StartsWith("http")) ;
                        urlLine = playlistLines[i];

                        var splitUrlLine = urlLine.Split("|");

                        var channel = new Channel()
                        {
                            Id = GetNamedMetadataAttribute(line, "channel-id") ?? GetNamedMetadataAttribute(line, "tvg-id") ?? splitUrlLine[0],
                            DisplayName = line.Substring(lastComma + 1),
                            GuideId = GetNamedMetadataAttribute(line, "tvg-id"),
                            ChannelNumber = GetNamedMetadataAttribute(line, "tvg-chno"),
                            IconUrl = GetNamedMetadataAttribute(line, "tvg-logo"),
                            StreamUrl = splitUrlLine[0],
                            UserAgent = splitUrlLine.Length > 1 ? GetNamedUrlAttribute(splitUrlLine[1], "user-agent") : null,
                            Referer = splitUrlLine.Length > 1 ? GetNamedUrlAttribute(splitUrlLine[1], "referer") : null,
                            Included = true,
                        };


                        channels.Add(channel);

                    }
                    i++;

                }
                playlist.Channels = new System.Collections.ObjectModel.ObservableCollection<Channel>(channels.OrderBy(c => c.ChannelNumber ?? "zzz").ThenBy(c => c.DisplayName).ToList());
            }
            catch
            {
                playlist.Channels = new System.Collections.ObjectModel.ObservableCollection<Channel>();
            }
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
            var splitUrls = model.CurrentPlaylist?.EpgUrl?.Split(",");
            if (splitUrls == null)
            {
                return;
            }
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

        public static async Task<List<DirectoryItem>> LoadIptvOrgFeed(string feed)
        {
            try
            {
                var client = new HttpClient();
                var feedContent = await client.GetStringAsync($"https://iptv-org.github.io/api/{feed}.json");
                var feedJson = JsonNode.Parse(feedContent).AsArray();
                var items = new List<DirectoryItem>();
                foreach (var category in feedJson)
                {
                    if (feed == "categories")
                    {
                        if (category["id"].ToString() == "xxx")
                        {
                            continue;
                        }
                        items.Add(new DirectoryItem()
                        {
                            Id = category["id"].ToString().ToLowerInvariant(),
                            Name = category["name"].ToString(),
                        });
                    }
                    else if (feed == "countries")
                    {
                        items.Add(new DirectoryItem()
                        {
                            Id = category["code"].ToString().ToLowerInvariant(),
                            Name = category["name"].ToString(),
                        });
                    }
                    else if (feed == "languages")
                    {
                        string[] languagesWithFeeds = { "ach", "adh", "aar", "afr", "aho", "sqi", "gsw", "asp", "alz", "amh", "ara", "hye", "asm", "aii", "ayb", "aym", "aze",
                            "bba", "bam", "bak", "eus", "bel", "ben", "bho", "bib", "bos", "box", "bul", "mya", "cat", "ceb", "tzm", "ckb", "cnu", "hne", "cgg", "zho",
                            "hrv", "ces", "dan", "prd", "div", "luo", "zza", "nld", "dyu", "arz", "eng", "est", "ewe", "fao", "far", "fil", "fin", "fon", "fra", "ful",
                            "gla", "glg", "lug", "gej", "kat", "deu", "kik", "gom", "gux", "ell", "gcf", "guj", "guw", "hat", "bgc", "hau", "heb", "hin", "hmn", "hun",
                            "isl", "ind", "iku", "gle", "its", "icr", "ita", "jpn", "jav", "kbp", "kab", "kan", "pam", "kaz", "khm", "kmz", "kin", "kir", "mkw", "bbo",
                            "kon", "kok", "kor", "kdi", "kur", "lah", "laj", "lao", "lat", "lav", "ltz", "lin", "lit", "lob", "lua", "lus", "lee", "mkd", "mai", "msa",
                            "mal", "mlt", "cmn", "mnk", "mri", "mar", "rkm", "stj", "sym", "nan", "mon", "xms", "mos", "nep", "dgi", "nor", "nyn", "nyo", "ori", "pan",
                            "pap", "pus", "fas", "pol", "por", "fuc", "que", "ron", "rom", "rus", "acf", "smo", "sat", "srp", "snd", "sin", "slk", "slv", "som", "sfs",
                            "nbl", "sbd", "spa", "arb", "sun", "swa", "ssw", "swe", "shy", "shi", "tgl", "tah", "tgk", "tmh", "taq", "tam", "rif", "tat", "tel", "tha", 
                            "bod", "tig", "tir", "ttj", "tso", "mzb", "tur", "tuk", "uig", "ukr", "urd", "uzb", "ven", "vie", "cym", "fry", "wol", "xho", "sah", "yor",
                            "yua", "yue", "dje", "zul" };

                        if (languagesWithFeeds.Contains(category["code"].ToString()))
                        {
                            items.Add(new DirectoryItem()
                            {
                                Id = category["code"].ToString().ToLowerInvariant(),
                                Name = category["name"].ToString(),
                            });
                        }
    
                    }
                    else if (feed == "subdivisions")
                    {
                        string[] countriesWithSubdivisionFeeds = { "AR", "AU", "AT", "BE", "BO", "BR", "CA", "CL", "CO", "CR", "DO", "EC", "FI", "FR", "GE", "DE", 
                            "GR", "GT", "IN", "ID", "IT", "JP", "MX", "PK", "PY", "PR", "PH", "CG", "RO", "RU", "KR", "ES", "UA", "UK", "US", "VE"};
                        if (countriesWithSubdivisionFeeds.Contains(category["country"].ToString()))
                        {
                            items.Add(new DirectoryItem()
                            {
                                Id = category["code"].ToString().ToLowerInvariant(),
                                Name = category["name"].ToString(),
                                ParentId = category["country"].ToString().ToLowerInvariant(),
                            });
                        }
                    }

                }
                return items.OrderBy(i => i.Name).ToList();
            }
            catch
            {
                return new List<DirectoryItem>();
            }
        }
    }

}
