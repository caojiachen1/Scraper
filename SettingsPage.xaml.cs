using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace Scraper
{
    public sealed partial class SettingsPage : Page
    {
        private ApplicationDataContainer localSettings;

        public SettingsPage()
        {
            this.InitializeComponent();
            localSettings = ApplicationData.Current.LocalSettings;
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load Default Browser setting
            if (localSettings.Values.TryGetValue("DefaultBrowser", out object browser))
            {
                DefaultBrowserComboBox.SelectedIndex = (int)browser;
            }
        }

        private void DefaultBrowserComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            localSettings.Values["DefaultBrowser"] = DefaultBrowserComboBox.SelectedIndex;
        }
    }
}