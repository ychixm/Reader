using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics; // For Debug.WriteLine

namespace Utils // Changed namespace
{
    public class JsonSettingsService<T> where T : class, new()
    {
        public T LoadSettings(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new T(); // Return default if file doesn't exist
            }
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                T? settings = JsonSerializer.Deserialize<T>(jsonContent);
                return settings ?? new T();
            }
            catch (Exception ex) // Catch potential errors during file reading or deserialization
            {
                Serilog.Log.ForContext<JsonSettingsService<T>>().Error(ex, "Error loading settings from {FilePath}", filePath);
                return new T(); // Return default on error
            }
        }

        public void SaveSettings(T settingsData, string filePath)
        {
            if (settingsData == null) throw new ArgumentNullException(nameof(settingsData));

            try
            {
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                }
                string jsonContent = JsonSerializer.Serialize(settingsData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, jsonContent);
            }
            catch (Exception ex) // Catch potential errors during serialization or file writing
            {
                Serilog.Log.ForContext<JsonSettingsService<T>>().Error(ex, "Error saving settings to {FilePath}", filePath);
                // Optionally re-throw or handle more gracefully
            }
        }
    }
}
