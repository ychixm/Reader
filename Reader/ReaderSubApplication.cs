using System.Windows.Controls;
using Reader.UserControls;
using Reader.ViewModels;
using Reader.Models; 
using Utils.Models;
using Utils; // ILoggerService is here

namespace Reader
{
    public class ReaderSubApplication : ISubApplication
    {
        private readonly ILoggerService _logger;
        private ReaderUserControl? _mainView;
        private ReaderOptionsViewModel? _optionsViewModel;
        private ReaderSettings _readerSettings; // Changed type back to Reader.Models.ReaderSettings

        public string Name => "Reader";

        public ReaderSubApplication(ILoggerService logger)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            // Load initial settings for the Reader module using the generic service
            _readerSettings = AppSettingsService.LoadModuleSettings<ReaderSettings>("ReaderModule", () => new ReaderSettings());
            _logger.LogInfo("ReaderSubApplication initialized.");
        }

        public UserControl GetMainView()
        {
            if (_mainView == null)
            {
                _logger.LogInfo("Creating ReaderUserControl main view.");
                _mainView = new ReaderUserControl(_logger); // Pass logger
                _mainView.ApplyNavigationSettings(_readerSettings.EnabledNavigationMethods);
                if (Enum.TryParse<TabOverflowMode>(_readerSettings.DefaultTabOverflowMode, out var mode))
                {
                    _mainView.ApplyTabOverflowMode(mode);
                }
                else
                {
                    _mainView.ApplyTabOverflowMode(TabOverflowMode.Scrollbar);
                }
            }
            return _mainView;
        }

        public IOptionsViewModel GetOptionsViewModel()
        {
            if (_optionsViewModel == null)
            {
                _logger.LogInfo("Creating ReaderOptionsViewModel.");
                _optionsViewModel = new ReaderOptionsViewModel(_logger); // Pass logger
                // _optionsViewModel.LoadSettings() is called by OptionsUserControl or by itself if needed.
                // The plan for OptionsUserControl was to call LoadSettings, which is good.
                // If called here too, it's redundant but harmless if LoadSettings is idempotent.
                // Let's stick to the previous plan where OptionsUserControl calls it.
                // If OptionsUserControl doesn't, then it *must* be called here:
                // _optionsViewModel.LoadSettings();
            }
            return _optionsViewModel;
        }

        public void ApplyOptions()
        {
            if (_mainView == null)
            {
                return;
            }

            // Reload Reader-specific settings from the centralized service
            var currentReaderSettings = AppSettingsService.LoadModuleSettings<ReaderSettings>("ReaderModule", () => new ReaderSettings());

            _mainView.ApplyNavigationSettings(currentReaderSettings.EnabledNavigationMethods);

            if (Enum.TryParse<TabOverflowMode>(currentReaderSettings.DefaultTabOverflowMode, out var mode))
            {
                _mainView.ApplyTabOverflowMode(mode);
            }
            else
            {
                _mainView.ApplyTabOverflowMode(TabOverflowMode.Scrollbar);
            }

            _readerSettings = currentReaderSettings; // Update local cache
        }
    }
}
