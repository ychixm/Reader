using DSharpPlus.VoiceNext;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundWeaver.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SoundWeaver.Audio
{
    // Représente une couche audio active dans le lecteur
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

    public class MultiLayerAudioPlayer : IDisposable
    {
        private VoiceNextConnection _voiceConnection;
        private VoiceTransmitSink _transmitSink;
        private MixingSampleProvider _mixer;
        private CancellationTokenSource _playbackCts;
        private Task _playbackTask;
        private readonly ConcurrentDictionary<string, AudioLayer> _activeLayers = new ConcurrentDictionary<string, AudioLayer>();

        // Mixer params
        private readonly int _mixerSampleRate;
        private readonly int _mixerChannels;
        private readonly int _resamplerQuality;

        /// <summary>
        /// MixerProfile: permet d'ajuster la qualité et le coût CPU.
        /// </summary>
        public MultiLayerAudioPlayer(
            VoiceNextConnection voiceConnection,
            int mixerSampleRate = 48000,
            int mixerChannels = 2,
            int resamplerQuality = 60 // 1=rapide, 60=meilleur qualité/plus CPU
        )
        {
            _voiceConnection = voiceConnection ?? throw new ArgumentNullException(nameof(voiceConnection));
            _transmitSink = _voiceConnection.GetTransmitSink();

            _mixerSampleRate = mixerSampleRate;
            _mixerChannels = mixerChannels;
            _resamplerQuality = resamplerQuality;

            // Mixer format paramétrable
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(_mixerSampleRate, _mixerChannels));
            _mixer.ReadFully = true; // Continue même si un flux termine

            StartPlaybackEngine();
        }

        private void StartPlaybackEngine()
        {
            _playbackCts?.Cancel();
            _playbackCts = new CancellationTokenSource();
            _playbackTask = Task.Run(() =>
                NAudioToDiscordBridge.SendStreamAsync(_mixer.ToWaveProvider16(), _transmitSink, 20, _playbackCts.Token), _playbackCts.Token);
        }

        public AudioLayer AddLayer(AudioTrack track, bool? loopOverride = null, float? initialVolume = null)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));

            try
            {
                var layer = new AudioLayer(
                    track,
                    loopOverride ?? track.IsLooping,
                    _mixerSampleRate,
                    _mixerChannels,
                    _resamplerQuality);

                if (initialVolume.HasValue)
                    layer.Volume = initialVolume.Value;
                else
                    layer.Volume = track.Volume;

                if (_activeLayers.TryAdd(layer.Id, layer))
                {
                    _mixer.AddMixerInput(layer.SampleProvider);
                    layer.PlaybackEnded += OnLayerPlaybackEnded;
                    Console.WriteLine($"Added layer: {track.Title} (ID: {layer.Id})");
                    return layer;
                }
                else
                {
                    layer.Dispose();
                    Console.WriteLine($"Failed to add layer (duplicate ID?): {track.Title}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding layer for track {track.Title}: {ex.Message}");
                return null;
            }
        }

        private void OnLayerPlaybackEnded(object sender, EventArgs e)
        {
            if (sender is AudioLayer layer)
            {
                Console.WriteLine($"Layer '{layer.Track.Title}' (ID: {layer.Id}) has finished playback.");
                RemoveLayer(layer.Id, true);
            }
        }

        public void UpdateLayerStates()
        {
            foreach (var layer in _activeLayers.Values.ToList())
            {
                if (!layer.IsLooping && layer.WaveStream != null && layer.WaveStream.Position >= layer.WaveStream.Length)
                {
                    OnLayerPlaybackEnded(layer, EventArgs.Empty);
                }
            }
        }

        public bool RemoveLayer(string layerId, bool autoCleanup = false)
        {
            if (_activeLayers.TryRemove(layerId, out AudioLayer layer))
            {
                _mixer.RemoveMixerInput(layer.SampleProvider);
                layer.PlaybackEnded -= OnLayerPlaybackEnded;
                layer.Dispose();
                Console.WriteLine($"Removed layer: {layer.Track.Title} (ID: {layer.Id})");
                return true;
            }
            if (!autoCleanup) Console.WriteLine($"Layer with ID {layerId} not found for removal.");
            return false;
        }

        public void RemoveLayer(AudioTrack track)
        {
            var layerInstance = _activeLayers.FirstOrDefault(kvp => kvp.Value.Track == track).Value;
            if (layerInstance != null)
            {
                RemoveLayer(layerInstance.Id);
            }
            else
            {
                Console.WriteLine($"No active layer found for track: {track.Title}");
            }
        }

        public AudioLayer GetLayer(string layerId)
        {
            _activeLayers.TryGetValue(layerId, out var layer);
            return layer;
        }

        public bool SetVolume(string layerId, float volume)
        {
            if (_activeLayers.TryGetValue(layerId, out AudioLayer layer))
            {
                layer.Volume = volume;
                Console.WriteLine($"Set volume for layer {layer.Track.Title} (ID: {layerId}) to {volume}");
                return true;
            }
            Console.WriteLine($"Layer with ID {layerId} not found for volume adjustment.");
            return false;
        }

        public bool SetLooping(string layerId, bool loop)
        {
            if (_activeLayers.TryGetValue(layerId, out AudioLayer layer))
            {
                if (layer.IsLooping != loop)
                {
                    if (layer.Track.Source.IsFile && layer.WaveStream.CanSeek)
                    {
                        if (layer.IsLooping && !loop)
                        {
                            layer.IsLooping = false;
                        }
                        else if (!layer.IsLooping && loop)
                        {
                            layer.IsLooping = true;
                            Console.WriteLine($"Note: Dynamic enabling of looping after layer creation has limitations in current setup.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Cannot change looping for layer {layer.Track.Title} (ID: {layerId}): stream not seekable or not file-based for re-init.");
                        return false;
                    }
                }
                Console.WriteLine($"Set looping for layer {layer.Track.Title} (ID: {layerId}) to {loop}");
                return true;
            }
            Console.WriteLine($"Layer with ID {layerId} not found for loop adjustment.");
            return false;
        }

        public void StopAllLayers()
        {
            foreach (var layerId in _activeLayers.Keys.ToList())
            {
                RemoveLayer(layerId, true);
            }
            Console.WriteLine("All layers stopped and removed.");
        }

        public void Dispose()
        {
            _playbackCts?.Cancel();
            try
            {
                _playbackTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (OperationCanceledException) { }
            catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is OperationCanceledException)) { }

            _playbackCts?.Dispose();
            _playbackCts = null;
            _playbackTask = null;

            StopAllLayers();

            _voiceConnection = null;

            Console.WriteLine("MultiLayerAudioPlayer disposed.");
        }
    }
}
