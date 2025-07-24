using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SoundWeaver.Audio;

namespace SoundWeaver.Models
{
    public enum SfxType { Instant, Continuous }

    public class SfxElement : INotifyPropertyChanged
    {
        public string FilePath { get; set; }
        public string DisplayName { get; set; }
        public double Volume { get; set; } = 1.0;
        public SfxType Type { get; set; }
        public SfxLayerState State { get; set; } = SfxLayerState.Stopped;
        public TimeSpan? PausePosition { get; set; }


        // Lecture
        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(); }
        }

        private TimeSpan _position;
        public TimeSpan Position
        {
            get => _position;
            set { _position = value; OnPropertyChanged(); }
        }

        public double PositionSeconds
        {
            get => Position.TotalSeconds;
            set
            {
                var newPos = TimeSpan.FromSeconds(value);
                if (newPos != Position)
                {
                    Position = newPos;
                    OnPropertyChanged();
                }
            }
        }

        private TimeSpan _duration;
        public TimeSpan Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
