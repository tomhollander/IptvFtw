using IptvFtw.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Streaming.Adaptive;
using Windows.Storage;
using Windows.System;
using Windows.System.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace IptvFtw
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DispatcherTimer _channelBarTimer;
        private DispatcherTimer _channelNumberTimer;

        private MainModel _model = new MainModel();
        private DisplayRequest _dispRequest = new DisplayRequest();

        public MainPage()
        {
            this.InitializeComponent();
            SetupTimers();

        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.DataContext = _model;
            RestoreSettings();
            await LoadData();

            try
            {
                _dispRequest.RequestActive();
            }
            catch
            {
            }

            base.OnNavigatedTo(e);
            channelsListView.Focus(FocusState.Programmatic);
        }

        private async Task LoadChannelsForPlaylist(Playlist playlist)
        {
            await DataLoader.LoadChannelsFromTvIrlPlaylist(playlist);
        }
        private async Task LoadData()
        {
            if (_model.CurrentPlaylist == null)
            {
                splitView.IsPaneOpen = true;
            }
            else
            {
                try
                {
                    await LoadChannelsForPlaylist(_model.CurrentPlaylist);
                    _model.SelectedChannel = _model.CurrentPlaylist?.Channels.Where(c => c.Id == _model.LastChannelId).FirstOrDefault();
                    if (_model.SelectedChannel == null )
                    {
                        _model.SelectedChannel = _model.CurrentPlaylist?.Channels.First();
                    }
                    playlistErrorTextBlock.Visibility = Visibility.Collapsed;
                    splitView.IsPaneOpen = false;
                    
                    SaveSettings();
                    ShowControls();
                    await PlayChannel();

                    if (_model.EpgUrl != null)
                    {
                        await DataLoader.LoadTvPrograms(_model);
                    }
                    

                }
                catch (Exception ex)
                {
                    splitView.IsPaneOpen = true;
                    playlistErrorTextBlock.Text = ex.Message;
                    playlistErrorTextBlock.Visibility = Visibility.Visible;
                }
            }
        }

        private void SavePlaylist()
        {
            if (_model.Playlists == null)
            {
                _model.Playlists = new ObservableCollection<Playlist>();
            }
            // Remove it if it's there already
            var thisPlaylist = _model.Playlists.Where(p => p.Url == "XX").FirstOrDefault();
            if (thisPlaylist != null)
            {
                
            }
            _model.Playlists.Add(new Playlist());

        }

        private Playlist unsavedPlaylist;

        private async void ApplyPlaylistUrl_Tapped(object sender, TappedRoutedEventArgs e)
        {
            unsavedPlaylist = new Playlist()
            {
                Url = playlistUrlTextBox.Text
            };
            try
            {
                playlistErrorTextBlock.Visibility = Visibility.Collapsed;
                await DataLoader.LoadChannelsFromTvIrlPlaylist(unsavedPlaylist);
                if (unsavedPlaylist.Channels.Count > 0) {
                    ToggleAddEditPlaylistControls(Visibility.Visible);
                    includeChannelsListView.ItemsSource = unsavedPlaylist.Channels;
                }
                else
                {
                    playlistErrorTextBlock.Text = "No channels found. Is it a proper IPTV playlist?";
                    playlistErrorTextBlock.Visibility = Visibility.Visible;
                }
                
            }
            catch (Exception ex)
            {
                playlistErrorTextBlock.Text = ex.Message;
                playlistErrorTextBlock.Visibility = Visibility.Visible;
            }
            
        }

        private void ToggleAddEditPlaylistControls(Visibility visibility)
        {
            playlistNameCaption.Visibility = visibility;
            playlistName.Visibility = visibility;
            includeChannelsCaption.Visibility = visibility;
            includeChannelsListView.Visibility = visibility;
            saveButton.IsEnabled = visibility == Visibility.Visible;
        }

        private async void ChannelsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var newChannel = (Channel)((ListView)sender).SelectedItem;
            if (_model.SelectedChannel != newChannel)
            {
                _model.SelectedChannel = newChannel;
                _model.LastChannelId = newChannel?.Id;
                _model.CurrentTvProgram = _model.TvPrograms?.Where(
                    p => p.ChannelId == newChannel?.GuideId &&
                         p.Start <= DateTime.Now &&
                         p.End >= DateTime.Now).FirstOrDefault();

                await PlayChannel();
                SaveSettings();
            }
        }

        private void channelsListView_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            _channelBarTimer.Stop();
            _channelBarTimer.Start();
        }

        private static readonly IDictionary<VirtualKey, char> NumericKeys =
            new Dictionary<VirtualKey, char> {
            { VirtualKey.Number0, '0' },
            { VirtualKey.Number1, '1' },
            { VirtualKey.Number2, '2' },
            { VirtualKey.Number3, '3' },
            { VirtualKey.Number4, '4' },
            { VirtualKey.Number5, '5' },
            { VirtualKey.Number6, '6' },
            { VirtualKey.Number7, '7' },
            { VirtualKey.Number8, '8' },
            { VirtualKey.Number9, '9' },
            { VirtualKey.NumberPad0, '0' },
            { VirtualKey.NumberPad1, '1' },
            { VirtualKey.NumberPad2, '2' },
            { VirtualKey.NumberPad3, '3' },
            { VirtualKey.NumberPad4, '4' },
            { VirtualKey.NumberPad5, '5' },
            { VirtualKey.NumberPad6, '6' },
            { VirtualKey.NumberPad7, '7' },
            { VirtualKey.NumberPad8, '8' },
            { VirtualKey.NumberPad9, '9' }
       };

        private void Page_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (FocusManager.GetFocusedElement() is TextBox)
            {
                return;
            }
            if (e.Key == Windows.System.VirtualKey.Space || e.Key == Windows.System.VirtualKey.GamepadX ||
                e.Key == Windows.System.VirtualKey.GamepadMenu || e.Key == Windows.System.VirtualKey.X)
            {
                ShowControls();
            }
            else if (e.Key == VirtualKey.Escape)
            {
                HideControls();
            }
            else if (e.Key == Windows.System.VirtualKey.Y || e.Key == Windows.System.VirtualKey.GamepadY ||
                e.Key == Windows.System.VirtualKey.GamepadView)
            {
                mediaElement.TransportControls.Focus(FocusState.Programmatic);
            }
            else if (e.Key == VirtualKey.Enter)
            {
                ChangeChannelNumber();
            }
            else if (NumericKeys.ContainsKey(e.Key))
            {
                ProcessChannelNumberInput(NumericKeys[e.Key]);
            }
        }

        private void ProcessChannelNumberInput(char key)
        {
            _channelNumberTimer.Start();
            if (channelNumberTextBlock.Visibility == Visibility.Collapsed)
            {
                channelNumberTextBlock.Text = "";
                channelNumberTextBlock.Visibility = Visibility.Visible;
            }
            if (channelNumberTextBlock.Text.Length == 3)
            {
                channelNumberTextBlock.Text = channelNumberTextBlock.Text.Substring(1);
            }
            channelNumberTextBlock.Text += key;
        }

        private void ChangeChannelNumber()
        {
            // Remove leading zeros
            string channelNumber = (Int32.Parse(channelNumberTextBlock.Text)).ToString();
            channelNumberTextBlock.Visibility = Visibility.Collapsed;
            _channelNumberTimer.Stop();

            var newChannel = _model.CurrentPlaylist?.Channels.Where(c => c.ChannelNumber == channelNumber).FirstOrDefault();
            if (newChannel != null)
            {
                _model.SelectedChannel = newChannel;
                PlayChannel();
            }

        }

        private async void MediaTransportControls_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (e.OriginalSource.GetType() == typeof(Grid) && mediaElement.IsFullWindow)
            {
                mediaElement.IsFullWindow = false;
                await Task.Delay(1000);
                ShowHideControls();
            }
        }

        private async void SettingsAppBarButton_Click(object sender, EventArgs e)
        {
            if (mediaElement.IsFullWindow)
            {
                mediaElement.IsFullWindow = false;
                await Task.Delay(1000);
            }
            splitView.IsPaneOpen = true;
            playlistUrlTextBox.Focus(FocusState.Programmatic);
        }

        private void SetupTimers()
        {
            _channelBarTimer = new DispatcherTimer();
            _channelBarTimer.Interval = TimeSpan.FromSeconds(8);
            _channelBarTimer.Tick += ((sender, e) => HideControls());

            _channelNumberTimer = new DispatcherTimer();
            _channelNumberTimer.Interval = TimeSpan.FromSeconds(3);
            _channelNumberTimer.Tick += ((sender, e) => ChangeChannelNumber());
        }
        private void MediaElement_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ShowHideControls();
        }

        private void ShowHideControls()
        {
            if (topControls.Visibility == Visibility.Visible)
            {
                HideControls();
            }
            else
            {
                ShowControls();
            }
            _channelBarTimer.Stop();
        }

        private void ShowControls()
        {

            _model.CurrentTvProgram = _model.TvPrograms?.Where(
                p => p.ChannelId == _model.SelectedChannel?.GuideId &&
                     p.Start <= DateTime.Now &&
                     p.End >= DateTime.Now).FirstOrDefault();

            topControls.Visibility = Visibility.Visible;
            channelsListView.Focus(FocusState.Programmatic);
        }

        private void HideControls()
        {
            topControls.Visibility = Visibility.Collapsed;
            _channelBarTimer.Stop();
        }

        private async Task PlayChannel()
        {
            if (_model.SelectedChannel?.StreamUri != null)
            {
                await InitializeAdaptiveMediaSource(_model.SelectedChannel.StreamUri);
                _channelBarTimer.Start();
            }
        }

        private async Task InitializeAdaptiveMediaSource(System.Uri uri)
        {
            var httpClient = new Windows.Web.Http.HttpClient();
            AdaptiveMediaSourceCreationResult result = await AdaptiveMediaSource.CreateFromUriAsync(uri, httpClient);

            if (result.Status == AdaptiveMediaSourceCreationStatus.Success)
            {
                var ams = result.MediaSource;
                mediaElement.Source = null;
                mediaElement.SetMediaStreamSource(ams);

                uint bitrate = ams.AvailableBitrates.Max();
                ams.InitialBitrate = bitrate;
                ams.DesiredMaxBitrate = bitrate;
                ams.DownloadRequested += Ams_DownloadRequested;

            }
            else
            {
                mediaElement.Source = null;
            }
        }

        private void Ams_DownloadRequested(AdaptiveMediaSource sender, AdaptiveMediaSourceDownloadRequestedEventArgs args)
        {
            Debug.WriteLine(args.ResourceType);
            if (args.ResourceType == AdaptiveMediaSourceResourceType.Key)
            {
                args.Result.ResourceByteRangeOffset = null;
                args.Result.ResourceByteRangeLength = null;
            }
        }

        private void RestoreSettings()
        {
            var lastUrl = (string) ApplicationData.Current.LocalSettings.Values["PlaylistUrl"];
            _model.LastChannelId = (string)ApplicationData.Current.LocalSettings.Values["LastChannel"];
            _model.Playlists = new ObservableCollection<Playlist>();
            var playlistUrls = ((string)ApplicationData.Current.LocalSettings.Values["RecentPlaylistUrls"])?.Split("|").ToList();
            var playlistNames = ((string)ApplicationData.Current.LocalSettings.Values["PlaylistNames"])?.Split("|").ToList();
            if (playlistNames == null)
            {
                playlistNames = playlistUrls;
            }
            if (playlistUrls != null)
            {
                for (int i=0; i<playlistUrls.Count; i++)
                {
                    var playlist = new Playlist();
                    playlist.Url = playlistUrls[i];
                    playlist.Name = playlistNames[i];
                    _model.Playlists.Add(playlist);
                }
            }
            _model.CurrentPlaylist = _model.Playlists.Where(p => p.Url == lastUrl).FirstOrDefault();

        }
        private void SaveSettings()
        {
            ApplicationData.Current.LocalSettings.Values["LastChannel"] = _model.LastChannelId;
            ApplicationData.Current.LocalSettings.Values["PlaylistUrl"] = _model.CurrentPlaylist?.Url;
            // ApplicationData.Current.LocalSettings.Values["RecentPlaylistUrls"] = String.Join("|", _model.RecentPlaylistUrls);
        }

        private async void PlaylistsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _model.CurrentPlaylist = (Playlist) playlistsListView.SelectedItem;
            if (_model.CurrentPlaylist == null)
            {
                return;
            }
            if (_model.CurrentPlaylist.Channels == null)
            {
                await LoadChannelsForPlaylist(_model.CurrentPlaylist);
            }
            _model.SelectedChannel = _model.CurrentPlaylist.IncludedChannels.FirstOrDefault();
            await PlayChannel();
            splitView.IsPaneOpen = false;
            ShowControls();

        }

        private void AddPlaylist_Tapped(object sender, TappedRoutedEventArgs e)
        {
            playlistUrlTextBox.Text = "";
            playlistUrlTextBox.IsEnabled = true;
            listPlaylists.Visibility = Visibility.Collapsed;
            ToggleAddEditPlaylistControls(Visibility.Collapsed);
            loadPlaylistButton.Visibility = Visibility.Visible;
            addEditPlaylist.Visibility = Visibility.Visible;
        }

        private async void saveButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            unsavedPlaylist.Name = String.IsNullOrEmpty(playlistName.Text) ?  unsavedPlaylist.Url : playlistName.Text;
            if (loadPlaylistButton.Visibility == Visibility.Collapsed)
            {
                // Edit mode
                var replacedPlaylist = _model.Playlists.Where(p => p.Url == unsavedPlaylist.Url).First();
                replacedPlaylist.Name = unsavedPlaylist.Name;
                replacedPlaylist.Channels = unsavedPlaylist.Channels;
            }
            else
            {
                // Add mode
                _model.Playlists.Add(unsavedPlaylist);
                _model.CurrentPlaylist = unsavedPlaylist;
                _model.SelectedChannel = _model.CurrentPlaylist.IncludedChannels.FirstOrDefault();
            }
            
            await PlayChannel();

            splitView.IsPaneOpen = false;
            listPlaylists.Visibility = Visibility.Visible;
            addEditPlaylist.Visibility = Visibility.Collapsed;
            ShowControls();
        }

        private void cancelButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            splitView.IsPaneOpen = false;
            listPlaylists.Visibility = Visibility.Visible;
            addEditPlaylist.Visibility = Visibility.Collapsed;
        }

        private async void editPlaylistButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var playlist = ((Button)sender).DataContext as Playlist;
            if (playlist.Channels == null)
            {
                await LoadChannelsForPlaylist(playlist);
            }
            unsavedPlaylist = (Playlist) playlist.Clone();
            playlistUrlTextBox.Text = unsavedPlaylist.Url;
            playlistUrlTextBox.IsEnabled = false;
            playlistName.Text = unsavedPlaylist.Name;
            loadPlaylistButton.Visibility = Visibility.Collapsed;
            includeChannelsListView.ItemsSource = unsavedPlaylist.Channels;
            listPlaylists.Visibility = Visibility.Collapsed;
            ToggleAddEditPlaylistControls(Visibility.Visible);
            addEditPlaylist.Visibility = Visibility.Visible;
        }

        private async void deletePlaylistButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var playlist = ((Button)sender).DataContext as Playlist;
            _model.Playlists.Remove(playlist);
            if (_model.CurrentPlaylist == playlist)
            {
                _model.CurrentPlaylist = _model.Playlists.FirstOrDefault();
                await PlayChannel();
                ShowControls();
            }
        }
    }

}
