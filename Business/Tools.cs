using System;
using System.Collections.Generic;
using System.Diagnostics; // Added
using System.IO;
using System.Linq; // Was already present
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;

namespace Reader.Business
{
    public class Tools
    {
        private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" // Added .jpeg
        };

        public static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            if (parentObject is T parent)
            {
                return parent;
            }
            else
            {
                return FindParent<T>(parentObject);
            }
        }

        public static List<DirectoryInfo> GetDirectories(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    // Assuming path is usually root "" which Directory.GetDirectories handles.
                    // For truly invalid or empty user-supplied paths, further checks might be needed.
                    // Directory.GetDirectories("") will get top-level directories from current drive's working dir.
                    // If path is meant to be app root or specific root, ensure it's correctly formed.
                }
                return Directory.GetDirectories(path)
                                .Select(directoryPath => new DirectoryInfo(directoryPath))
                                .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting directories from path '{path}': {ex.Message}");
                return new List<DirectoryInfo>(); // Return empty list on error
            }
        }

        public static Uri? GetFirstImageInDirectory(DirectoryInfo directoryInfo)
        {
            try
            {
                if (directoryInfo == null)
                {
                    System.Diagnostics.Debug.WriteLine("GetFirstImageInDirectory: directoryInfo was null.");
                    return null;
                }

                var imageFiles = directoryInfo.EnumerateFiles()
                                              .Where(f => SupportedImageExtensions.Contains(f.Extension))
                                              .OrderBy(f => f.Name)
                                              .ToList(); // ToList() is okay here as we need to sort before First()

                if (imageFiles.Count == 0)
                {
                    return null;
                }
                return new Uri(imageFiles.First().FullName);
            }
            catch (Exception ex)
            {
                // Check directoryInfo for null again for the error message, though it's checked above.
                string dirFullName = directoryInfo?.FullName ?? "NULL_DIRECTORY_INFO";
                System.Diagnostics.Debug.WriteLine($"Error getting first image in directory {dirFullName}: {ex.Message}");
                return null;
            }
        }

        public static (int width, int height) GetImageDimensions(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                {
                    System.Diagnostics.Debug.WriteLine("GetImageDimensions: imagePath was null or empty.");
                    return (0,0);
                }
                using FileStream stream = new(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                BitmapFrame frame = decoder.Frames[0];
                return (frame.PixelWidth, frame.PixelHeight);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting image dimensions for {imagePath}: {ex.Message}");
                return (0, 0);
            }
        }
    }
}
