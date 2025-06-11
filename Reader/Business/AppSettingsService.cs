using System.IO; 
using Reader.Models;
using Utils; 

namespace Reader.Business
{
    public static class AppSettingsService
    {
        public static event EventHandler? SettingsChanged;

        private static readonly string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        private static readonly JsonSettingsService<AppSettings> _jsonService = new JsonSettingsService<AppSettings>();

        public static AppSettings LoadAppSettings()
        {
            return _jsonService.LoadSettings(_settingsFilePath);
        }

        public static void SaveAppSettings(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _jsonService.SaveSettings(settings, _settingsFilePath);
            SettingsChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
