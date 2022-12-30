using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IptvFtw.Models
{
    public class TvProgram
    {
        public string ChannelId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Description { get; set; }

        public string TitleAndSubtitle
        {
            get { return String.IsNullOrEmpty(Subtitle) ? Title : Title + " - " + Subtitle;}
        }
    }
}
