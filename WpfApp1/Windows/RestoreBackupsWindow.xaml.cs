using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace QuickSticky
{
    public partial class RestoreBackupsWindow : Window
    {
        public RestoreBackupsWindow()
        {
            InitializeComponent();
            LoadBackups();
        }

        private void LoadBackups()
        {
            var entries = new List<BackupEntry>();

            foreach (var path in NoteStorage.EnumerateBackups(App.NotesDir))
            {
                try
                {
                    var model = NoteStorage.Load(path);
                    entries.Add(new BackupEntry
                    {
                        Path = path,
                        DisplayName = GetDisplayName(model),
                        DeletedDisplay = GetDeletedDisplay(model)
                    });
                }
                catch
                {
                    // Skip backups that fail to load rather than blocking the rest.
                }
            }

            entries.Sort((a, b) => string.CompareOrdinal(b.DeletedDisplay, a.DeletedDisplay));

            BackupList.ItemsSource = entries;

            bool hasBackups = entries.Count > 0;
            EmptyMessage.Visibility = hasBackups ? Visibility.Collapsed : Visibility.Visible;
            RestoreButton.IsEnabled = hasBackups;
        }

        private static string GetDisplayName(NoteModel model)
        {
            return string.IsNullOrWhiteSpace(model.Title) ? "Untitled note" : model.Title.Trim();
        }

        private static string GetDeletedDisplay(NoteModel model)
        {
            if (model.RemovedAtUtc is not DateTime removedUtc)
                return "Deleted at an unknown time";

            return "Deleted " + removedUtc.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (BackupList.SelectedItems.Count == 0)
                return;

            var app = (App)Application.Current;

            // Snapshot first: restoring mutates the list's backing collection.
            var selected = new List<BackupEntry>();
            foreach (BackupEntry entry in BackupList.SelectedItems)
                selected.Add(entry);

            foreach (var entry in selected)
            {
                try
                {
                    app.OpenNoteFile(entry.Path);
                }
                catch
                {
                    // Continue restoring the rest if one fails.
                }
            }

            Close();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowEffects.Apply(this);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }
    }
}
