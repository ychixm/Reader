using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging; // For GetImageDimensions

namespace Reader.Utils
{
    public static class FileSystemHelpers
    {
        /// <summary>
        /// Gets a list of subdirectories for a given path.
        /// </summary>
        /// <param name="directoryPath">The path to search for directories.</param>
        /// <returns>A list of DirectoryInfo objects. Returns an empty list if an error occurs.</returns>
        public static List<DirectoryInfo> GetDirectories(string directoryPath)
        {
            try
            {
                return Directory.GetDirectories(directoryPath)
                                .Select(path => new DirectoryInfo(path))
                                .ToList();
            }
            catch (Exception) // Consider logging the exception ex
            {
                // Debug.WriteLine($"Error getting directories from path '{directoryPath}': {ex.Message}");
                return new List<DirectoryInfo>();
            }
        }

        /// <summary>
        /// Gets the URI of the first file in the specified directory matching given extensions, ordered by name.
        /// </summary>
        /// <param name="directoryInfo">The directory to scan.</param>
        /// <param name="validExtensions">A HashSet of valid extensions (e.g., ".jpg", ".png").</param>
        /// <returns>A Uri for the first matching file, or null if no supported files are found or an error occurs.</returns>
        public static Uri? GetFirstFileByExtensions(DirectoryInfo directoryInfo, HashSet<string> validExtensions)
        {
            if (directoryInfo == null)
            {
                // Debug.WriteLine("GetFirstFileByExtensions: directoryInfo was null.");
                return null;
            }
            if (validExtensions == null || validExtensions.Count == 0)
            {
                // Debug.WriteLine("GetFirstFileByExtensions: validExtensions was null or empty.");
                return null;
            }

            try
            {
                var files = directoryInfo.EnumerateFiles()
                                         .Where(f => validExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase)) // Ensure case-insensitivity if needed
                                         .OrderBy(f => f.Name)
                                         .ToList();

                if (files.Count == 0)
                {
                    return null;
                }
                return new Uri(files.First().FullName);
            }
            catch (Exception) // Consider logging the exception ex
            {
                // Debug.WriteLine($"Error getting first file by extensions in directory {directoryInfo.FullName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the pixel dimensions (width and height) of an image file.
        /// </summary>
        /// <param name="imagePath">The path to the image file.</param>
        /// <returns>A tuple containing the width and height. Returns (0,0) if an error occurs or path is invalid.</returns>
        public static (int width, int height) GetImageDimensions(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                // Debug.WriteLine("GetImageDimensions: imagePath was null or empty.");
                return (0,0);
            }

            try
            {
                using FileStream stream = new(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                BitmapFrame frame = decoder.Frames[0];
                return (frame.PixelWidth, frame.PixelHeight);
            }
            catch (Exception) // Consider logging the exception ex
            {
                // Debug.WriteLine($"Error getting image dimensions for {imagePath}: {ex.Message}");
                return (0, 0);
            }
        }
    }
}
