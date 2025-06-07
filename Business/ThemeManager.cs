using System.Windows; // Required for Application.Current
using Microsoft.Win32; // Required for Registry access
using System; // Required for Exception

namespace Reader.Business
{
    public static class ThemeManager
    {
        // private static bool _isDarkModeInitialized = false; // Removed CS0414
        // private static bool _cachedIsDarkMode; // Removed CS0169

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
            catch (Exception) // CS0168: ex not used
            {
                // Log error or handle (e.g., System.Diagnostics.Debug.WriteLine($"Error reading theme from registry: {ex.Message}"));
                // Default to light theme in case of any error.
            }
            return false; // Default to light theme
        }

        public static void ApplyTheme()
        {
            bool isDarkMode = GetCurrentSystemIsDarkMode();

            if (isDarkMode)
            {
                Application.Current.Resources["TabItemBackgroundBrush"] = Application.Current.Resources["TabItemBackground_Dark"];
                Application.Current.Resources["TabItemBorderBrush"] = Application.Current.Resources["TabItemBorderBrush_Dark"];
                Application.Current.Resources["TabItemForegroundBrush"] = Application.Current.Resources["TabItemForeground_Dark"];
                Application.Current.Resources["TabItemBackground_SelectedBrush"] = Application.Current.Resources["TabItemBackground_Selected_Dark"];
                Application.Current.Resources["TabItemForeground_SelectedBrush"] = Application.Current.Resources["TabItemForeground_Selected_Dark"];
                Application.Current.Resources["TabItemBackground_MouseOverBrush"] = Application.Current.Resources["TabItemBackground_MouseOver_Dark"];
                Application.Current.Resources["TabControlContentBackgroundBrush"] = Application.Current.Resources["TabControlContentBackground_Dark"];
            }
            else
            {
                Application.Current.Resources["TabItemBackgroundBrush"] = Application.Current.Resources["TabItemBackground_Light"];
                Application.Current.Resources["TabItemBorderBrush"] = Application.Current.Resources["TabItemBorderBrush_Light"];
                Application.Current.Resources["TabItemForegroundBrush"] = Application.Current.Resources["TabItemForeground_Light"];
                Application.Current.Resources["TabItemBackground_SelectedBrush"] = Application.Current.Resources["TabItemBackground_Selected_Light"];
                Application.Current.Resources["TabItemForeground_SelectedBrush"] = Application.Current.Resources["TabItemForeground_Selected_Light"];
                Application.Current.Resources["TabItemBackground_MouseOverBrush"] = Application.Current.Resources["TabItemBackground_MouseOver_Light"];
                Application.Current.Resources["TabControlContentBackgroundBrush"] = Application.Current.Resources["TabControlContentBackground_Light"];
            }
        }
    }
}
