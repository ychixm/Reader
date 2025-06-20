using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Utils
{
    public static class AppSettingsService
    {
        public static event EventHandler? SettingsChanged;

        private static readonly string _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Assistant", "config.json");

        private static readonly JsonSettingsService<Dictionary<string, object>> _dictionaryStorageService = new JsonSettingsService<Dictionary<string, object>>();

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
            var settingsDictionary = _dictionaryStorageService.LoadSettings(_settingsFilePath) ?? new Dictionary<string, object>();

            if (settings == null)
            {
                if (settingsDictionary.ContainsKey(moduleKey))
                {
                    settingsDictionary.Remove(moduleKey);
                }
            }
            else
            {
                settingsDictionary[moduleKey] = settings; // Store the object directly
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
            var settingsDictionary = _dictionaryStorageService.LoadSettings(_settingsFilePath) ?? new Dictionary<string, object>();

            if (settingsDictionary.TryGetValue(moduleKey, out object? valueAsObject))
            {
                if (valueAsObject != null)
                {
                    try
                    {
                        // If valueAsObject is a JsonElement, it needs to be re-serialized and then deserialized to T
                        // This is a common way to convert JsonElement to a POCO.
                        // Ensure System.Text.Json.JsonSerializer is available.
                        // Using JsonSerializer.SerializeToUtf8Bytes and then Deserialize is efficient.
                        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(valueAsObject, _jsonSerializerOptions);
                        return JsonSerializer.Deserialize<T>(jsonBytes, _jsonSerializerOptions);
                    }
                    catch (JsonException ex)
                    {
                        Console.Error.WriteLine($"Error deserializing settings for module {moduleKey} from object to {typeof(T).Name}: {ex.Message}");
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
