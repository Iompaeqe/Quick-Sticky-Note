using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuickSticky
{
    public partial class SettingsWindow : Window
    {
        private static SettingsWindow _instance;

        private List<SettingsCategory> _categories;

        /// <summary>
        /// Opens the settings window, or brings the existing one to the front.
        /// Only one settings window exists at a time across all notes.
        /// </summary>
        public static void ShowSettings()
        {
            if (_instance != null)
            {
                if (_instance.WindowState == WindowState.Minimized)
                    _instance.WindowState = WindowState.Normal;

                _instance.Activate();
                return;
            }

            _instance = new SettingsWindow();
            _instance.Closed += (_, _) => _instance = null;
            _instance.Show();
        }

        public SettingsWindow()
        {
            InitializeComponent();
            InitializeCategories();
        }

        private void InitializeCategories()
        {
            _categories = new List<SettingsCategory>
            {
                new("Theme", () => new ThemeSettingsPage()),
                new("Backups", () => new BackupSettingsPage())
            };

            NavList.ItemsSource = _categories;
            NavList.SelectedIndex = 0;
        }

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavList.SelectedItem is SettingsCategory category)
                PageHost.Content = category.GetPage();
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
