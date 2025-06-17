using System.Windows.Controls;
using Utils; // For ISubApplication, IOptionsViewModel
using Reader.UserControls; // For ReaderUserControl
using Reader.ViewModels; // For ReaderOptionsViewModel
using Reader.Models; // For AppSettings, NavigationMethod
using Reader.Business; // For AppSettingsService
using Utils.Models; // For TabOverflowMode
using System; // Required for Enum.TryParse

namespace Reader
{
    public class ReaderSubApplication : ISubApplication
    {
        private ReaderUserControl? _mainView;
        private ReaderOptionsViewModel? _optionsViewModel;
        private AppSettings _appSettings;

        public string Name => "Reader";

        public ReaderSubApplication()
        {
            // Load initial settings for the application's startup
            _appSettings = AppSettingsService.LoadAppSettings();
        }

        public UserControl GetMainView()
        {
            if (_mainView == null)
            {
                _mainView = new ReaderUserControl();
                // Apply initial settings from _appSettings loaded at construction
                _mainView.ApplyNavigationSettings(_appSettings.EnabledNavigationMethods);
                if (Enum.TryParse<TabOverflowMode>(_appSettings.DefaultTabOverflowMode, out var mode))
                {
                    _mainView.ApplyTabOverflowMode(mode);
                }
                else
                {
                    // Fallback to a default if parsing fails or setting is empty/invalid
                    _mainView.ApplyTabOverflowMode(TabOverflowMode.Scrollbar);
                }
            }
            return _mainView;
        }

        public IOptionsViewModel GetOptionsViewModel()
        {
            if (_optionsViewModel == null)
            {
                _optionsViewModel = new ReaderOptionsViewModel();
                _optionsViewModel.LoadSettings(); // ViewModel now loads its own settings
            }
            return _optionsViewModel;
        }

        public void ApplyOptions()
        {
            // At this point, IOptionsViewModel.Apply() has already been called by the Assistant,
            // so settings should be persisted.
            // We need to ensure the main view reflects these persisted settings.

            if (_mainView == null)
            {
                // Or throw, or log. MainView should exist if options are being applied.
                return;
            }

            // Reload settings to ensure we're applying what was just saved
            var currentSettings = AppSettingsService.LoadAppSettings();

            // Apply loaded settings to the MainView
            _mainView.ApplyNavigationSettings(currentSettings.EnabledNavigationMethods);

            if (Enum.TryParse<TabOverflowMode>(currentSettings.DefaultTabOverflowMode, out var mode))
            {
                _mainView.ApplyTabOverflowMode(mode);
            }
            else
            {
                // Fallback for safety, though settings should be valid if saved by ViewModel
                _mainView.ApplyTabOverflowMode(TabOverflowMode.Scrollbar);
            }

            // Update the local _appSettings instance as well to keep it in sync
            _appSettings = currentSettings;
        }
    }
}
