using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IptvFtw.Models
{
    public class Playlist : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string EpgUrl { get; set; }

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
    }
}
