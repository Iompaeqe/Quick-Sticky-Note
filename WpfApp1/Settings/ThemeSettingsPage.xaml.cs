using System.Windows.Controls;

namespace QuickSticky
{
    public partial class ThemeSettingsPage : UserControl
    {
        private bool _loading;

        public ThemeSettingsPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _loading = true;

            ThemeList.ItemsSource = ThemeManager.AvailableThemes;
            ThemeList.SelectedItem = ThemeManager.GetThemeOrDefault(SettingsStore.Current.ThemeId);

            _loading = false;
        }

        private void ThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || ThemeList.SelectedItem is not ThemeDefinition theme)
                return;

            ThemeManager.ApplyTheme(theme.Id);
        }

    }
}
