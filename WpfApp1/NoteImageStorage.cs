using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace QuickSticky
{
    internal static class NoteImageStorage
    {
        public static string GetAssetFolderPath(string notePath)
        {
            var directory = Path.GetDirectoryName(notePath) ?? "";
            var noteName = Path.GetFileNameWithoutExtension(notePath);
            return Path.Combine(directory, $"{noteName}_files");
        }

        public static string GetImagePath(string notePath, string fileName)
        {
            return Path.Combine(GetAssetFolderPath(notePath), Path.GetFileName(fileName));
        }

        public static string SaveClipboardBitmap(string notePath, BitmapSource bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            var assetFolder = GetAssetFolderPath(notePath);
            Directory.CreateDirectory(assetFolder);

            var fileName = GenerateUniqueImageFileName(assetFolder);
            var imagePath = Path.Combine(assetFolder, fileName);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(imagePath);
            encoder.Save(stream);

            return fileName;
        }

        public static BitmapImage LoadBitmap(string imagePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        public static void DeleteAssetFolder(string notePath)
        {
            try
            {
                var assetFolder = GetAssetFolderPath(notePath);

                if (Directory.Exists(assetFolder))
                    Directory.Delete(assetFolder, true);
            }
            catch
            {
                // Deleting the note should not fail because a stale asset cannot be removed.
            }
        }

        private static string GenerateUniqueImageFileName(string assetFolder)
        {
            for (int i = 1; i <= 9999; i++)
            {
                var fileName = $"img_{i:000}.png";

                if (!File.Exists(Path.Combine(assetFolder, fileName)))
                    return fileName;
            }

            return $"img_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.png";
        }
    }
}
