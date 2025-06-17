using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Utils; // For IOptionsViewModel and new AppSettingsService
using Utils.Models; // For TabOverflowMode and new settings models (ReaderSpecificSettings, NavigationMethod)
using System; // For Enum.TryParse

// Remove: using Reader.Models;
// Remove: using Reader.Business;

namespace Reader.ViewModels
{
    public class ReaderOptionsViewModel : IOptionsViewModel
    {
        private bool _enableKeyboardNavigation;
        private bool _enableGridClickNavigation;
        private bool _enableVisibleButtonsNavigation;
        private TabOverflowMode _selectedTabOverflowMode;
        // Add DefaultPath if it needs to be exposed by the ViewModel, otherwise it's just saved/loaded.
        // For this refactor, we'll assume it's not directly bound in ReaderOptionsView.

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
            // We need to ensure ReaderOptionsView XAML/code-behind is updated if its DataContext
            // or bindings depend on specific types from Reader.Models that are now in Utils.Models.
            // For now, assume GetView() itself doesn't change, but its view might need to adapt later.
            return new UserControls.ReaderOptionsView { DataContext = this };
        }

        public void LoadSettings()
        {
            var allSettings = AppSettingsService.LoadApplicationSettings(); // New Utils.AppSettingsService
            var readerSettings = allSettings.Reader;

            EnableKeyboardNavigation = readerSettings.EnabledNavigationMethods.HasFlag(Utils.Models.NavigationMethod.KeyboardArrows);
            EnableGridClickNavigation = readerSettings.EnabledNavigationMethods.HasFlag(Utils.Models.NavigationMethod.GridClick);
            EnableVisibleButtonsNavigation = readerSettings.EnabledNavigationMethods.HasFlag(Utils.Models.NavigationMethod.VisibleButtons);

            if (Enum.TryParse<TabOverflowMode>(readerSettings.DefaultTabOverflowMode, out var mode))
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
            var allSettings = AppSettingsService.LoadApplicationSettings(); // New Utils.AppSettingsService

            Utils.Models.NavigationMethod updatedMethods = Utils.Models.NavigationMethod.None;
            if (EnableKeyboardNavigation) updatedMethods |= Utils.Models.NavigationMethod.KeyboardArrows;
            if (EnableGridClickNavigation) updatedMethods |= Utils.Models.NavigationMethod.GridClick;
            if (EnableVisibleButtonsNavigation) updatedMethods |= Utils.Models.NavigationMethod.VisibleButtons;

            allSettings.Reader.EnabledNavigationMethods = updatedMethods;
            allSettings.Reader.DefaultTabOverflowMode = SelectedTabOverflowMode.ToString();
            // DefaultPath is not managed by this ViewModel's UI, but it will be preserved if loaded
            // and saved as part of allSettings.Reader. If it were editable, it would be here too.

            AppSettingsService.SaveApplicationSettings(allSettings); // New Utils.AppSettingsService
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
