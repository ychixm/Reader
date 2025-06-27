using Utils; // Required for IOptionsViewModel, AppSettingsService
using SoundWeaver.Models; // Required for SoundWeaverSettings
using System.ComponentModel; // Required for INotifyPropertyChanged
using System.Runtime.CompilerServices; // Required for CallerMemberName
using System.Windows.Controls; // Required for UserControl

namespace SoundWeaver
{
    public class SoundWeaverOptionsViewModel : IOptionsViewModel
    {
        public string Title => "SoundWeaver Options";

        private bool _sampleOption;
        public bool SampleOption
        {
            get => _sampleOption;
            set
            {
                if (_sampleOption != value)
                {
                    _sampleOption = value;
                    OnPropertyChanged();
                }
            }
        }
        // Add other bindable properties for SoundWeaver options here

        public SoundWeaverOptionsViewModel()
        {
            LoadSettings(); // Load settings on construction
        }

        public void LoadSettings()
        {
            // Load settings using the specific SoundWeaverSettings type
            var settings = AppSettingsService.LoadModuleSettings<SoundWeaverSettings>("SoundWeaver", () => new SoundWeaverSettings());
            SampleOption = settings.SampleOption;
            // Load other settings from SoundWeaverSettings into properties
        }

        public void Apply()
        {
            // Create a settings object from current properties
            var settings = new SoundWeaverSettings
            {
                SampleOption = this.SampleOption
                // Set other properties for SoundWeaverSettings
            };
            AppSettingsService.SaveModuleSettings("SoundWeaver", settings);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public UserControl GetView()
        {
            // Pass this ViewModel instance to the View if it needs it directly
            // (e.g., if the View's DataContext is set in code-behind or via a ViewModelLocator)
            // For now, assuming SoundWeaverOptionsView sets its DataContext to a new instance of this VM,
            // or it's set by the OptionsUserControl in Assistant.
            // To ensure *this* instance is used:
            var view = new SoundWeaverOptionsView();
            view.DataContext = this; // Ensure the view uses this instance
            return view;
        }
    }
}
