using System; // For Uri
using System.Configuration;
using System.Data;
using System.Windows;
using System.Linq; // For MergedDictionaries manipulation
// For .NET 9 ThemeMode enum:
// No specific using needed for ThemeMode enum if it's directly accessible via Application.Current.ThemeMode
// or if System.Windows.Controls.ThemeMode is used.

namespace Assistant
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ResourceDictionary? _currentTextBlockThemeDictionary = null;

        private void LoadThemeSpecificTextBlockStyles(System.Windows.Controls.ThemeMode themeMode)
        {
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            // Remove previously loaded TextBlock theme dictionary
            if (_currentTextBlockThemeDictionary != null)
            {
                mergedDictionaries.Remove(_currentTextBlockThemeDictionary);
                _currentTextBlockThemeDictionary = null;
            }

            string? dictionaryUriString = null;
            bool isSystemDark = false;

            // For System mode, we need to detect if OS is dark.
            // This is a simplified check; a full implementation might use Windows API calls or a helper.
            // For now, let's assume if not explicitly Light, it might be Dark or a dark System.
            // A more robust check for system theme would be needed for ThemeMode.System.
            // However, ThemeMode itself should handle this internally when applying the base Fluent theme.
            // Our TextBlock style should just align with what ThemeMode has decided.

            // A simple way to check effective theme if ThemeMode is System:
            // We can check a known brush color after the base theme is applied by ThemeMode.
            // For example, SystemColors.WindowBrush.ToString() could be compared.
            // But for loading OUR dictionaries, we rely on the ThemeMode value directly.

            if (themeMode == System.Windows.Controls.ThemeMode.System)
            {
                // For ThemeMode.System, we need to determine if the system is currently in dark mode.
                // A common way is to check a known system color.
                // For example, if SystemColors.WindowColor is dark.
                // This is an approximation. .NET 9 might offer better ways to get the *actual* applied sub-theme of System.
                // For this example, let's default System to Light for our TextBlock override,
                // unless we have a robust way to check if System resolved to Dark.
                // A safer bet: if user wants specific TextBlock overrides, they should use ThemeMode.Light/Dark explicitly for now.
                // Or, we assume ThemeMode.System will make SystemColors.ControlTextBrushKey correct.
                // Let's keep this simple: if ThemeMode is Dark, use Dark. Otherwise, use Light.
                // This means ThemeMode.System will use TextBlockLight.xaml unless we enhance detection.
                // User specifically wants their "white text" for "dark theme".
                 if (Microsoft.Win32.SystemEvents.UserPreferenceChanging != null) // trick to get Microsoft.Win32 assembly loaded for SystemParameters
                {
                     try {
                        var currentWindowsTheme = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", "1");
                        if (currentWindowsTheme != null && currentWindowsTheme.ToString() == "0") {
                            isSystemDark = true;
                        }
                     } catch {} // Registry access might fail
                }
            }

            if (themeMode == System.Windows.Controls.ThemeMode.Dark || (themeMode == System.Windows.Controls.ThemeMode.System && isSystemDark))
            {
                dictionaryUriString = "pack://application:,,,/Assistant;component/Styles/TextBlockDark.xaml";
            }
            else // Light or System (defaulting to Light for TextBlock override if system detection is basic)
            {
                dictionaryUriString = "pack://application:,,,/Assistant;component/Styles/TextBlockLight.xaml";
            }

            if (dictionaryUriString != null)
            {
                try
                {
                    var themeDictionary = new ResourceDictionary { Source = new Uri(dictionaryUriString, UriKind.Absolute) };
                    mergedDictionaries.Add(themeDictionary);
                    _currentTextBlockThemeDictionary = themeDictionary;
                }
                catch (Exception ex)
                {
                    // Log or handle exception (e.g., file not found)
                    System.Diagnostics.Debug.WriteLine($"Error loading TextBlock theme dictionary: {ex.Message}");
                }
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Determine the initial ThemeMode.
            // The ThemeMode property on Application is set from XAML before OnStartup if defined there.
            System.Windows.Controls.ThemeMode currentMode = Application.Current.ThemeMode;
            LoadThemeSpecificTextBlockStyles(currentMode);

            // Optional: Add handler for theme changes if you build a mechanism for runtime ThemeMode changes.
            // For .NET 9, if ThemeMode is changed programmatically, you'd call LoadThemeSpecificTextBlockStyles again.
            // If ThemeMode="System", you might listen to SystemEvents.UserPreferenceChanged for OS theme changes.
            // For simplicity, this example only applies it at startup.
        }
    }

}
