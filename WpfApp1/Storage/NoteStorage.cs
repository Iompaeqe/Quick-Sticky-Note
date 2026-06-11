using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuickSticky
{
    public static class NoteStorage
    {
        private const string BackupFolderName = "Backups";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        public static string GenerateNewPath(string notesDir)
        {
            var file = $"Note_{DateTime.Now:yyyyMMdd_HHmmssfff}.qnote";
            return Path.Combine(notesDir, file);
        }

        /// <summary>
        /// Renames a note file to "{title}_{timestamp}.qnote", preserving the
        /// original timestamp suffix and moving the note's image folder along with
        /// it. Returns the (possibly unchanged) path. A blank title falls back to
        /// the "Note" prefix.
        /// </summary>
        public static string RenameToMatchTitle(string path, string title)
        {
            var directory = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(directory))
                return path;

            var extension = Path.GetExtension(path);
            var stamp = ExtractTimestamp(Path.GetFileNameWithoutExtension(path))
                        ?? DateTime.Now.ToString("yyyyMMdd_HHmmssfff");

            var prefix = SanitizeForFileName(title);

            if (prefix.Length == 0)
                prefix = "Note";

            var desiredName = $"{prefix}_{stamp}{extension}";

            if (string.Equals(Path.GetFileName(path), desiredName, StringComparison.OrdinalIgnoreCase))
                return path;

            var destination = GenerateAvailablePath(directory, desiredName);

            File.Move(path, destination);

            try
            {
                NoteImageStorage.MoveAssetFolder(path, destination);
            }
            catch
            {
                // Keep the note and its images together: undo the note move so the
                // caller's path stays valid.
                TryMoveFile(destination, path);
                throw;
            }

            return destination;
        }

        public static NoteModel Load(string path)
        {
            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<NoteModel>(json) ?? new NoteModel();
            model.Blocks ??= new();
            return model;
        }

        public static void Save(string path, NoteModel model)
        {
            var json = JsonSerializer.Serialize(model, SerializerOptions);
            AtomicFile.WriteAllText(path, json);
        }

        public static string MoveToBackupsAsRemoved(string path, NoteModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var backupPath = GenerateBackupPath(path);
            var previousRemoved = model.Removed;
            var previousRemovedAtUtc = model.RemovedAtUtc;

            model.Removed = true;
            model.RemovedAtUtc = DateTime.UtcNow;

            Save(path, model);

            try
            {
                File.Move(path, backupPath);
                NoteImageStorage.MoveAssetFolder(path, backupPath);
                return backupPath;
            }
            catch
            {
                TryRestoreMovedNote(path, backupPath);
                RestoreRemovedState(path, model, previousRemoved, previousRemovedAtUtc);
                throw;
            }
        }

        public static void Delete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            NoteImageStorage.DeleteAssetFolder(path);
        }

        public static string GetBackupsDirectory(string notesDir)
        {
            return Path.Combine(notesDir, BackupFolderName);
        }

        public static void DeleteAllBackups(string notesDir)
        {
            var backupsDir = GetBackupsDirectory(notesDir);

            if (Directory.Exists(backupsDir))
                Directory.Delete(backupsDir, recursive: true);
        }

        public static string[] EnumerateBackups(string notesDir)
        {
            var backupsDir = GetBackupsDirectory(notesDir);

            return Directory.Exists(backupsDir)
                ? Directory.GetFiles(backupsDir, "*.qnote")
                : Array.Empty<string>();
        }

        /// <summary>
        /// Moves a backed-up note (and its asset folder) back into the notes
        /// directory and clears the removed mark. Returns the restored path.
        /// </summary>
        public static string RestoreFromBackup(string backupPath, string notesDir, NoteModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            Directory.CreateDirectory(notesDir);

            var destinationPath = GenerateAvailablePath(notesDir, Path.GetFileName(backupPath));

            // Clear the mark and persist it to the backup file before moving, so an
            // interrupted move still leaves a consistent (un-removed) note behind.
            model.Removed = false;
            model.RemovedAtUtc = null;
            Save(backupPath, model);

            File.Move(backupPath, destinationPath);
            NoteImageStorage.MoveAssetFolder(backupPath, destinationPath);

            return destinationPath;
        }

        private static string GenerateBackupPath(string path)
        {
            var backupDirectory = GetBackupDirectory(path);
            Directory.CreateDirectory(backupDirectory);

            return GenerateAvailablePath(backupDirectory, Path.GetFileName(path));
        }

        private static string GenerateAvailablePath(string targetDirectory, string fileName)
        {
            var candidate = Path.Combine(targetDirectory, fileName);

            if (IsNotePathAvailable(candidate))
                return candidate;

            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");

            for (int i = 1; i <= 9999; i++)
            {
                candidate = Path.Combine(
                    targetDirectory,
                    $"{baseName}_{timestamp}_{i:000}{extension}");

                if (IsNotePathAvailable(candidate))
                    return candidate;
            }

            return Path.Combine(
                targetDirectory,
                $"{baseName}_{timestamp}_{Guid.NewGuid():N}{extension}");
        }

        private static string GetBackupDirectory(string path)
        {
            var noteDirectory = Path.GetDirectoryName(path) ?? "";
            return Path.Combine(noteDirectory, BackupFolderName);
        }

        private static bool IsNotePathAvailable(string path)
        {
            return !File.Exists(path) &&
                   !Directory.Exists(NoteImageStorage.GetAssetFolderPath(path));
        }

        private static string ExtractTimestamp(string nameWithoutExtension)
        {
            // The trailing "yyyyMMdd_HHmmssfff" suffix: 8 digits, underscore, 9 digits.
            var match = Regex.Match(nameWithoutExtension, @"(\d{8}_\d{9})$");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string SanitizeForFileName(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "";

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(title.Length);

            foreach (var c in title.Trim())
            {
                if (Array.IndexOf(invalid, c) < 0)
                    builder.Append(c);
            }

            var cleaned = builder.ToString().Trim().TrimEnd('.', ' ');

            const int maxLength = 60;
            if (cleaned.Length > maxLength)
                cleaned = cleaned.Substring(0, maxLength).Trim().TrimEnd('.', ' ');

            return cleaned;
        }

        private static void TryMoveFile(string from, string to)
        {
            try
            {
                if (File.Exists(from) && !File.Exists(to))
                    File.Move(from, to);
            }
            catch
            {
                // Best-effort rollback only.
            }
        }

        private static void TryRestoreMovedNote(string originalPath, string backupPath)
        {
            try
            {
                if (!File.Exists(originalPath) && File.Exists(backupPath))
                    File.Move(backupPath, originalPath);
            }
            catch
            {
                // Best-effort rollback only. The original exception is more useful to callers.
            }
        }

        private static void RestoreRemovedState(
            string path,
            NoteModel model,
            bool removed,
            DateTime? removedAtUtc)
        {
            try
            {
                model.Removed = removed;
                model.RemovedAtUtc = removedAtUtc;

                if (File.Exists(path))
                    Save(path, model);
            }
            catch
            {
                // Best-effort rollback only. The original exception is more useful to callers.
            }
        }
    }
}
