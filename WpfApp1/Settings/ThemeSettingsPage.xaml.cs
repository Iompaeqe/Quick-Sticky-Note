using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

        private void ThemeList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = FindParent<ScrollViewer>(this);

            if (scrollViewer == null)
                return;

            e.Handled = true;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        }

        private static T FindParent<T>(System.Windows.DependencyObject child)
            where T : System.Windows.DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if (parent is T match)
                    return match;

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

    }
}
