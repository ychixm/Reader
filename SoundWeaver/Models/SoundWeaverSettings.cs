namespace SoundWeaver.Models
{
    public class SoundWeaverSettings
    {
        // Example setting, maps to SoundWeaverOptionsViewModel's SampleOption
        public bool SampleOption { get; set; } = true; // Default value

        // Add other SoundWeaver specific settings here
        // For example:
        // public string DefaultBotToken { get; set; }
        // public ulong DefaultGuildId { get; set; }
        // public ulong DefaultChannelId { get; set; }
        // public float DefaultVolume { get; set; } = 1.0f;

        public SoundWeaverSettings()
        {
            // Initialize with default values if necessary
        }
    }
}
