using System;
using System.IO;
using System.Text.Json;

namespace QuickSticky
{
    /// <summary>
    /// Loads and persists <see cref="AppSettings"/>. <see cref="Current"/> is the
    /// single in-memory source of truth; mutate it and call <see cref="Save"/>.
    /// </summary>
    public static class SettingsStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickSticky",
            "settings.json");

        private static AppSettings _current;

        public static AppSettings Current => _current ??= Load();

        private static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings != null)
                        return settings;
                }
            }
            catch
            {
                // Fall back to defaults if the settings file is missing or corrupt.
            }

            return new AppSettings();
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Current, SerializerOptions);
                AtomicFile.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // A failed settings write should never take down the app.
            }
        }
    }
}
