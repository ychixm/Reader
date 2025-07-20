namespace SoundWeaver.Models
{
    public class SoundWeaverSettings
    {
        public bool SampleOption { get; set; } = true;
        public int SelectedChannels { get; set; } = 2;
        public List<ChannelBitrateSetting> ChannelBitrates { get; set; } = new();
    }
}
