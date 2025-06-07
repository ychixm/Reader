using System.Windows; // Required for Application.Current
// using Microsoft.Win32; // No longer needed
// using System; // No longer needed for Exception
using Reader.Utils; // For WindowsThemeHelpers

namespace Reader.Business
{
    public static class ThemeManager
    {
        // private static bool _isDarkModeInitialized = false; // Removed CS0414
        // private static bool _cachedIsDarkMode; // Removed CS0169

        // GetCurrentSystemIsDarkMode method removed.

        public static void ApplyTheme()
        {
            bool isDarkMode = WindowsThemeHelpers.GetCurrentSystemIsDarkMode();

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
