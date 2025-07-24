using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SoundWeaver.Models
{
    public class ChannelSetting : INotifyPropertyChanged
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public string ChannelName { get; set; }

        private int _discordBitrateCap = 96000;
        public int DiscordBitrateCap
        {
            get => _discordBitrateCap;
            set { if (_discordBitrateCap != value) { _discordBitrateCap = value; OnPropertyChanged(); } }
        }

        private int _bitrate = 64000;
        public int Bitrate
        {
            get => _bitrate;
            set
            {
                int val = Math.Clamp(value, 8000, DiscordBitrateCap);
                if (_bitrate != val)
                {
                    _bitrate = val;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
