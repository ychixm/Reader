using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Utils; // For IOptionsViewModel, AppSettingsService, ILoggerService
using Reader.Models; // For ReaderSettings, NavigationMethod
using Utils.Models; // For TabOverflowMode - assuming this is a shared enum
using System; // For Enum.TryParse

namespace Reader.ViewModels
{
    public class ReaderOptionsViewModel : IOptionsViewModel
    {
        private readonly ILoggerService _logger;
        private bool _enableKeyboardNavigation;
        private bool _enableGridClickNavigation;
        private bool _enableVisibleButtonsNavigation;
        private TabOverflowMode _selectedTabOverflowMode;
        private string? _defaultPath;
        // DefaultPath is part of ReaderSettings but not directly bound/edited in this VM's view in this iteration.
        // It will be loaded and saved as part of the ReaderSettings object.

        public string Title => "Reader Options";

        public bool EnableKeyboardNavigation
        {
            get => _enableKeyboardNavigation;
            set
            {
                if (_enableKeyboardNavigation != value)
                {
                    _enableKeyboardNavigation = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? DefaultPath
        {
            get => _defaultPath;
            set
            {
                if (_defaultPath != value)
                {
                    _defaultPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EnableGridClickNavigation
        {
            get => _enableGridClickNavigation;
            set
            {
                if (_enableGridClickNavigation != value)
                {
                    _enableGridClickNavigation = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EnableVisibleButtonsNavigation
        {
            get => _enableVisibleButtonsNavigation;
            set
            {
                if (_enableVisibleButtonsNavigation != value)
                {
                    _enableVisibleButtonsNavigation = value;
                    OnPropertyChanged();
                }
            }
        }

        public TabOverflowMode SelectedTabOverflowMode
        {
            get => _selectedTabOverflowMode;
            set
            {
                if (_selectedTabOverflowMode != value)
                {
                    _selectedTabOverflowMode = value;
                    OnPropertyChanged();
                }
            }
        }

        // Constructor to accept ILoggerService
        public ReaderOptionsViewModel(ILoggerService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            LoadSettings(); // Load settings upon construction
            _logger.LogInfo("ReaderOptionsViewModel initialized.");
        }

        public UserControl GetView()
        {
            // Pass the logger to the View
            return new UserControls.ReaderOptionsView(_logger) { DataContext = this };
        }

        public void LoadSettings()
        {
            try
            {
                // Use the overload of LoadModuleSettings that provides a default factory
                var settings = AppSettingsService.LoadModuleSettings<ReaderSettings>("ReaderModule", () => new ReaderSettings());

                EnableKeyboardNavigation = settings.EnabledNavigationMethods.HasFlag(Reader.Models.NavigationMethod.KeyboardArrows);
                EnableGridClickNavigation = settings.EnabledNavigationMethods.HasFlag(Reader.Models.NavigationMethod.GridClick);
                EnableVisibleButtonsNavigation = settings.EnabledNavigationMethods.HasFlag(Reader.Models.NavigationMethod.VisibleButtons);

                if (Enum.TryParse<TabOverflowMode>(settings.DefaultTabOverflowMode, out var mode))
                {
                    SelectedTabOverflowMode = mode;
                }
                else
                {
                    // If DefaultTabOverflowMode is null/empty or invalid, use a default from ReaderSettings or a hardcoded one
                    SelectedTabOverflowMode = TabOverflowMode.Scrollbar; // Fallback
                }
                DefaultPath = settings.DefaultPath;
            }
            catch (Exception ex_load_settings)
            {
                _logger.LogError(ex_load_settings, "Error loading reader options settings.");
            }
        }

        public void Apply()
        {
            try
            {
                _logger.LogInfo("Applying Reader options settings.");
                // It's good practice to load existing settings for the module first if other properties
                // (not managed by this VM, like DefaultPath) should be preserved.
                var settingsToSave = AppSettingsService.LoadModuleSettings<ReaderSettings>("ReaderModule", () => new ReaderSettings());
                // Or, if this VM is authoritative for ALL ReaderSettings: var settingsToSave = new ReaderSettings();


                Reader.Models.NavigationMethod updatedMethods = Reader.Models.NavigationMethod.None;
                if (EnableKeyboardNavigation) updatedMethods |= Reader.Models.NavigationMethod.KeyboardArrows;
                if (EnableGridClickNavigation) updatedMethods |= Reader.Models.NavigationMethod.GridClick;
                if (EnableVisibleButtonsNavigation) updatedMethods |= Reader.Models.NavigationMethod.VisibleButtons;

                settingsToSave.EnabledNavigationMethods = updatedMethods;
                settingsToSave.DefaultTabOverflowMode = SelectedTabOverflowMode.ToString();
                settingsToSave.DefaultPath = DefaultPath;
                // settingsToSave.DefaultPath would be preserved if loaded as above. If this VM controlled it, it'd be set here.

                AppSettingsService.SaveModuleSettings("Reader", settingsToSave);
                _logger.LogInfo("Reader options settings applied and saved.");
            }
            catch (Exception ex_apply_settings)
            {
                _logger.LogError(ex_apply_settings, "Error applying reader options settings.");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
