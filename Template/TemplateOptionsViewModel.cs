using Utils; // Required for IOptionsViewModel
using System.ComponentModel; // Required for INotifyPropertyChanged
using System.Runtime.CompilerServices; // Required for CallerMemberName

namespace Template
{
    public class TemplateOptionsViewModel : IOptionsViewModel, INotifyPropertyChanged
    {
        public string OptionsTitle => "Template Project Options";

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

        public TemplateOptionsViewModel()
        {
            // Initialize with default values
            SampleOption = true;
        }

        public void Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                var settings = System.Text.Json.JsonSerializer.Deserialize<JsonSettings>(json);
                if (settings != null)
                {
                    SampleOption = settings.SampleOption;
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                // Handle deserialization error, e.g., log it or load defaults
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        public string Save()
        {
            var settings = new JsonSettings { SampleOption = this.SampleOption };
            return System.Text.Json.JsonSerializer.Serialize(settings);
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
    }
}
