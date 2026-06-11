namespace QuickSticky
{
    /// <summary>
    /// Application-wide settings, persisted as JSON. Add new fields here with a
    /// sensible default; older settings files deserialize and keep the defaults.
    /// </summary>
    public class AppSettings
    {
        public int Version { get; set; } = 1;

        public string ThemeId { get; set; } = "AcrylicGlass";

        public bool EnableAutoBackups { get; set; } = true;
    }
}
