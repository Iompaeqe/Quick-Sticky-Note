using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuickSticky
{
    public enum UpdateStatus
    {
        UpToDate,
        UpdateAvailable,
        Failed
    }

    public sealed class UpdateCheckResult
    {
        public UpdateStatus Status { get; init; }
        public Version LatestVersion { get; init; }
        public string ReleaseUrl { get; init; } = UpdateService.ReleasesPageUrl;
        public string InstallerUrl { get; init; }
        public string InstallerName { get; init; }
    }

    /// <summary>
    /// Checks GitHub releases for a newer version and, when an installer asset is
    /// published, downloads it so the app can relaunch into the new setup.
    /// </summary>
    public static class UpdateService
    {
        public const string ReleasesPageUrl =
            "https://github.com/Iompaeqe/Quick-Sticky-Note";

        private const string LatestReleaseApi =
            "https://api.github.com/repos/Iompaeqe/Quick-Sticky-Note/releases/latest";

        private static readonly HttpClient Http = CreateClient();

        public static Version CurrentVersion =>
            Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

        public static string CurrentVersionDisplay => CurrentVersion.ToString(3);

        // The installer asset this build updates itself from, baked in at publish
        // time (framework-dependent vs. standalone). See WpfApp1.csproj.
        public static string ExpectedInstallerName { get; } = ReadExpectedInstallerName();

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                var json = await Http.GetStringAsync(LatestReleaseApi).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var latest = ParseVersion(GetString(root, "tag_name"));

                if (latest == null)
                    return new UpdateCheckResult { Status = UpdateStatus.Failed };

                var releaseUrl = GetString(root, "html_url");
                FindInstallerAsset(root, out var installerUrl, out var installerName);

                bool isNewer = latest > CurrentVersion;

                return new UpdateCheckResult
                {
                    Status = isNewer ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate,
                    LatestVersion = latest,
                    ReleaseUrl = string.IsNullOrWhiteSpace(releaseUrl) ? ReleasesPageUrl : releaseUrl,
                    InstallerUrl = installerUrl,
                    InstallerName = installerName
                };
            }
            catch
            {
                return new UpdateCheckResult { Status = UpdateStatus.Failed };
            }
        }

        /// <summary>
        /// Downloads the installer asset to a temp folder and returns its path,
        /// or null when there is no installer asset to download.
        /// </summary>
        public static async Task<string> DownloadInstallerAsync(UpdateCheckResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.InstallerUrl))
                return null;

            var directory = Path.Combine(Path.GetTempPath(), "QuickStickyUpdate");
            Directory.CreateDirectory(directory);

            var fileName = string.IsNullOrWhiteSpace(result.InstallerName)
                ? "QuickSticky_Update.exe"
                : Path.GetFileName(result.InstallerName);

            var filePath = Path.Combine(directory, fileName);

            var bytes = await Http.GetByteArrayAsync(result.InstallerUrl).ConfigureAwait(false);
            await File.WriteAllBytesAsync(filePath, bytes).ConfigureAwait(false);

            return filePath;
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

            // GitHub's API rejects requests without a User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("QuickSticky-Updater");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            return client;
        }

        private static void FindInstallerAsset(
            JsonElement root,
            out string installerUrl,
            out string installerName)
        {
            installerUrl = null;
            installerName = null;

            if (!root.TryGetProperty("assets", out var assets) ||
                assets.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            // Only match this build's own installer. If the release doesn't carry
            // it, installerUrl stays null and the UI falls back to the release page
            // rather than installing the wrong variant.
            foreach (var asset in assets.EnumerateArray())
            {
                var name = GetString(asset, "name");

                if (name != null &&
                    string.Equals(name, ExpectedInstallerName, StringComparison.OrdinalIgnoreCase))
                {
                    installerUrl = GetString(asset, "browser_download_url");
                    installerName = name;
                    return;
                }
            }
        }

        private static string ReadExpectedInstallerName()
        {
            const string fallback = "QuickSticky_Setup.exe";

            foreach (var attribute in Assembly.GetExecutingAssembly()
                         .GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (string.Equals(attribute.Key, "UpdateAssetName", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(attribute.Value))
                {
                    return attribute.Value;
                }
            }

            return fallback;
        }

        private static string GetString(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static Version ParseVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return null;

            tag = tag.Trim();

            if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                tag = tag.Substring(1);

            return Version.TryParse(tag, out var version) ? Normalize(version) : null;
        }

        // Compare on Major.Minor.Build only; ignore the revision field so tags like
        // "1.2.0" and assembly versions like "1.2.0.0" line up correctly.
        private static Version Normalize(Version version)
        {
            return new Version(
                Math.Max(0, version.Major),
                Math.Max(0, version.Minor),
                Math.Max(0, version.Build));
        }
    }
}
