namespace QuickSticky
{
    /// <summary>
    /// A single backed-up note shown in the restore picker: its file path, a
    /// display name, and when it was deleted.
    /// </summary>
    public sealed class BackupEntry
    {
        public string Path { get; init; } = "";

        public string DisplayName { get; init; } = "";

        public string DeletedDisplay { get; init; } = "";
    }
}
