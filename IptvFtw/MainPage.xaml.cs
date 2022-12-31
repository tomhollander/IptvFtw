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
        private DispatcherTimer _timer;
        private MainModel _model = new MainModel();
        private DisplayRequest _dispRequest = new DisplayRequest();

        public MainPage()
        {
            this.InitializeComponent();
            SetupTimer();

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

        private async Task LoadData()
        {
            if (_model.PlaylistUrl == null)
            {
                splitView.IsPaneOpen = true;
            }
            else
            {
                try
                {
                    await DataLoader.LoadChannelsFromTvIrlPlaylist(_model.PlaylistUrl, _model);
                    _model.SelectedChannel = _model.Channels.Where(c => c.Id == _model.LastChannelId).FirstOrDefault();
                    if (_model.SelectedChannel == null )
                    {
                        _model.SelectedChannel = _model.Channels.First();
                    }
                    playlistErrorTextBlock.Visibility = Visibility.Collapsed;
                    splitView.IsPaneOpen = false;
                    PushRecentPlaylistUrl();
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

        private void PushRecentPlaylistUrl()
        {
            if (_model.RecentPlaylistUrls == null)
            {
                _model.RecentPlaylistUrls = new ObservableCollection<string>();
            }
            // Remove it if it's there already
            _model.RecentPlaylistUrls.Remove(_model.PlaylistUrl);
            _model.RecentPlaylistUrls.Insert(0, _model.PlaylistUrl);

            if (_model.RecentPlaylistUrls.Count > 10)
            {
                _model.RecentPlaylistUrls.RemoveAt(9);
            }

        }

        private async void ApplyPlaylistUrl_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _model.PlaylistUrl = playlistUrlTextBox.Text;
            await LoadData();
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
            _timer.Stop();
            _timer.Start();
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

        private void SetupTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(8);
            _timer.Tick += ((sender, e) => HideControls());
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
            _timer.Stop();
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
            _timer.Stop();
        }

        private async Task PlayChannel()
        {
            if (_model.SelectedChannel?.StreamUri != null)
            {
                await InitializeAdaptiveMediaSource(_model.SelectedChannel.StreamUri);
                _timer.Start();
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
            _model.PlaylistUrl = (string) ApplicationData.Current.LocalSettings.Values["PlaylistUrl"];
            _model.LastChannelId = (string)ApplicationData.Current.LocalSettings.Values["LastChannel"];
            var recentPlaylists = ((string)ApplicationData.Current.LocalSettings.Values["RecentPlaylistUrls"])?.Split("|").ToList();
            if (recentPlaylists != null)
            {
                _model.RecentPlaylistUrls = new ObservableCollection<string>(recentPlaylists);
            }

        }
        private void SaveSettings()
        {
            ApplicationData.Current.LocalSettings.Values["LastChannel"] = _model.LastChannelId;
            ApplicationData.Current.LocalSettings.Values["PlaylistUrl"] = _model.PlaylistUrl;
            ApplicationData.Current.LocalSettings.Values["RecentPlaylistUrls"] = String.Join("|", _model.RecentPlaylistUrls);
        }

        private void RecentPlaylistUrl_Tapped(object sender, TappedRoutedEventArgs e)
        {
            playlistUrlTextBox.Text = ((TextBlock)sender).Text;
        }
    }

}
