using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IptvFtw.Models
{
    public class MainModel : INotifyPropertyChanged
    {
        private string _playlistUrl { get; set;  }
        public string PlaylistUrl
        {
            get { return _playlistUrl; }
            set
            {
                _playlistUrl = value;
                RaisePropertyChanged(nameof(PlaylistUrl));
            }
        }

        private string _epgUrl { get; set; }
        public string EpgUrl
        {
            get { return _epgUrl; }
            set
            {
                _epgUrl = value;
                RaisePropertyChanged(nameof(EpgUrl));
            }
        }

        public string LastChannelId { get; set; }

        private List<Channel> _channels;
        public List<Channel> Channels
        {
            get { return _channels; }
            set
            {
                _channels = value;
                RaisePropertyChanged(nameof(Channels));
            }
        }

        private Channel _selectedChannel;

        public Channel SelectedChannel
        {
            get { return _selectedChannel; }
            set
            {
                _selectedChannel = value;
                RaisePropertyChanged(nameof(SelectedChannel));
            }
        }

        private List<TvProgram> _tvPrograms;
        public List<TvProgram> TvPrograms
        {
            get { return _tvPrograms; }
            set
            {
                _tvPrograms = value;
                RaisePropertyChanged(nameof(TvPrograms));
            }
        }

        private TvProgram _currentTvProgram;
        public TvProgram CurrentTvProgram
        {
            get { return _currentTvProgram; }
            set
            {
                _currentTvProgram = value;
                RaisePropertyChanged(nameof(CurrentTvProgram));
            }
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
