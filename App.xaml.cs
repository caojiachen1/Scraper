using Microsoft.UI.Xaml;

namespace Scraper
{
    public partial class App : Application
    {
        public static MainWindow CurrentWindow = new();
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Provides access to the current window instance.
        /// </summary>

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Create a new window and set it as the current window
            CurrentWindow.Activate();
        }
    }
}
