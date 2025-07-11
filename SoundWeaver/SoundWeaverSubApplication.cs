using Microsoft.Extensions.Logging;
using SoundWeaver.Models; // For SoundWeaverSettings
using SoundWeaver.UI; // For SoundWeaverControlViewModel
using System.Windows.Controls;
using Utils; // For ISubApplication, IOptionsViewModel, AppSettingsService

namespace SoundWeaver
{
    public class SoundWeaverSubApplication : ISubApplication
    {
        private SoundWeaverControl? _mainView;
        private SoundWeaverOptionsViewModel? _optionsViewModel;
        private SoundWeaverSettings _settings;
        private readonly ILogger<SoundWeaverSubApplication> _logger;
        private readonly ILoggerFactory _loggerFactory; // To pass to other components if they are not DI-managed

        public string Name => "SoundWeaver";

        public SoundWeaverSubApplication(ILogger<SoundWeaverSubApplication> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

            // Load initial settings for the SoundWeaver module
            _logger.LogInformation("Initializing SoundWeaverSubApplication and loading settings.");
            _settings = AppSettingsService.LoadModuleSettings<SoundWeaverSettings>("SoundWeaver", () => new SoundWeaverSettings());
        }

        public UserControl GetMainView()
        {
            if (_mainView == null)
            {
                _logger.LogDebug("Creating MainView for SoundWeaver.");
                // To correctly use DI for the ViewModel, SoundWeaverSubApplication needs access to IServiceProvider.
                // Assuming App.ServiceProvider is accessible and set up.
                // If App.ServiceProvider is not available, ILoggerFactory could be used, but it's less ideal for resolving complex objects.
                var viewModel = Assistant.App.ServiceProvider.GetRequiredService<SoundWeaverControlViewModel>();
                _mainView = new SoundWeaverControl { DataContext = viewModel };
                _logger.LogInformation("SoundWeaver MainView created and ViewModel attached.");
            }
            return _mainView;
        }

        public IOptionsViewModel GetOptionsViewModel()
        {
            if (_optionsViewModel == null)
            {
                // Assuming SoundWeaverOptionsViewModel also needs to be DI-managed if it has logger dependencies.
                // For now, it's newed up. If it needs ILogger, this will need to change.
                // Let's check its constructor. It doesn't seem to have one taking ILogger from previous steps.
                // If it's simple and doesn't log, this is fine.
                // If it does log, it should also be resolved via DI or passed logger factory.
                // For now, keeping as is, as it wasn't part of the logger injection list.
                _optionsViewModel = new SoundWeaverOptionsViewModel();
                _logger.LogDebug("Created SoundWeaverOptionsViewModel.");
                // _optionsViewModel.LoadSettings(); // ViewModel now loads settings in its constructor
            }
            return _optionsViewModel;
        }

        public void ApplyOptions()
        {
            // This method is called when global "Apply" is clicked in Assistant options.
            // 1. Ensure options are saved from the ViewModel
            _optionsViewModel?.Apply(); // Saves settings via AppSettingsService

            // 2. Reload settings for this module
            _settings = AppSettingsService.LoadModuleSettings<SoundWeaverSettings>("SoundWeaver", () => new SoundWeaverSettings());

            // 3. Apply reloaded settings to the main view if it's active and needs updates
            if (_mainView != null)
            {
                // Example: If MainViewModel needs to react to settings changes
                // var viewModel = _mainView.DataContext as UI.MainViewModel;
                // if (viewModel != null && _settings != null)
                // {
                //    // viewModel.UpdateDiscordToken(_settings.DefaultBotToken); // Hypothetical method
                //    // Or, if settings directly affect UI elements not bound through MainViewModel's core logic.
                // }
                _logger.LogInformation("ApplyOptions called. Settings reloaded. MainView is active.");
                // Potentially, MainViewModel could also listen to some kind of settings changed event.
            }
            else
            {
                _logger.LogInformation("ApplyOptions called. Settings reloaded. MainView is not currently active/initialized.");
            }
        }
    }
}
