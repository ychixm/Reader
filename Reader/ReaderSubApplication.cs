using System.Windows.Controls;
using Utils; // For ISubApplication, IOptionsViewModel
using System.Windows.Controls;
using Utils; // For ISubApplication, AppSettingsService
using Reader.UserControls;
using Reader.ViewModels;
using Reader.Models; // For ReaderSettings, NavigationMethod
using Utils.Models; // For TabOverflowMode - assuming this is a shared enum
using System; // For Enum.TryParse

namespace Reader
{
    public class ReaderSubApplication : ISubApplication
    {
        private ReaderUserControl? _mainView;
        private ReaderOptionsViewModel? _optionsViewModel;
        private ReaderSettings _readerSettings; // Changed type back to Reader.Models.ReaderSettings

        public string Name => "Reader";

        public ReaderSubApplication()
        {
            // Load initial settings for the Reader module using the generic service
            _readerSettings = AppSettingsService.LoadModuleSettings<ReaderSettings>("ReaderModule", () => new ReaderSettings());
        }

        public UserControl GetMainView()
        {
            if (_mainView == null)
            {
                _mainView = new ReaderUserControl();
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
