using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IptvFtw.Models
{
    public class Playlist : INotifyPropertyChanged, ICloneable
    {
        private string _name;
        public string Name
        {
            get { return _name; }
            set { 
                _name = value; 
                RaisePropertyChanged(nameof(Name)); 
            }
        }
        public string Url { get; set; }
        public string EpgUrl { get; set; }

        public List<string> SavedIncludedChannelIds { get; set; }

        private ObservableCollection<Channel> _channels;

        public ObservableCollection<Channel> Channels
        {
            get { return _channels; }
            set
            {
                _channels = value;
                RaisePropertyChanged(nameof(Channels));
                RaisePropertyChanged(nameof(IncludedChannels));
            }
        }

        public ObservableCollection<Channel> IncludedChannels
        {
            get { return _channels == null ? null : new ObservableCollection<Channel>(_channels.Where(ch => ch.Included).ToList()); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(property, new PropertyChangedEventArgs(property));
            }
        }

        public object Clone()
        {
            var p = new Playlist()
            {
                Name = Name,
                Url = Url,
                EpgUrl = EpgUrl,
                SavedIncludedChannelIds = SavedIncludedChannelIds,
                Channels = new ObservableCollection<Channel>(),
            };
            foreach (var channel in Channels)
            {
                p.Channels.Add((Channel)channel.Clone());
            }
            return p;
        }
    }
}
