using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Utils; // For IOptionsViewModel
using Utils.Models; // For TabOverflowMode
using Reader.Models; // Added for AppSettings, NavigationMethod
using Reader.Business; // Added for AppSettingsService
using System; // Added for Enum.TryParse

namespace Reader.ViewModels
{
    public class ReaderOptionsViewModel : IOptionsViewModel
    {
        private bool _enableKeyboardNavigation;
        private bool _enableGridClickNavigation;
        private bool _enableVisibleButtonsNavigation;
        private TabOverflowMode _selectedTabOverflowMode;

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

        public UserControl GetView()
        {
            // We will create ReaderOptionsView in the next step.
            // For now, this will point to it.
            return new UserControls.ReaderOptionsView { DataContext = this };
        }

        public void LoadSettings()
        {
            var appSettings = AppSettingsService.LoadAppSettings();
            EnableKeyboardNavigation = appSettings.EnabledNavigationMethods.HasFlag(NavigationMethod.KeyboardArrows);
            EnableGridClickNavigation = appSettings.EnabledNavigationMethods.HasFlag(NavigationMethod.GridClick);
            EnableVisibleButtonsNavigation = appSettings.EnabledNavigationMethods.HasFlag(NavigationMethod.VisibleButtons);

            if (Enum.TryParse<TabOverflowMode>(appSettings.DefaultTabOverflowMode, out var mode))
            {
                SelectedTabOverflowMode = mode;
            }
            else
            {
                SelectedTabOverflowMode = TabOverflowMode.Scrollbar; // Default fallback
            }
        }

        public void Apply()
        {
            var appSettings = AppSettingsService.LoadAppSettings(); // Load current settings to update them

            NavigationMethod updatedMethods = NavigationMethod.None;
            if (EnableKeyboardNavigation) updatedMethods |= NavigationMethod.KeyboardArrows;
            if (EnableGridClickNavigation) updatedMethods |= NavigationMethod.GridClick;
            if (EnableVisibleButtonsNavigation) updatedMethods |= NavigationMethod.VisibleButtons;
            appSettings.EnabledNavigationMethods = updatedMethods;

            appSettings.DefaultTabOverflowMode = SelectedTabOverflowMode.ToString();

            AppSettingsService.SaveAppSettings(appSettings);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
