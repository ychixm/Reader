using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics; // For Debug.WriteLine

namespace ReaderUtils // Changed namespace
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
                Debug.WriteLine($"Error loading settings from {filePath}: {ex.Message}");
                return new T(); // Return default on error
            }
        }

        public void SaveSettings(T settingsData, string filePath)
        {
            if (settingsData == null) throw new ArgumentNullException(nameof(settingsData));

            try
            {
                string jsonContent = JsonSerializer.Serialize(settingsData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, jsonContent);
            }
            catch (Exception ex) // Catch potential errors during serialization or file writing
            {
                Debug.WriteLine($"Error saving settings to {filePath}: {ex.Message}");
                // Optionally re-throw or handle more gracefully
            }
        }
    }
}
