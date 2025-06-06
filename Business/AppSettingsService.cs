using System;
using System.IO;
using System.Text.Json; // Requires System.Text.Json NuGet package if not already part of the project

namespace Reader.Business
{
    public static class AppSettingsService
    {
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public static AppSettings LoadAppSettings()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string jsonContent = File.ReadAllText(ConfigFilePath);
                    // Ensure deserializer options are set if needed, e.g., for case insensitivity,
                    // but default should be fine for properties like "DefaultPath" and "DefaultTabOverflowMode".
                    return JsonSerializer.Deserialize<AppSettings>(jsonContent) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                // Log error (e.g., System.Diagnostics.Debug.WriteLine($"Error loading app settings: {ex.Message}");)
                // Fall through to return default settings
            }
            return new AppSettings(); // Return default (empty) settings if file doesn't exist or error occurs
        }

        public static void SaveAppSettings(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            try
            {
                // Configure JsonSerializerOptions for pretty printing (indented JSON)
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonContent = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(ConfigFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                // Log error (e.g., System.Diagnostics.Debug.WriteLine($"Error saving app settings: {ex.Message}");)
                // Consider how to handle save failures (e.g., notify user, retry)
            }
        }
    }
}
