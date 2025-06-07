using System;
using System.Collections.Generic;
// using System.Diagnostics; // Will be removed
using System.IO;
using System.Linq;
// System.Text and System.Threading.Tasks are not strictly needed by the final version of this file.
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using Reader.Models;

namespace Reader.Business
{
    public class Tools
    {
        private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
        };

        /// <summary>
        /// Finds the first parent of a given DependencyObject that is of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the parent to find.</typeparam>
        /// <param name="child">The starting DependencyObject.</param>
        /// <returns>The first parent of type T, or null if not found.</returns>
        public static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;
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

        /// <summary>
        /// Gets a list of subdirectories for a given path.
        /// If the path is null or empty, it defaults to the application's base directory.
        /// </summary>
        /// <param name="path">The path to search for directories.</param>
        /// <returns>A list of DirectoryInfo objects. Returns an empty list if an error occurs.</returns>
        public static List<DirectoryInfo> GetDirectories(string path)
        {
            string pathToLog = path;
            try
            {
                string effectivePath = path;
                if (string.IsNullOrEmpty(path))
                {
                    string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                    try
                    {
                        if (File.Exists(configFilePath))
                        {
                            string jsonContent = File.ReadAllText(configFilePath);
                            var appSettings = JsonSerializer.Deserialize<AppSettings>(jsonContent);
                            if (appSettings != null && !string.IsNullOrEmpty(appSettings.DefaultPath))
                            {
                                effectivePath = appSettings.DefaultPath;
                            }
                            else
                            {
                                effectivePath = AppDomain.CurrentDomain.BaseDirectory;
                            }
                        }
                        else
                        {
                            effectivePath = AppDomain.CurrentDomain.BaseDirectory;
                        }
                    }
                    catch (Exception) // Catch potential errors during file reading or deserialization
                    {
                        // Debug.WriteLine($"Error reading or parsing appsettings.json: {ex.Message}");
                        effectivePath = AppDomain.CurrentDomain.BaseDirectory; // Fallback
                    }
                    pathToLog = effectivePath; // Update pathToLog for logging purposes
                }
                return Directory.GetDirectories(effectivePath)
                                .Select(directoryPath => new DirectoryInfo(directoryPath))
                                .ToList();
            }
            catch (Exception ex)
            {
                // Debug.WriteLine($"Error getting directories from path '{pathToLog}': {ex.Message}");
                return new List<DirectoryInfo>();
            }
        }

        /// <summary>
        /// Gets the URI of the first supported image file in the specified directory, ordered by name.
        /// Supported extensions are defined in SupportedImageExtensions.
        /// </summary>
        /// <param name="directoryInfo">The directory to scan.</param>
        /// <returns>A Uri for the first image file, or null if no supported images are found or an error occurs.</returns>
        public static Uri? GetFirstImageInDirectory(DirectoryInfo directoryInfo)
        {
            if (directoryInfo == null)
            {
                // Debug.WriteLine("GetFirstImageInDirectory: directoryInfo was null.");
                return null;
            }

            try
            {
                var imageFiles = directoryInfo.EnumerateFiles()
                                              .Where(f => SupportedImageExtensions.Contains(f.Extension))
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
                // Debug.WriteLine($"Error getting first image in directory {directoryInfo.FullName}: {ex.Message}");
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
            catch (Exception ex)
            {
                // Debug.WriteLine($"Error getting image dimensions for {imagePath}: {ex.Message}");
                return (0, 0);
            }
        }
    }

    public class AppSettings
    {
        public string DefaultPath { get; set; }
        public string DefaultTabOverflowMode { get; set; }
        public NavigationMethod EnabledNavigationMethods { get; set; } = NavigationMethod.All;
    }
}
