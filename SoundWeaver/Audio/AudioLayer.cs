using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundWeaver.Models;

namespace SoundWeaver.Audio
{
    public class AudioLayer : IDisposable
    {
        public string Id { get; }
        public AudioTrack Track { get; }
        public WaveStream WaveStream { get; private set; }
        public ISampleProvider SampleProvider { get; private set; }

        private LoopStream _loopStreamLogic;
        private VolumeSampleProvider _volumeProvider;
        private MediaFoundationResampler _resamplerInstance; // Pour disposer correctement

        public bool IsLooping
        {
            get => _loopStreamLogic?.EnableLooping ?? false;
            set
            {
                if (_loopStreamLogic != null)
                    _loopStreamLogic.EnableLooping = value;
            }
        }

        public float Volume
        {
            get => _volumeProvider?.Volume ?? 1.0f;
            set
            {
                if (_volumeProvider != null)
                    _volumeProvider.Volume = Math.Clamp(value, 0.0f, 2.0f);
            }
        }

        public event EventHandler PlaybackEnded;

        public AudioLayer(
            AudioTrack track,
            bool forceLoop = false,
            int targetSampleRate = 48000,
            int targetChannels = 2,
            int resamplerQuality = 60)
        {
            Id = Guid.NewGuid().ToString();
            Track = track ?? throw new ArgumentNullException(nameof(track));

            string sourcePath = track.Source.IsFile ? track.Source.LocalPath : track.Source.AbsoluteUri;
            if (track.Source.IsFile && !File.Exists(sourcePath))
                throw new FileNotFoundException("Audio file not found.", sourcePath);

            WaveStream = new AudioFileReader(sourcePath);

            bool actualLoop = forceLoop || track.IsLooping;

            if (actualLoop)
            {
                _loopStreamLogic = new LoopStream(WaveStream, true);
                _volumeProvider = new VolumeSampleProvider(_loopStreamLogic.ToSampleProvider());
            }
            else
            {
                _volumeProvider = new VolumeSampleProvider(WaveStream.ToSampleProvider());
            }

            Volume = track.Volume;

            // Conversion de format pour compatibilité avec le mixer (par défaut 48kHz/stéréo/float)
            SampleProvider = EnsureCompatibleFormat(_volumeProvider, targetSampleRate, targetChannels, resamplerQuality);
        }

        private ISampleProvider EnsureCompatibleFormat(ISampleProvider input, int sampleRate, int channels, int quality)
        {
            var wf = input.WaveFormat;
            if (wf.SampleRate != sampleRate || wf.Channels != channels)
            {
                _resamplerInstance = new MediaFoundationResampler(
                    input.ToWaveProvider(),
                    new WaveFormat(sampleRate, wf.BitsPerSample, channels))
                {
                    ResamplerQuality = quality
                };
                input = _resamplerInstance.ToSampleProvider();
            }
            return input;
        }

        public void CheckPlaybackEnded()
        {
            if (WaveStream != null && WaveStream.Position >= WaveStream.Length && !IsLooping)
            {
                PlaybackEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            _loopStreamLogic?.Dispose();
            WaveStream?.Dispose();
            _resamplerInstance?.Dispose();
            WaveStream = null;
            SampleProvider = null;
            _volumeProvider = null;
            _loopStreamLogic = null;
            _resamplerInstance = null;
        }
    }
}
