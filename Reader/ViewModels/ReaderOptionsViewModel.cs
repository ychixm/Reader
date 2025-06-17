using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Utils; // For IOptionsViewModel
using Utils.Models; // For TabOverflowMode

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

        public void Apply()
        {
            // This method will be used to apply the settings.
            // For now, it can be empty. We might save settings here
            // or signal the ReaderSubApplication.
            // Actual saving to AppSettingsService will likely be coordinated
            // by ReaderSubApplication.ApplyOptions()
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
