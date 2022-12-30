using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IptvFtw.Models
{
    public class Channel
    {
        public string Id { get; set; }

        public string DisplayName { get; set; }
        public string GuideId { get; set; }
        public string IconUrl { get; set; }
        public Uri IconUri { get { return String.IsNullOrEmpty(IconUrl) ? null : new Uri(IconUrl); } }

        public string ChannelNumber { get; set; }
        public string StreamUrl { get; set; }

        public Uri StreamUri { get { return String.IsNullOrEmpty(StreamUrl) ? null : new Uri(StreamUrl); } }

        public string UserAgent { get; set; }
        public string Referer { get; set; }
    }
}
