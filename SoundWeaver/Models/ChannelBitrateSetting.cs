using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SoundWeaver.Models
{
    public class ChannelBitrateSetting : INotifyPropertyChanged
    {
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
