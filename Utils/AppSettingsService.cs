using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json; // For JsonConvert serialization/deserialization

namespace Utils
{
    public static class AppSettingsService
    {
        public static event EventHandler? SettingsChanged;

        private static readonly string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "application_settings.json");

        // The core service now manages a dictionary of module keys to JSON strings.
        private static readonly JsonSettingsService<Dictionary<string, string>> _dictionaryStorageService = new JsonSettingsService<Dictionary<string, string>>();

        private static JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto, // Important for potentially complex objects if not just POCOs
            Formatting = Formatting.Indented
        };

        public static void SaveModuleSettings(string moduleKey, object? settings)
        {
            if (string.IsNullOrWhiteSpace(moduleKey))
            {
                throw new ArgumentException("Module key cannot be null or whitespace.", nameof(moduleKey));
            }

            var settingsDictionary = _dictionaryStorageService.LoadSettings(_settingsFilePath, () => new Dictionary<string, string>());

            if (settings == null)
            {
                if (settingsDictionary.ContainsKey(moduleKey))
                {
                    settingsDictionary.Remove(moduleKey);
                }
            }
            else
            {
                string serializedSettings = JsonConvert.SerializeObject(settings, _jsonSerializerSettings);
                settingsDictionary[moduleKey] = serializedSettings;
            }

            _dictionaryStorageService.SaveSettings(settingsDictionary, _settingsFilePath);
            SettingsChanged?.Invoke(null, EventArgs.Empty);
        }

        public static T? LoadModuleSettings<T>(string moduleKey) where T : class // `new()` constraint removed to allow returning null if not found
        {
            if (string.IsNullOrWhiteSpace(moduleKey))
            {
                throw new ArgumentException("Module key cannot be null or whitespace.", nameof(moduleKey));
            }

            var settingsDictionary = _dictionaryStorageService.LoadSettings(_settingsFilePath, () => new Dictionary<string, string>());

            if (settingsDictionary.TryGetValue(moduleKey, out string? serializedSettings))
            {
                if (!string.IsNullOrEmpty(serializedSettings))
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<T>(serializedSettings, _jsonSerializerSettings);
                    }
                    catch (JsonException ex)
                    {
                        // Log error:($"Error deserializing settings for module {moduleKey}: {ex.Message}");
                        // Optionally, throw or return default/null based on desired error handling.
                        // For now, let it return null if deserialization fails.
                        Console.Error.WriteLine($"Error deserializing settings for module {moduleKey}: {ex.Message}");
                        return null;
                    }
                }
            }
            return null; // Key not found or serializedSettings is null/empty
        }

        // Optional: A method to load with a default if not found
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
