using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;

namespace Reader.Business
{
    public class Tools
    {
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
            List<DirectoryInfo> directories = [];

            var directoryPaths = Directory.GetDirectories(path);

            foreach (var directoryPath in directoryPaths)
            {
                directories.Add(new DirectoryInfo(directoryPath));
            }

            return directories;
        }

        public static Uri? GetFirstImageInDirectory(DirectoryInfo directoryInfo)
        {
            try
            {
                var imageFiles = directoryInfo.EnumerateFiles()
                                              .Where(f => f.Extension.Equals(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                                                          f.Extension.Equals(".png", System.StringComparison.OrdinalIgnoreCase) ||
                                                          f.Extension.Equals(".bmp", System.StringComparison.OrdinalIgnoreCase) ||
                                                          f.Extension.Equals(".gif", System.StringComparison.OrdinalIgnoreCase) ||
                                                          f.Extension.Equals(".webp", System.StringComparison.OrdinalIgnoreCase))
                                              .OrderBy(f => f.Name)
                                              .ToList();

                if (imageFiles.Count == 0)
                {
                    return null;
                }
                return new Uri(imageFiles.First().FullName);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Error loading image: " + ex.Message);
                return null;
            }
        }

        public static (int width, int height) GetImageDimensions(string imagePath)
        {
            try
            {
                using FileStream stream = new(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                BitmapFrame frame = decoder.Frames[0];
                return (frame.PixelWidth, frame.PixelHeight);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting image dimensions: {ex.Message}");
                return (0, 0);
            }
        }
    }
}
