using System.Windows; // Required for Application.Current
using Windows.UI.ViewManagement; // Required for UISettings

namespace Reader.Business
{
    public static class ThemeManager
    {
        private static bool _isDarkModeInitialized = false;
        private static bool _cachedIsDarkMode;

        public static bool GetCurrentSystemIsDarkMode()
        {
            if (!_isDarkModeInitialized)
            {
                try
                {
                    var uiSettings = new UISettings();
                    var backgroundColor = uiSettings.GetColorValue(UIColorType.Background);
                    _cachedIsDarkMode = backgroundColor.R < 128 && backgroundColor.G < 128 && backgroundColor.B < 128;
                }
                catch (System.Exception)
                {
                    // Fallback for environments where UISettings might not be available (e.g. older Windows)
                    _cachedIsDarkMode = false;
                }
                _isDarkModeInitialized = true;
            }
            return _cachedIsDarkMode;
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
