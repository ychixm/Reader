using System.Windows.Controls;
using Utils; // For ISubApplication, IOptionsViewModel
using Reader.UserControls;
using Reader.ViewModels;
// Remove: using Reader.Models;
// Remove: using Reader.Business;
using Utils.Models; // For ReaderSpecificSettings, NavigationMethod, TabOverflowMode, AllAppSettings
using System;

namespace Reader
{
    public class ReaderSubApplication : ISubApplication
    {
        private ReaderUserControl? _mainView;
        private ReaderOptionsViewModel? _optionsViewModel;
        private ReaderSpecificSettings _readerSettings; // Changed type

        public string Name => "Reader";

        public ReaderSubApplication()
        {
            // Load initial settings for the Reader module from the centralized service
            _readerSettings = AppSettingsService.LoadApplicationSettings().Reader;
        }

        public UserControl GetMainView()
        {
            if (_mainView == null)
            {
                _mainView = new ReaderUserControl();
                // Apply initial settings from the loaded _readerSettings
                _mainView.ApplyNavigationSettings(_readerSettings.EnabledNavigationMethods);
                if (Enum.TryParse<TabOverflowMode>(_readerSettings.DefaultTabOverflowMode, out var mode))
                {
                    _mainView.ApplyTabOverflowMode(mode);
                }
                else
                {
                    _mainView.ApplyTabOverflowMode(TabOverflowMode.Scrollbar); // Default fallback
                }
            }
            return _mainView;
        }

        public IOptionsViewModel GetOptionsViewModel()
        {
            if (_optionsViewModel == null)
            {
                _optionsViewModel = new ReaderOptionsViewModel();
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
            // At this point, IOptionsViewModel.Apply() has already been called by Assistant.OptionsUserControl,
            // so settings should be persisted in the centralized application_settings.json.
            // We need to ensure the main view reflects these persisted settings.

            if (_mainView == null)
            {
                return;
            }

            // Reload all application settings and get the Reader-specific part
            var currentAllSettings = AppSettingsService.LoadApplicationSettings();
            var currentReaderSettings = currentAllSettings.Reader;

            _mainView.ApplyNavigationSettings(currentReaderSettings.EnabledNavigationMethods);

            if (Enum.TryParse<TabOverflowMode>(currentReaderSettings.DefaultTabOverflowMode, out var mode))
            {
                _mainView.ApplyTabOverflowMode(mode);
            }
            else
            {
                _mainView.ApplyTabOverflowMode(TabOverflowMode.Scrollbar);
            }

            // Update the local _readerSettings instance to keep it in sync
            _readerSettings = currentReaderSettings;
        }
    }
}
