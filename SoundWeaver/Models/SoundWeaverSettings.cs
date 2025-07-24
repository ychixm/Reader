namespace SoundWeaver.Models
{
    public class SoundWeaverSettings
    {
        public bool SampleOption { get; set; } = true;
        public int SelectedChannels { get; set; } = 2;
        public List<ChannelSetting> ChannelSettings { get; set; } = new();
        public string DiscordToken { get; set; }
        public List<SfxElement> SfxElements { get; set; } = new();
    }
}
