using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IptvFtw.Models
{
    public class MainModel : INotifyPropertyChanged
    {
        private ObservableCollection<Playlist> _playlists;
        public ObservableCollection<Playlist> Playlists
        {
            get { return _playlists; }
            set
            {
                _playlists = value;
                RaisePropertyChanged(nameof(Playlists));
            }
        }

        private Playlist _currentPlaylist { get; set;  }
        public Playlist CurrentPlaylist
        {
            get { return _currentPlaylist; }
            set
            {
                _currentPlaylist = value;
                RaisePropertyChanged(nameof(CurrentPlaylist));
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
