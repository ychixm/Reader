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
                    if (valueAsObject is System.Text.Json.JsonElement jsonElement)
                    {
                        if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Null)
                        {
                            return null; // Handles {"ModuleKey": null}
                        }
                        else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            string stringValue = jsonElement.GetString() ?? string.Empty;
                            if (string.IsNullOrEmpty(stringValue)) // Or if stringValue is like "{}" for an empty object vs "" for nothing
                            {
                                 // Depending on requirements, an empty string might mean 'defaults' or be an error.
                                 // Assuming here it might not be valid for deserializing to T, so return null (-> defaults).
                                 // If an empty string _could_ be valid JSON for some T, this might need adjustment.
                                return null;
                            }
                            try
                            {
                                // Attempt to deserialize the string content directly
                                return JsonSerializer.Deserialize<T>(stringValue, _jsonSerializerOptions);
                            }
                            catch (JsonException ex)
                            {
                                Serilog.Log.ForContext<AppSettingsService>().Error(ex, "Error deserializing string-encoded JSON for module {ModuleKey} to {TypeName}", moduleKey, typeof(T).Name);
                                return null; // Fallback to default
                            }
                        }
                        // For JsonValueKind.Object, JsonValueKind.Array, etc. (i.e., already structured JSON)
                        // Fall through to the existing serialize-to-bytes then deserialize logic.
                        // This handles the case where config.json is already in the desired nested object format.
                    }
                    // If valueAsObject is not a JsonElement, or if it's a JsonElement that's not Null or String,
                    // it will be handled by the general serialize-to-bytes method.
                    // This might include primitive types if they were stored directly, or other complex types.

                    try // General case: valueAsObject is JsonElement (e.g. Object/Array) or potentially another type.
                    {
                        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(valueAsObject, _jsonSerializerOptions);
                        return JsonSerializer.Deserialize<T>(jsonBytes, _jsonSerializerOptions);
                    }
                    catch (JsonException ex)
                    {
                        Serilog.Log.ForContext<AppSettingsService>().Error(ex, "Error deserializing settings for module {ModuleKey} from object to {TypeName}", moduleKey, typeof(T).Name);
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
