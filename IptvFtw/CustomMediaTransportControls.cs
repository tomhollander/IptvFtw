using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;

namespace IptvFtw
{
    public sealed class CustomMediaTransportControls : MediaTransportControls
    {
        public event EventHandler<EventArgs> SettingsClick;
        public event EventHandler<EventArgs> CompactClick;
        public event EventHandler<EventArgs> RestoreCompactClick;

        private Button _compactWindowButton;
        private Button _restoreCompactWindowButton;
        private Button _fullWindowButton;
        private Button _settingsButton;
        private Button _castButton;
        private double _defaultButtonWidth;

        public CustomMediaTransportControls()
        {
            this.DefaultStyleKey = typeof(CustomMediaTransportControls);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _settingsButton = GetTemplateChild("SettingsButton") as Button;
            _settingsButton.Click += SettingsButton_Click;

            _compactWindowButton = GetTemplateChild("CompactWindowButton") as Button;
            _compactWindowButton.Click += CompactWindowButton_Click;

            _restoreCompactWindowButton = GetTemplateChild("RestoreCompactWindowButton") as Button;
            _restoreCompactWindowButton.Click += _restoreCompactWindowButton_Click;

            _fullWindowButton = GetTemplateChild("FullWindowButton") as Button;
            _castButton = GetTemplateChild("CastButton") as Button;

            _defaultButtonWidth = _settingsButton.Width;
            SetDockButton();
        }

        private void SetDockButton()
        {
            _restoreCompactWindowButton.Width = 0;
            try
            {
                if (ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay))
                {
                    _compactWindowButton.Width = _defaultButtonWidth;
                }
                else
                {
                    _compactWindowButton.Width = 0;
                }
            }
            catch
            {
                _compactWindowButton.Width = 0;
            }
        }

        private async void CompactWindowButton_Click(object sender, RoutedEventArgs e)
        {
            bool modeSwitched = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);
            if (modeSwitched)
            {
                _compactWindowButton.Width = 0;
                _restoreCompactWindowButton.Width = _defaultButtonWidth;
                _fullWindowButton.Width = 0;
                _settingsButton.Width = 0;
                _castButton.Width = 0;
            }
            CompactClick?.Invoke(this, EventArgs.Empty);
        }

        private async void _restoreCompactWindowButton_Click(object sender, RoutedEventArgs e)
        {
            bool modeSwitched = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default);
            if (modeSwitched)
            {
                _compactWindowButton.Width = _defaultButtonWidth;
                _restoreCompactWindowButton.Width = 0;
                _fullWindowButton.Width = _defaultButtonWidth;
                _settingsButton.Width = _defaultButtonWidth;
                _castButton.Width = _defaultButtonWidth;
            }
            RestoreCompactClick?.Invoke(this, EventArgs.Empty);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Raise an event on the custom control when 'like' is clicked
            SettingsClick?.Invoke(this, EventArgs.Empty);
        }

    }
}
