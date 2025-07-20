using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using SoundWeaver;
using SoundWeaver.Models;
using Utils;

namespace Assistant
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        public static Serilog.Core.LoggingLevelSwitch? GlobalLogLevelSwitch { get; private set; } // Made public static
        private ILoggerService _logger; // Store logger instance

        private ResourceDictionary? _currentTextBlockThemeDictionary = null;

        public App()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();
            _logger = ServiceProvider.GetRequiredService<ILoggerService>();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Configure Serilog
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logDirectory = Path.Combine(appDataPath, "Assistant");
            Directory.CreateDirectory(logDirectory);
            string logFilePath = Path.Combine(logDirectory, "logs.txt");

            GlobalLogLevelSwitch = new Serilog.Core.LoggingLevelSwitch();
            GlobalLogLevelSwitch.MinimumLevel = LogEventLevel.Verbose;

            Serilog.Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(GlobalLogLevelSwitch) // Use the static property
                .Enrich.FromLogContext()
                .WriteTo.Debug(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(logFilePath,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: 7,
                              fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB
                              rollOnFileSizeLimit: true)
                .CreateLogger();

            services.AddLogging(builder =>
            {
                builder.AddSerilog(dispose: true);
            });

            services.AddSingleton<ILoggerService>(sp => new LogService(GlobalLogLevelSwitch ?? new Serilog.Core.LoggingLevelSwitch()));

            services.AddSingleton<MainFrame>();
            services.AddSingleton<SoundWeaverControlViewModel>();
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
                    _logger?.LogError(ex_reg, "Error reading system theme from registry. Defaulting to dark theme.");
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
                    _logger?.LogError(ex, "Error loading TextBlock theme dictionary for URI {DictionaryUriString}", dictionaryUriString);
                }
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _logger.LogInfo("Application starting up.");

            ThemeMode currentMode = Application.Current.ThemeMode;
            LoadThemeSpecificTextBlockStyles(currentMode);
            _logger.LogInfo("Application startup complete.");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            var soundWeaverVm = ServiceProvider.GetService<SoundWeaverControlViewModel>();
            soundWeaverVm?.DisconnectBotCommand.Execute(null);
            soundWeaverVm?.Dispose();
        }

    }
}
