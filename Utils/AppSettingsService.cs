using System;
using System.IO;
using Utils.Models; // For AllAppSettings

namespace Utils
{
    public static class AppSettingsService
    {
        public static event EventHandler? SettingsChanged;

        // Define the path for the centralized settings file
        private static readonly string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "application_settings.json");

        // Use the existing generic JsonSettingsService, now typed with AllAppSettings
        private static readonly JsonSettingsService<AllAppSettings> _jsonService = new JsonSettingsService<AllAppSettings>();

        public static AllAppSettings LoadApplicationSettings()
        {
            // Load settings; if the file doesn't exist or is invalid,
            // JsonSettingsService.LoadSettings should return a new instance of AllAppSettings
            // (assuming JsonSettingsService handles default object creation on error or file not found).
            // If JsonSettingsService requires a factory for default, that needs to be passed.
            // For now, assume it returns new T() if file not found.
            return _jsonService.LoadSettings(_settingsFilePath, () => new AllAppSettings());
        }

        public static void SaveApplicationSettings(AllAppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _jsonService.SaveSettings(settings, _settingsFilePath);
            SettingsChanged?.Invoke(null, EventArgs.Empty); // Raise event after saving
        }
    }
}
