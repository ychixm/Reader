using System;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoundWeaver; // For AddSoundWeaverServices()
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
        public static IServiceProvider ServiceProvider { get; private set; }

        private ResourceDictionary? _currentTextBlockThemeDictionary = null;

        public App()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole(); 
                builder.SetMinimumLevel(LogLevel.Information);
            });

            services.AddSingleton<MainFrame>();
        }

        private void LoadThemeSpecificTextBlockStyles(ThemeMode themeMode)
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

            if (themeMode == System.Windows.ThemeMode.System)
            {
                try
                {
                    var currentWindowsTheme = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", "1");
                    if (currentWindowsTheme != null && currentWindowsTheme.ToString() == "0")
                    {
                        isSystemDark = true;
                    }
                }
                catch (Exception ex_reg)
                {
                    Utils.LogService.LogError(ex_reg, "Error reading system theme from registry. Defaulting to dark theme.");
                    isSystemDark = true;
                }
            }

            if (themeMode == System.Windows.ThemeMode.Dark || (themeMode == System.Windows.ThemeMode.System && isSystemDark))
            {
                dictionaryUriString = "pack://application:,,,/Assistant;component/Styles/TextBlockDark.xaml";
            }
            else
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
                    Utils.LogService.LogError(ex, "Error loading TextBlock theme dictionary for URI {DictionaryUriString}", dictionaryUriString);
                }
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = ServiceProvider.GetRequiredService<MainFrame>();
            mainWindow.Show();

            ThemeMode currentMode = Application.Current.ThemeMode;
            LoadThemeSpecificTextBlockStyles(currentMode);
        }
    }

}
