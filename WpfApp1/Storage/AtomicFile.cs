using System;
using System.IO;

namespace QuickSticky
{
    /// <summary>
    /// Writes files by staging to a sibling temp file and swapping it in with an
    /// atomic same-volume move, so a crash mid-write never leaves a truncated file.
    /// </summary>
    internal static class AtomicFile
    {
        public static void WriteAllText(string path, string contents)
        {
            var directory = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(directory))
            {
                File.WriteAllText(path, contents);
                return;
            }

            Directory.CreateDirectory(directory);

            var tempPath = Path.Combine(
                directory,
                $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

            try
            {
                File.WriteAllText(tempPath, contents);
                File.Move(tempPath, path, overwrite: true);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
