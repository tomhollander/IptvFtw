using IptvFtw.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
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
        private static readonly System.Threading.SemaphoreSlim _settingsFileLock = new System.Threading.SemaphoreSlim(1, 1);

        private List<DirectoryItem> ipTvOrgCategories;
        private List<DirectoryItem> ipTvOrgCountries;
        private List<DirectoryItem> ipTvOrgSubdivisions;
        private List<DirectoryItem> ipTvOrgLangauges;
        private List<DirectoryItem> MjhNzPlaylists;

        public MainPage()
        {
            this.InitializeComponent();
            SetupTimers();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.DataContext = _model;
            await RestoreSettings();
            await LoadData();
            await LoadDirectoryItems();

            await Task.Delay(500); // Small delay to ensure UI is ready before trying to focus elements
            if (_model.Playlists.Count == 0)
            {
                splitView.IsPaneOpen = true;
                addEditPlaylist.Visibility = Visibility.Visible;
                listPlaylists.Visibility = Visibility.Collapsed;
                playlistUrlTextBox.Focus(FocusState.Programmatic);

            }
            else if (_model.SelectedChannel == null || _model.CurrentPlaylist == null)
            {
                splitView.IsPaneOpen = true;
                listPlaylists.Visibility = Visibility.Visible;
                addEditPlaylist.Visibility = Visibility.Collapsed;
                playlistsListView.Focus(FocusState.Programmatic);
            }
            else
            {
                channelsListView.Focus(FocusState.Programmatic);
            }

            try
            {
                _dispRequest.RequestActive();
            }
            catch
            {
            }

                
        }

        private async Task LoadDirectoryItems()
        {
            var tasks = new List<Task<List<DirectoryItem>>>
            {
                DataLoader.LoadIptvOrgFeed("categories"),
                DataLoader.LoadIptvOrgFeed("countries"),
                DataLoader.LoadIptvOrgFeed("subdivisions"),
                DataLoader.LoadIptvOrgFeed("languages"),
            };
            await Task.WhenAll(tasks);
            
            ipTvOrgCategories = tasks[0].Result;
            ipTvOrgCountries = tasks[1].Result;
            ipTvOrgSubdivisions = tasks[2].Result;
            ipTvOrgLangauges = tasks[3].Result;

            MjhNzPlaylists = new List<DirectoryItem>
            {
                new DirectoryItem { Id = "https://i.mjh.nz/au/Adelaide/kodi-tv.m3u8", Name = "AU - Adelaide" },
                new DirectoryItem { Id = "https://i.mjh.nz/au/Brisbane/kodi-tv.m3u8", Name = "AU - Brisbane" },
                new DirectoryItem { Id = "https://i.mjh.nz/au/Canberra/kodi-tv.m3u8", Name = "AU - Canberra" },
                new DirectoryItem { Id = "https://i.mjh.nz/au/Darwin/kodi-tv.m3u8", Name = "AU - Darwin" },
                new DirectoryItem { Id = "https://i.mjh.nz/au/Hobart/kodi-tv.m3u8", Name = "AU - Hobart" },
                new DirectoryItem { Id = "https://i.mjh.nz/au/Melbourne/kodi-tv.m3u8", Name = "AU - Melbourne" },
                new DirectoryItem { Id = "https://i.mjh.nz/au/Perth/kodi-tv.m3u8", Name = "AU - Perth" },
                new DirectoryItem { Id = "https://i.mjh.nz/au/Sydney/kodi-tv.m3u8", Name = "AU - Sydney" },
                new DirectoryItem { Id = "https://i.mjh.nz/nz/kodi-tv.m3u8", Name = "New Zealand" },
                new DirectoryItem { Id = "https://i.mjh.nz/world/raw.m3u8", Name = "World" }
            };

            _model.PrimaryDirectoryItems = ipTvOrgCategories;
            playlistSourceCombo.IsEnabled = true;
        }

        private async Task LoadChannelsForPlaylist(Playlist playlist)
        {
            await DataLoader.LoadChannelsFromTvIrlPlaylist(playlist);
            if (playlist.SavedIncludedChannelIds != null)
            {
                foreach (var channel in playlist.Channels)
                {
                    channel.Included = playlist.SavedIncludedChannelIds.Contains(channel.Id);
                }
                playlist.RaisePropertyChanged(nameof(Playlist.IncludedChannels));
            }
            if (playlist.EpgUrl != null)
            {
                await DataLoader.LoadTvPrograms(_model);
            }
        }
        private async Task LoadData()
        {
            if (_model.CurrentPlaylist == null)
            {
                return;
            }
            else
            {
                try
                {
                    await LoadChannelsForPlaylist(_model.CurrentPlaylist);
                    _model.SelectedChannel = _model.CurrentPlaylist?.Channels.Where(c => c.Id == _model.LastChannelId).FirstOrDefault();
                    if (_model.SelectedChannel == null)
                    {
                        _model.SelectedChannel = _model.CurrentPlaylist?.Channels.First();
                    }
                    playlistErrorTextBlock.Visibility = Visibility.Collapsed;
                    splitView.IsPaneOpen = false;

                    await SaveSettings();
                    ShowControls();
                    await PlayChannel();


                }
                catch (Exception ex)
                {
                    splitView.IsPaneOpen = true;
                    playlistErrorTextBlock.Text = ex.Message;
                    playlistErrorTextBlock.Visibility = Visibility.Visible;
                }
            }
        }



        private Playlist unsavedPlaylist;

        private async void ApplyPlaylistUrl_Click(object sender, RoutedEventArgs e)
        {
            playlistSourceCombo.SelectedIndex = -1; // Reset the playlist source dropdown to avoid confusion about where the playlist came from
            await ApplyPlaylist();
        }

        private void SetAddEditPanelMode(bool? isAddMode, bool isPlayListLoaded)
        {
            if (isAddMode == true)
            {
                addEditHeading.Text = "Add Playlist";
                addPlaylistRadioButtons.Visibility = Visibility.Visible;
                addFromDirectoryOptions.Visibility = directoryRadioButton.IsChecked.Value ? Visibility.Visible : Visibility.Collapsed;
                addFromUrlOptions.Visibility = urlRadioButton.IsChecked.Value ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (isAddMode == false)
            {
                addEditHeading.Text = "Edit Playlist";
                addPlaylistRadioButtons.Visibility = Visibility.Collapsed;
                addFromDirectoryOptions.Visibility = Visibility.Collapsed;
            }
            // Leave it alone if it's null
            
            if (isPlayListLoaded)
            {
                playlistNameCaption.Visibility = Visibility.Visible;
                playlistName.Visibility = Visibility.Visible;
                includeChannelsCaption.Visibility = Visibility.Visible;
                includeChannelsListView.Visibility = Visibility.Visible;
                selectAllCheckBox.Visibility = Visibility.Visible;
                saveButton.IsEnabled = true;
            }
            else
            {
                playlistNameCaption.Visibility = Visibility.Collapsed;
                playlistName.Visibility = Visibility.Collapsed;
                includeChannelsCaption.Visibility = Visibility.Collapsed;
                includeChannelsListView.Visibility = Visibility.Collapsed;
                selectAllCheckBox.Visibility = Visibility.Collapsed;
                saveButton.IsEnabled = false;
            }
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
                await SaveSettings();
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
            // Make it easy to get out of the playlist edit mode with a gamepad
            if (e.Key == VirtualKey.GamepadX && splitView.IsPaneOpen && (addEditPlaylist.Visibility == Visibility.Visible) && saveButton.IsEnabled)
            {
                saveButton_Click(saveButton, new RoutedEventArgs());
            }
            else if (e.Key == VirtualKey.GamepadB && splitView.IsPaneOpen && (addEditPlaylist.Visibility == Visibility.Visible))
            {
                cancelButton_Click(cancelButton, new RoutedEventArgs());
            }
            else if (FocusManager.GetFocusedElement() is TextBox || FocusManager.GetFocusedElement() is Button || FocusManager.GetFocusedElement() is CheckBox)
            {
                return;
            }
            if (e.Key == Windows.System.VirtualKey.Space || e.Key == Windows.System.VirtualKey.GamepadX ||
                e.Key == Windows.System.VirtualKey.X && !splitView.IsPaneOpen)
            {
                ShowControls();
            }
            else if (e.Key == VirtualKey.Escape)
            {
                HideControls();
            }
            else if (e.Key == Windows.System.VirtualKey.Y || e.Key == Windows.System.VirtualKey.GamepadY ||
                e.Key == Windows.System.VirtualKey.GamepadView && !splitView.IsPaneOpen)
            {
                mediaElement.TransportControls.Focus(FocusState.Programmatic);
            }
            else if (e.Key == VirtualKey.Enter && !splitView.IsPaneOpen)
            {
                ChangeChannelNumber();
            }
            else if (NumericKeys.ContainsKey(e.Key))
            {
                ProcessChannelNumberInput(NumericKeys[e.Key]);
            }
            else if (e.Key == VirtualKey.GamepadMenu)
            {
                if (!splitView.IsPaneOpen)
                {
                    splitView.IsPaneOpen = true;
                    addEditPlaylist.Visibility = Visibility.Collapsed;
                    listPlaylists.Visibility = Visibility.Visible;
                    topControls.Visibility = Visibility.Collapsed;
                }
                else
                {
                    splitView.IsPaneOpen = false;
                }
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
                mediaElement.AreTransportControlsEnabled = true;
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

        private async Task RestoreSettings()
        {
            // await ApplicationData.Current.ClearAsync(); // For testing
            var lastUrl = (string)ApplicationData.Current.LocalSettings.Values["PlaylistUrl"];
            _model.LastChannelId = (string)ApplicationData.Current.LocalSettings.Values["LastChannel"];
            _model.Playlists = new ObservableCollection<Playlist>();
            
            var playlistUrlsString = (string)ApplicationData.Current.LocalSettings.Values["RecentPlaylistUrls"];
            var playlistUrls = String.IsNullOrWhiteSpace(playlistUrlsString) ? null : playlistUrlsString.Split("|").ToList();
            var playlistNamesString = (string)ApplicationData.Current.LocalSettings.Values["PlaylistNames"];
            var playlistNames = String.IsNullOrWhiteSpace(playlistNamesString) ? null : playlistNamesString.Split("|").ToList();

            string includedChannelsRaw = null;
            await _settingsFileLock.WaitAsync();
            try
            {
                try
                {
                    var file = await ApplicationData.Current.LocalFolder.GetFileAsync("PlaylistIncludedChannels.dat");
                    includedChannelsRaw = await FileIO.ReadTextAsync(file);
                }
                catch (FileNotFoundException)
                {
                    // Fallback for migration
                    includedChannelsRaw = (string)ApplicationData.Current.LocalSettings.Values["PlaylistIncludedChannels"];
                }
            }
            finally
            {
                _settingsFileLock.Release();
            }

            var playlistIncludedChannels = includedChannelsRaw?.Split("|").ToList();

            if (playlistNames == null)
            {
                playlistNames = playlistUrls;
            }

            if (playlistUrls != null)
            {
                for (int i = 0; i < playlistUrls.Count; i++)
                {
                    var playlist = new Playlist();
                    playlist.Url = playlistUrls[i];
                    playlist.Name = playlistNames[i];
                    if (playlistIncludedChannels != null && i < playlistIncludedChannels.Count && !string.IsNullOrEmpty(playlistIncludedChannels[i]))
                    {
                        playlist.SavedIncludedChannelIds = playlistIncludedChannels[i].Split('~').ToList();
                    }
                    _model.Playlists.Add(playlist);
                }
            }

            _model.CurrentPlaylist = _model.Playlists.Where(p => p.Url == lastUrl).FirstOrDefault();
        }

        

        private async Task SaveSettings()
        {
            ApplicationData.Current.LocalSettings.Values["LastChannel"] = _model.LastChannelId;
            ApplicationData.Current.LocalSettings.Values["PlaylistUrl"] = _model.CurrentPlaylist?.Url;

            if (_model.Playlists != null)
            {
                // Join URLs and Names with pipe separator to match RestoreSettings logic
                string playlistUrls = string.Join("|", _model.Playlists.Select(p => p.Url));
                // Provide a fallback to Url if Name is null to ensure the lists stay aligned
                string playlistNames = string.Join("|", _model.Playlists.Select(p => p.Name ?? p.Url));

                // Save included channels mapping
                var includedChannelsList = new List<string>();
                foreach (var playlist in _model.Playlists)
                {
                    if (playlist.Channels != null)
                    {
                        // Update the saved list from current state
                        playlist.SavedIncludedChannelIds = playlist.Channels.Where(c => c.Included).Select(c => c.Id).ToList();
                    }

                    if (playlist.SavedIncludedChannelIds != null)
                    {
                        includedChannelsList.Add(string.Join("~", playlist.SavedIncludedChannelIds));
                    }
                    else
                    {
                        includedChannelsList.Add("");
                    }
                }
                string playlistIncludedChannels = string.Join("|", includedChannelsList);

                ApplicationData.Current.LocalSettings.Values["RecentPlaylistUrls"] = playlistUrls;
                ApplicationData.Current.LocalSettings.Values["PlaylistNames"] = playlistNames;

                await _settingsFileLock.WaitAsync();
                try
                {
                    var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("PlaylistIncludedChannels.dat", CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteTextAsync(file, playlistIncludedChannels);
                }
                finally
                {
                    _settingsFileLock.Release();
                }
            }
        }


        private void AddPlaylist_Click(object sender, RoutedEventArgs e)
        {
            playlistUrlTextBox.Text = "";
            playlistUrlTextBox.IsEnabled = true;
            listPlaylists.Visibility = Visibility.Collapsed;
            SetAddEditPanelMode(true, false);
            loadPlaylistButton.Visibility = Visibility.Visible;
            addEditPlaylist.Visibility = Visibility.Visible;
            directoryRadioButton.IsChecked = true;
            directoryRadioButton.Focus(FocusState.Programmatic);
            playlistsPrimaryCombo.SelectedIndex = -1;
            addFromDirectoryOptions.Visibility = Visibility.Visible;
            addFromUrlOptions.Visibility = Visibility.Collapsed;
        }

        private async void saveButton_Click(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = false;
            listPlaylists.Visibility = Visibility.Visible;
            addEditPlaylist.Visibility = Visibility.Collapsed;

            unsavedPlaylist.Name = String.IsNullOrEmpty(playlistName.Text) ? unsavedPlaylist.Url : playlistName.Text;

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
            await SaveSettings();


            ShowControls();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = false;
            listPlaylists.Visibility = Visibility.Visible;
            addEditPlaylist.Visibility = Visibility.Collapsed;
        }

        private async void editPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            var playlist = ((Button)sender).DataContext as Playlist;
            if (playlist.Channels == null)
            {
                await LoadChannelsForPlaylist(playlist);
            }
            unsavedPlaylist = (Playlist)playlist.Clone();
            playlistUrlTextBox.Text = unsavedPlaylist.Url;
            playlistUrlTextBox.IsEnabled = false;
            playlistName.Text = unsavedPlaylist.Name;
            loadPlaylistButton.Visibility = Visibility.Collapsed;
            includeChannelsListView.ItemsSource = unsavedPlaylist.Channels;
            listPlaylists.Visibility = Visibility.Collapsed;
            SetAddEditPanelMode(false, true);
            addEditPlaylist.Visibility = Visibility.Visible;
            playlistName.Focus(FocusState.Programmatic);
        }

        private async void deletePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            var playlist = ((Button)sender).DataContext as Playlist;
            _model.Playlists.Remove(playlist);
            if (_model.CurrentPlaylist == playlist)
            {
                _model.CurrentPlaylist = _model.Playlists.FirstOrDefault();
                await PlayChannel();
                ShowControls();
            }
            await SaveSettings();
        }

        private async void playlistsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var playlist = (Playlist)playlistsListView.SelectedItem;
            if (playlist == null)
            {
                return;
            }

            // Focus the playlistButton inside the selected container
            if (playlistsListView.ContainerFromItem(playlist) is ListViewItem container)
            {
                var button = FindChildByName<Button>(container, "playlistButton");
                button?.Focus(FocusState.Programmatic);
            }
        }

        private static T FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                    return element;
                var result = FindChildByName<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private async void playlistButton_Click(object sender, RoutedEventArgs e)
        {
            var playlist = (Playlist)((Button)sender).DataContext;
            playlistsListView.SelectedItem = playlist;
            await SelectPlaylist(playlist);
        }



        private async Task SelectPlaylist(Playlist playlist)
        {
            _model.CurrentPlaylist = playlist;
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



        private void SetSaveButtonEnabledStateForSelectedChannels()
        {
           // Disable the save button if more than 1000 channels are selected to avoid performance issues with loading/saving and potential crashes
            if (unsavedPlaylist.Channels != null && unsavedPlaylist.Channels.Count(c => c.Included) > 1000)
            {
                saveButton.IsEnabled = false;
                playlistErrorTextBlock.Text = "You cannot include more than 1000 channels in a playlist. Please deselect some channels before saving.";
                playlistErrorTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                saveButton.IsEnabled = true;
                playlistErrorTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void directoryRadioButton_Click(object sender, RoutedEventArgs e)
        {
            addFromDirectoryOptions.Visibility = Visibility.Visible;
            addFromUrlOptions.Visibility = Visibility.Collapsed;
        }

        private void urlRadioButton_Click(object sender, RoutedEventArgs e)
        {
            addFromDirectoryOptions.Visibility = Visibility.Collapsed;
            addFromUrlOptions.Visibility = Visibility.Visible;
        }

        private void playlistSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (playlistsPrimaryComboLabel is null) // Can happen during initialization before XAML components are loaded
            {
                return;
            }
            if (playlistSourceCombo.SelectedIndex == 0) // iptv-org Categories
            {
                playlistsPrimaryComboLabel.Text = "Category";
                playlistsSecondaryComboLabel.Visibility = Visibility.Collapsed;
                playlistsSecondaryCombo.Visibility = Visibility.Collapsed;
                _model.PrimaryDirectoryItems = ipTvOrgCategories;
            }
            else if (playlistSourceCombo.SelectedIndex == 1) // iptv-org Countries
            {
                playlistsPrimaryComboLabel.Text = "Country";
                playlistsSecondaryComboLabel.Visibility = Visibility.Visible;
                playlistsSecondaryCombo.Visibility = Visibility.Visible;
                _model.PrimaryDirectoryItems = ipTvOrgCountries;
            }
            if (playlistSourceCombo.SelectedIndex == 2) // iptv-org Languages
            {
                playlistsPrimaryComboLabel.Text = "Language";
                playlistsSecondaryComboLabel.Visibility = Visibility.Collapsed;
                playlistsSecondaryCombo.Visibility = Visibility.Collapsed;
                _model.PrimaryDirectoryItems = ipTvOrgLangauges;
            }
            else if (playlistSourceCombo.SelectedIndex == 3) // Mjh
            {
                playlistsPrimaryComboLabel.Text = "Playlist";
                playlistsSecondaryComboLabel.Visibility = Visibility.Collapsed;
                playlistsSecondaryCombo.Visibility = Visibility.Collapsed;
                _model.PrimaryDirectoryItems = MjhNzPlaylists;
            }
            SetAddEditPanelMode(true, false);
        }

        private async void playlistsPrimaryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (playlistsPrimaryCombo.SelectedItem is DirectoryItem selectedItem)
            {
                if (playlistSourceCombo.SelectedIndex == 1) // iptv-org Countries
                {
                    var secondaryItems = new List<DirectoryItem>();
                    secondaryItems.Add(new DirectoryItem { Id = selectedItem.Id, Name = "All" });
                    secondaryItems.AddRange(ipTvOrgSubdivisions.Where(s => s.ParentId == selectedItem.Id).ToList());
                    _model.SecondaryDirectoryItems = secondaryItems;
                    playlistsSecondaryCombo.SelectedIndex = 0;
                }
                else
                {
                    _model.SecondaryDirectoryItems = null;
                    await ApplyPlaylist();
                }
            }
        }

        private async Task ApplyPlaylist()
        {
            switch (playlistSourceCombo.SelectedIndex)
            {
                case 0: // iptv-org Categories
                    unsavedPlaylist = new Playlist()
                    {
                        Name = ((DirectoryItem)playlistsPrimaryCombo.SelectedItem).Name,
                        Url = $"https://iptv-org.github.io/iptv/categories/{((DirectoryItem)playlistsPrimaryCombo.SelectedItem).Id}.m3u"
                    };
                    break;
                case 1: // iptv-org Countries
                    if (playlistsSecondaryCombo.SelectedItem is DirectoryItem subdivision)
                    {
                        if (playlistsSecondaryCombo.SelectedIndex == 0) // All
                        {
                            unsavedPlaylist = new Playlist()
                            {
                                Name = ((DirectoryItem)playlistsPrimaryCombo.SelectedItem).Name,
                                Url = $"https://iptv-org.github.io/iptv/countries/{subdivision.Id}.m3u"
                            };
                        }
                        else
                        {
                            unsavedPlaylist = new Playlist()
                            {
                                Name = subdivision.Name,
                                Url = $"https://iptv-org.github.io/iptv/subdivisions/{subdivision.Id}.m3u"
                            };
                        }
                    }
                    else
                    {
                        unsavedPlaylist = new Playlist()
                        {
                            Name = ((DirectoryItem)playlistsPrimaryCombo.SelectedItem).Name,
                            Url = $"https://iptv-org.github.io/iptv/countries/{((DirectoryItem)playlistsPrimaryCombo.SelectedItem).Id}.m3u"
                        };
                    }
                    break;
                case 2: // iptv-org Languages
                    unsavedPlaylist = new Playlist()
                    {
                        Name = ((DirectoryItem)playlistsPrimaryCombo.SelectedItem).Name,
                        Url = $"https://iptv-org.github.io/iptv/languages/{((DirectoryItem)playlistsPrimaryCombo.SelectedItem).Id}.m3u"
                    };
                    break;
                case 3: // Mjh
                    unsavedPlaylist = new Playlist()
                    {
                        Name = ((DirectoryItem)playlistsPrimaryCombo.SelectedItem).Name,
                        Url = ((DirectoryItem)playlistsPrimaryCombo.SelectedItem).Id
                    };
                    break;
                default:
                    unsavedPlaylist = new Playlist()
                    {
                        Name = Regex.Match(playlistUrlTextBox.Text, "[^/]+?(?=\\.[^.]+$|$)").Value,
                        Url = playlistUrlTextBox.Text
                    };
                    break;
            }
            playlistName.Text = unsavedPlaylist.Name;
            try
            {
                playlistErrorTextBlock.Visibility = Visibility.Collapsed;
                await DataLoader.LoadChannelsFromTvIrlPlaylist(unsavedPlaylist);
                if (unsavedPlaylist.Channels?.Count > 0)
                {
                    SetAddEditPanelMode(null, true);
                    if (unsavedPlaylist.Channels.Count > 1000)
                    {
                        // Deselect all channels after the first 1000
                        unsavedPlaylist.Channels.Skip(1000).ToList().ForEach(c => c.Included = false); 
                    }
                    includeChannelsListView.ItemsSource = unsavedPlaylist.Channels;
                    SetSaveButtonEnabledStateForSelectedChannels();
                }
                else
                {
                    playlistErrorTextBlock.Text = "No channels found. Is it a proper IPTV playlist?";
                    includeChannelsListView.ItemsSource = new ObservableCollection<Channel>();
                    playlistErrorTextBlock.Visibility = Visibility.Visible;
                }

            }
            catch (Exception ex)
            {
                playlistErrorTextBlock.Text = ex.Message;
                playlistErrorTextBlock.Visibility = Visibility.Visible;
            }
        }

        private async void playlistsSecondaryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (playlistsSecondaryCombo.SelectedIndex >= 0)
            {
                await ApplyPlaylist();
            }
        }

        private void selectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Set all channels to included or not included based on the Select All checkbox state
            if (unsavedPlaylist.Channels != null)
            {
                bool includeAll = selectAllCheckBox.IsChecked == true;
                foreach (var channel in unsavedPlaylist.Channels)
                {
                    channel.Included = includeAll;
                }
                includeChannelsListView.ItemsSource = null;
                includeChannelsListView.ItemsSource = unsavedPlaylist.Channels;

            }
            SetSaveButtonEnabledStateForSelectedChannels();
        }

        private void includeChannelCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Focus the checkbox inside the selected container
            if (includeChannelsListView.ContainerFromItem(includeChannelsListView.SelectedItem) is ListViewItem container)
            {
                var checkBox = FindChildByName<CheckBox>(container, "includeChannelCheckBox");
                checkBox?.Focus(FocusState.Programmatic);
            }

            // Set the Select All checkbox state based on whether all channels are included or not
            if (unsavedPlaylist.Channels != null)
            {
                if (unsavedPlaylist.Channels.All(c => c.Included))
                {
                    selectAllCheckBox.IsChecked = true;
                }
                else if (unsavedPlaylist.Channels.All(c => !c.Included))
                {
                    selectAllCheckBox.IsChecked = false;
                }
                else
                {
                    selectAllCheckBox.IsChecked = null; // Indeterminate state
                }
            }

            // Prevent saving of playlists > 1000 channels
            SetSaveButtonEnabledStateForSelectedChannels();
        }
    }

}
