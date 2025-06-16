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
            // Load settings when the sub-application is initialized.
            // This ensures that the OptionsViewModel is initialized with current settings.
            _appSettings = AppSettingsService.LoadAppSettings();
        }

        public UserControl GetMainView()
        {
            // Lazy initialization of the main view
            if (_mainView == null)
            {
                _mainView = new ReaderUserControl();
                // Apply initial settings directly from _appSettings
                _mainView.ApplyNavigationSettings(_appSettings.EnabledNavigationMethods);
                if (Enum.TryParse<TabOverflowMode>(_appSettings.DefaultTabOverflowMode, out var mode))
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
            // Lazy initialization of the options view model
            if (_optionsViewModel == null)
            {
                _optionsViewModel = new ReaderOptionsViewModel
                {
                    // Initialize ViewModel properties from loaded AppSettings
                    EnableKeyboardNavigation = _appSettings.EnabledNavigationMethods.HasFlag(NavigationMethod.KeyboardArrows),
                    EnableGridClickNavigation = _appSettings.EnabledNavigationMethods.HasFlag(NavigationMethod.GridClick),
                    EnableVisibleButtonsNavigation = _appSettings.EnabledNavigationMethods.HasFlag(NavigationMethod.VisibleButtons)
                };

                if (Enum.TryParse<TabOverflowMode>(_appSettings.DefaultTabOverflowMode, out var mode))
                {
                    _optionsViewModel.SelectedTabOverflowMode = mode;
                }
                else
                {
                    // Default to Scrollbar if parsing fails or setting is empty
                    _optionsViewModel.SelectedTabOverflowMode = TabOverflowMode.Scrollbar;
                }
            }
            return _optionsViewModel;
        }

        public void ApplyOptions()
        {
            if (_optionsViewModel == null || _mainView == null)
            {
                // Or throw an exception, or log an error
                // This shouldn't happen if GetOptionsViewModel and GetMainView were called.
                return;
            }

            // Update AppSettings from ViewModel
            NavigationMethod updatedMethods = NavigationMethod.None;
            if (_optionsViewModel.EnableKeyboardNavigation) updatedMethods |= NavigationMethod.KeyboardArrows;
            if (_optionsViewModel.EnableGridClickNavigation) updatedMethods |= NavigationMethod.GridClick;
            if (_optionsViewModel.EnableVisibleButtonsNavigation) updatedMethods |= NavigationMethod.VisibleButtons;
            _appSettings.EnabledNavigationMethods = updatedMethods;

            _appSettings.DefaultTabOverflowMode = _optionsViewModel.SelectedTabOverflowMode.ToString();

            // Save the updated settings
            AppSettingsService.SaveAppSettings(_appSettings);

            // Notify the MainView (ReaderUserControl) to apply these changes.
            // This requires ReaderUserControl to have public methods/properties
            // to accept these settings. This part will be refined in the refactoring step (Step 6).

            // Example of how ReaderUserControl might be updated:
            // _mainView.UpdateNavigationSettings(updatedMethods);
            // _mainView.UpdateTabOverflowMode(_optionsViewModel.SelectedTabOverflowMode);

            // For now, we assume ReaderUserControl will pick up changes from AppSettingsService
            // upon next load, or we add explicit update methods in Step 6.
            // If ReaderUserControl's constructor re-reads settings, or if its relevant parts
            // re-evaluate settings, changes might apply "automatically".
            // However, direct method calls are cleaner.

            // Let's assume ReaderUserControl might need to re-initialize or update its components
            // based on the new settings.
            // For instance, if ReaderUserControl's TabOverflowControl or navigation options
            // are bound to properties that _appSettings directly influences, or if it has methods
            // to refresh these.

            // Notify the MainView (ReaderUserControl) to apply these changes.
            if (_mainView is ReaderUserControl ruc && _optionsViewModel != null) // Ensure optionsViewModel is not null
            {
                ruc.ApplyNavigationSettings(_appSettings.EnabledNavigationMethods);
                ruc.ApplyTabOverflowMode(_optionsViewModel.SelectedTabOverflowMode);
            }
        }
    }
}
