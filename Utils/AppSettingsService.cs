using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Utils
{
    public static class AppSettingsService
    {
        public static event EventHandler? SettingsChanged;

        private static readonly string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "application_settings.json");

        private static readonly JsonSettingsService<Dictionary<string, string>> _dictionaryStorageService = new JsonSettingsService<Dictionary<string, string>>();

        private static JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        public static void SaveModuleSettings(string moduleKey, object? settings)
        {
            if (string.IsNullOrWhiteSpace(moduleKey))
            {
                throw new ArgumentException("Module key cannot be null or whitespace.", nameof(moduleKey));
            }

            // Changed line:
            var settingsDictionary = _dictionaryStorageService.LoadSettings(_settingsFilePath) ?? new Dictionary<string, string>();

            if (settings == null)
            {
                if (settingsDictionary.ContainsKey(moduleKey))
                {
                    settingsDictionary.Remove(moduleKey);
                }
            }
            else
            {
                string serializedSettings = JsonSerializer.Serialize(settings, settings.GetType(), _jsonSerializerOptions);
                settingsDictionary[moduleKey] = serializedSettings;
            }

            _dictionaryStorageService.SaveSettings(settingsDictionary, _settingsFilePath);
            SettingsChanged?.Invoke(null, EventArgs.Empty);
        }

        public static T? LoadModuleSettings<T>(string moduleKey) where T : class
        {
            if (string.IsNullOrWhiteSpace(moduleKey))
            {
                throw new ArgumentException("Module key cannot be null or whitespace.", nameof(moduleKey));
            }

            // Changed line:
            var settingsDictionary = _dictionaryStorageService.LoadSettings(_settingsFilePath) ?? new Dictionary<string, string>();

            if (settingsDictionary.TryGetValue(moduleKey, out string? serializedSettings))
            {
                if (!string.IsNullOrEmpty(serializedSettings))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<T>(serializedSettings, _jsonSerializerOptions);
                    }
                    catch (JsonException ex)
                    {
                        Console.Error.WriteLine($"Error deserializing settings for module {moduleKey} using System.Text.Json: {ex.Message}");
                        return null;
                    }
                }
            }
            return null;
        }

        public static T LoadModuleSettings<T>(string moduleKey, Func<T> defaultFactory) where T : class
        {
             T? result = LoadModuleSettings<T>(moduleKey);
             if (result == null)
             {
                 return defaultFactory();
             }
             return result;
        }
    }
}
