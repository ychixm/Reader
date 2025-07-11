using System.Windows.Controls;
using SoundWeaver.Models; // For SoundWeaverSettings
using Utils; // For ISubApplication, IOptionsViewModel, AppSettingsService, ILoggerService

namespace SoundWeaver
{
    public class SoundWeaverSubApplication : ISubApplication
    {
        private readonly ILoggerService _logger;
        private SoundWeaverControl? _mainView;
        private SoundWeaverOptionsViewModel? _optionsViewModel;
        private SoundWeaverSettings _settings;

        public string Name => "SoundWeaver";

        public SoundWeaverSubApplication(ILoggerService logger)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            // Load initial settings for the SoundWeaver module
            _settings = AppSettingsService.LoadModuleSettings<SoundWeaverSettings>("SoundWeaver", () => new SoundWeaverSettings());
            _logger.LogInfo("SoundWeaverSubApplication initialized.");
        }

        public UserControl GetMainView()
        {
            if (_mainView == null)
            {
                _mainView = new SoundWeaverControl();
                // Apply any initial settings from _settings to _mainView if needed
                // For example, if MainViewModel took settings in its constructor or had properties:
                // var viewModel = _mainView.DataContext as UI.MainViewModel;
                // if (viewModel != null && _settings != null)
                // {
                //     viewModel.DiscordToken = _settings.DefaultBotToken ?? ""; // Example
                // }
            }
            return _mainView;
        }

        public IOptionsViewModel GetOptionsViewModel()
        {
            if (_optionsViewModel == null)
            {
                _optionsViewModel = new SoundWeaverOptionsViewModel();
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
                _logger.LogInfo("SoundWeaverSubApplication: ApplyOptions called. Settings reloaded.");
                // Potentially, MainViewModel could also listen to some kind of settings changed event.
            }
        }
    }
}
