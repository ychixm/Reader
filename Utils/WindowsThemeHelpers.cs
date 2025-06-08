using System;
using Microsoft.Win32;
using System.Diagnostics; // For Debug.WriteLine, if used in catch block
using System.Runtime.Versioning; // For SupportedOSPlatform attribute

namespace Utils // Changed namespace
{
    public static class WindowsThemeHelpers
    {
        [SupportedOSPlatform("windows")]
        public static bool GetCurrentSystemIsDarkMode()
        {
            try
            {
                // The AppsUseLightTheme registry value is 0 for dark mode, 1 for light mode.
                // Registry.GetValue returns null if the path or name does not exist.
                var registryValue = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme",
                    null); // Default value if not found

                if (registryValue is int appsUseLightTheme)
                {
                    return appsUseLightTheme == 0;
                }
            }
            catch (Exception ex)
            {
                // Log error or handle (e.g., Debug.WriteLine($"Error reading theme from registry: {ex.Message}"));
                // Default to light theme in case of any error.
                // Using ex in Debug.WriteLine to avoid CS0168 if uncommented.
                Debug.WriteLine($"Error reading theme from registry: {ex.Message}");
            }
            return false; // Default to light theme
        }
    }
}
