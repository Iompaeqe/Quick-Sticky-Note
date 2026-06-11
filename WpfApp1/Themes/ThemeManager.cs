using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace QuickSticky
{
    public sealed class ThemeDefinition
    {
        public ThemeDefinition(string id, string name, string description, string resourcePath)
        {
            Id = id;
            Name = name;
            Description = description;
            ResourcePath = resourcePath;
        }

        public string Id { get; }

        public string Name { get; }

        public string Description { get; }

        internal string ResourcePath { get; }
    }

    public static class ThemeManager
    {
        public const string DefaultThemeId = "AcrylicGlass";

        private static readonly IReadOnlyList<ThemeDefinition> Themes =
            new[]
            {
                new ThemeDefinition(
                    DefaultThemeId,
                    "Acrylic Glass",
                    "The original translucent Quick Sticky Note look.",
                    "Themes/AcrylicGlass.xaml"),
                new ThemeDefinition(
                    "MicaSlate",
                    "Mica Slate",
                    "A restrained dark desktop utility style with solid surfaces.",
                    "Themes/MicaSlate.xaml"),
                new ThemeDefinition(
                    "PaperLantern",
                    "Paper Lantern",
                    "A warm paper-like light theme for long writing sessions.",
                    "Themes/PaperLantern.xaml"),
                new ThemeDefinition(
                    "MidnightOrchard",
                    "Midnight Orchard",
                    "A cozy dark developer-style theme with soft violet accents.",
                    "Themes/MidnightOrchard.xaml"),
                new ThemeDefinition(
                    "BlueprintGrid",
                    "Blueprint Grid",
                    "A compact technical theme with square panels and blueprint-style outlines.",
                    "Themes/BlueprintGrid.xaml"),
                new ThemeDefinition(
                    "BentoBloom",
                    "Bento Bloom",
                    "A soft roomy theme with rounded bento panels and pill-like controls.",
                    "Themes/BentoBloom.xaml"),
                new ThemeDefinition(
                    "SignalMono",
                    "Signal Mono",
                    "A high-contrast monochrome theme with hard edges and strong outlines.",
                    "Themes/SignalMono.xaml")
            };

        private static readonly HashSet<string> ThemeResourcePaths =
            Themes.Select(theme => theme.ResourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        public static event EventHandler ThemeChanged;

        public static IReadOnlyList<ThemeDefinition> AvailableThemes => Themes;

        public static ThemeDefinition CurrentTheme { get; private set; } = GetThemeOrDefault(DefaultThemeId);

        public static void ApplySavedTheme()
        {
            var savedThemeId = SettingsStore.Current.ThemeId;
            var requestedTheme = FindTheme(savedThemeId);

            ApplyTheme(savedThemeId, persist: false);

            if (requestedTheme == null)
            {
                SettingsStore.Current.ThemeId = CurrentTheme.Id;
                SettingsStore.Save();
            }
        }

        public static void ApplyTheme(string themeId, bool persist = true)
        {
            var theme = GetThemeOrDefault(themeId);
            var application = Application.Current;

            if (application == null)
                return;

            var dictionaries = application.Resources.MergedDictionaries;

            for (int i = dictionaries.Count - 1; i >= 0; i--)
            {
                var source = dictionaries[i].Source?.OriginalString;

                if (!string.IsNullOrWhiteSpace(source) && ThemeResourcePaths.Contains(source))
                    dictionaries.RemoveAt(i);
            }

            dictionaries.Insert(0, new ResourceDictionary
            {
                Source = new Uri(theme.ResourcePath, UriKind.Relative)
            });

            CurrentTheme = theme;

            if (persist && !string.Equals(SettingsStore.Current.ThemeId, theme.Id, StringComparison.Ordinal))
            {
                SettingsStore.Current.ThemeId = theme.Id;
                SettingsStore.Save();
            }

            WindowEffects.RefreshOpenWindows();
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static ThemeDefinition GetThemeOrDefault(string themeId)
        {
            return FindTheme(themeId) ?? Themes[0];
        }

        public static bool GetBlurEnabled()
        {
            return GetResource("ThemeBlurEnabled", true);
        }

        public static uint GetBlurTintColor()
        {
            var value = GetResource("ThemeBlurTintColor", "0x00000000");

            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value = value.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(2);

            return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        private static ThemeDefinition FindTheme(string themeId)
        {
            if (string.IsNullOrWhiteSpace(themeId))
                return null;

            return Themes.FirstOrDefault(
                theme => string.Equals(theme.Id, themeId, StringComparison.OrdinalIgnoreCase));
        }

        private static T GetResource<T>(string key, T fallback)
        {
            if (Application.Current?.TryFindResource(key) is T value)
                return value;

            return fallback;
        }
    }
}
