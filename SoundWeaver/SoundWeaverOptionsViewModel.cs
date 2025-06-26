using Utils; // Required for IOptionsViewModel
using System.ComponentModel; // Required for INotifyPropertyChanged
using System.Runtime.CompilerServices; // Required for CallerMemberName

namespace SoundWeaver
{
    public class SoundWeaverOptionsViewModel : IOptionsViewModel, INotifyPropertyChanged
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

        public SoundWeaverOptionsViewModel()
        {
            // Initialize with default values
            SampleOption = true;
        }

        public void LoadSettings()
        {
            var settings = Utils.AppSettingsService.LoadModuleSettings<JsonSettings>("SoundWeaver");
            if (settings != null)
            {
                SampleOption = settings.SampleOption;
            }
            // If settings are null, defaults set in the constructor will be used.
        }

        public void Apply()
        {
            var settings = new JsonSettings { SampleOption = this.SampleOption };
            Utils.AppSettingsService.SaveModuleSettings("SoundWeaver", settings);
        }

        // Helper class for JSON serialization
        private class JsonSettings
        {
            public bool SampleOption { get; set; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public System.Windows.Controls.UserControl GetView()
        {
            return new SoundWeaverOptionsView();
        }
    }
}
