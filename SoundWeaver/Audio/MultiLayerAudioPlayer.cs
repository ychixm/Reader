using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.Audio;
using Microsoft.VisualBasic;
using SoundWeaver.Models;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;

namespace SoundWeaver.Audio
{
    /// <summary>
    /// Mixeur temps-réel : plusieurs pistes NAudio ? flux PCM 48 kHz stéréo 16-bit vers Discord.NET AudioClient.
    /// </summary>
    public class MultiLayerAudioPlayer : IDisposable
    {
        private readonly IAudioClient _audioClient;
        private readonly MixingSampleProvider _mixer;
        private readonly ConcurrentDictionary<string, AudioLayer> _activeLayers = new();
        private readonly int _mixerSampleRate;
        private readonly int _mixerChannels;
        private readonly int _resamplerQuality;

        private CancellationTokenSource _playbackCts;
        private Task _playbackTask;

        public MultiLayerAudioPlayer(
            IAudioClient audioClient,
            int mixerSampleRate = 48000,
            int mixerChannels = 2,
            int resamplerQuality = 60)
        {
            _audioClient = audioClient ?? throw new ArgumentNullException(nameof(audioClient));
            _mixerSampleRate = mixerSampleRate;
            _mixerChannels = mixerChannels;
            _resamplerQuality = resamplerQuality;

            _mixer = new MixingSampleProvider(
                        WaveFormat.CreateIeeeFloatWaveFormat(_mixerSampleRate, _mixerChannels))
            { ReadFully = true };

            StartPlaybackEngine();
        }

        private void StartPlaybackEngine()
        {
            _playbackCts?.Cancel();
            _playbackCts = new CancellationTokenSource();

            _playbackTask = Task.Run(async () =>
            {
                // Discord.NET fournit un AudioOutStream (PCM 16-bit, 20 ms)
                using var pcmStream = _audioClient.CreatePCMStream(AudioApplication.Mixed);

                try
                {
                    await NAudioToDiscordBridge.SendPcmAsync(
                              _mixer.ToWaveProvider16(), pcmStream, 20, _playbackCts.Token);
                }
                finally
                {
                    await pcmStream.FlushAsync();
                }
            }, _playbackCts.Token);
        }

        public AudioLayer AddLayer(AudioTrack track, bool? loopOverride = null, float? initialVolume = null)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));

            var layer = new AudioLayer(
                           track,
                           loopOverride ?? track.IsLooping,
                           _mixerSampleRate,
                           _mixerChannels,
                           _resamplerQuality);

            if (initialVolume.HasValue) layer.Volume = initialVolume.Value;

            if (_activeLayers.TryAdd(layer.Id, layer))
            {
                _mixer.AddMixerInput(layer.SampleProvider);
                layer.PlaybackEnded += OnLayerPlaybackEnded;
                return layer;
            }

            layer.Dispose();
            return null;
        }

        private void OnLayerPlaybackEnded(object? sender, EventArgs e)
        {
            if (sender is AudioLayer l) RemoveLayer(l.Id, true);
        }

        public bool RemoveLayer(string layerId, bool auto = false)
        {
            if (_activeLayers.TryRemove(layerId, out var layer))
            {
                _mixer.RemoveMixerInput(layer.SampleProvider);
                layer.PlaybackEnded -= OnLayerPlaybackEnded;
                layer.Dispose();
                return true;
            }
            return false;
        }

        public void StopAllLayers()
        {
            foreach (var id in _activeLayers.Keys.ToList())
                RemoveLayer(id, true);
        }

        public void Dispose()
        {
            _playbackCts?.Cancel();
            try { _playbackTask?.Wait(TimeSpan.FromSeconds(2)); }
            catch { /* ignore */ }

            _playbackCts?.Dispose();
            StopAllLayers();
        }
    }
}
