using System;
using System.Windows.Controls;

namespace QuickSticky
{
    /// <summary>
    /// One entry in the settings navigation. The page is created lazily the first
    /// time the category is opened, then reused.
    /// </summary>
    internal sealed class SettingsCategory
    {
        private readonly Func<UserControl> _pageFactory;
        private UserControl _page;

        public SettingsCategory(string title, Func<UserControl> pageFactory)
        {
            Title = title;
            _pageFactory = pageFactory;
        }

        public string Title { get; }

        public UserControl GetPage() => _page ??= _pageFactory();
    }
}
