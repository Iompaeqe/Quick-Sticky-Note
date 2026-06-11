using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace QuickSticky
{
    public partial class BackupSettingsPage : UserControl
    {
        // Guards against the toggle's Click handler reacting to programmatic
        // state changes (initial load, or reverting a cancelled disable).
        private bool _suppressToggle;

        public BackupSettingsPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _suppressToggle = true;
            AutoBackupToggle.IsChecked = SettingsStore.Current.EnableAutoBackups;
            _suppressToggle = false;
        }

        private void AutoBackupToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressToggle)
                return;

            bool enabled = AutoBackupToggle.IsChecked == true;

            if (!enabled && !ConfirmDisableAutoBackups())
            {
                _suppressToggle = true;
                AutoBackupToggle.IsChecked = true;
                _suppressToggle = false;
                return;
            }

            SettingsStore.Current.EnableAutoBackups = enabled;
            SettingsStore.Save();
        }

        private bool ConfirmDisableAutoBackups()
        {
            var result = MessageBox.Show(
                Window.GetWindow(this),
                "With automatic backups off, deleting a note removes it immediately and it cannot be restored.\n\nTurn off automatic backups?",
                "Disable Automatic Backups",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            return result == MessageBoxResult.Yes;
        }

        private void RestoreBackups_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RestoreBackupsWindow
            {
                Owner = Window.GetWindow(this)
            };

            dialog.ShowDialog();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dir = NoteStorage.GetBackupsDirectory(App.NotesDir);
                Directory.CreateDirectory(dir);

                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    "The backups folder could not be opened.",
                    "Backups",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                Window.GetWindow(this),
                "This permanently deletes every backed-up note. This cannot be undone.\n\nDelete all backups?",
                "Delete All Backups",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                NoteStorage.DeleteAllBackups(App.NotesDir);

                MessageBox.Show(
                    Window.GetWindow(this),
                    "All backups have been deleted.",
                    "Delete All Backups",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show(
                    Window.GetWindow(this),
                    "The backups could not be deleted. Some files may be open in another program.",
                    "Delete All Backups",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
