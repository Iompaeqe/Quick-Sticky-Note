using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace QuickSticky
{
    public partial class AboutSettingsPage : UserControl
    {
        private UpdateCheckResult _result;
        private bool _checking;

        public AboutSettingsPage()
        {
            InitializeComponent();
            VersionText.Text = $"Version : {UpdateService.CurrentVersionDisplay}";
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_checking)
                return;

            _checking = true;

            UpdateButton.IsEnabled = false;
            UpdateButton.Content = "Checking for updates…";
            UpdateStatusText.Visibility = Visibility.Collapsed;

            _result = await UpdateService.CheckForUpdatesAsync();

            ApplyResult();
            _checking = false;
        }

        private void ApplyResult()
        {
            switch (_result.Status)
            {
                case UpdateStatus.UpToDate:
                    UpdateButton.Content = "Latest version";
                    UpdateButton.IsEnabled = false;
                    UpdateStatusText.Visibility = Visibility.Collapsed;
                    break;

                case UpdateStatus.UpdateAvailable:
                    UpdateButton.Content = $"Update to v{_result.LatestVersion.ToString(3)}";
                    UpdateButton.IsEnabled = true;
                    UpdateStatusText.Visibility = Visibility.Collapsed;
                    break;

                default:
                    UpdateButton.Content = "Check for updates";
                    UpdateButton.IsEnabled = true;
                    ShowStatus("Couldn't check for updates. Check your connection and try again.");
                    break;
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            // Up to date: the button is a no-op. Failed check: retry.
            if (_result == null || _result.Status == UpdateStatus.Failed)
            {
                await CheckForUpdatesAsync();
                return;
            }

            if (_result.Status != UpdateStatus.UpdateAvailable)
                return;

            await StartUpdateAsync();
        }

        private async Task StartUpdateAsync()
        {
            // No installer attached to the release: just open the releases page.
            if (string.IsNullOrWhiteSpace(_result.InstallerUrl))
            {
                OpenReleasePage();
                return;
            }

            var confirm = MessageBox.Show(
                Window.GetWindow(this),
                $"Version {_result.LatestVersion.ToString(3)} is available. Download and install it now?\n\nThe app will close so the update can be applied.",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                UpdateButton.IsEnabled = false;
                UpdateButton.Content = "Downloading update…";

                var installerPath = await UpdateService.DownloadInstallerAsync(_result);

                if (string.IsNullOrWhiteSpace(installerPath))
                {
                    OpenReleasePage();
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                });

                // Exit so the installer can replace the running files.
                Application.Current.Shutdown();
            }
            catch
            {
                UpdateButton.IsEnabled = true;
                UpdateButton.Content = $"Update to v{_result.LatestVersion.ToString(3)}";

                MessageBox.Show(
                    Window.GetWindow(this),
                    "The update could not be downloaded. Opening the releases page instead.",
                    "Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                OpenReleasePage();
            }
        }

        private void OpenReleasePage()
        {
            var url = string.IsNullOrWhiteSpace(_result?.ReleaseUrl)
                ? UpdateService.ReleasesPageUrl
                : _result.ReleaseUrl;

            OpenUrl(url);
        }

        private void ShowStatus(string text)
        {
            UpdateStatusText.Text = text;
            UpdateStatusText.Visibility = Visibility.Visible;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Nothing actionable if the shell can't open a browser.
            }
        }
    }
}
