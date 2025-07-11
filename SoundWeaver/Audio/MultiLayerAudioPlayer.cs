using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Logging;
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
    public class MultiLayerAudioPlayer : IDisposable
    {
        private VoiceNextConnection _voiceConnection;
        private VoiceTransmitSink _transmitSink;
        private MixingSampleProvider _mixer;
        private CancellationTokenSource _playbackCts;
        private Task _playbackTask;
        private readonly ConcurrentDictionary<string, AudioLayer> _activeLayers = new ConcurrentDictionary<string, AudioLayer>();
        private readonly ILogger<MultiLayerAudioPlayer> _logger;
        private readonly ILogger<AudioLayer> _audioLayerLogger;

        private readonly int _mixerSampleRate;
        private readonly int _mixerChannels;
        private readonly int _resamplerQuality;

        public MultiLayerAudioPlayer(
            VoiceNextConnection voiceConnection,
            ILogger<MultiLayerAudioPlayer> logger,
            ILogger<AudioLayer> audioLayerLogger,
            int mixerSampleRate = 48000,
            int mixerChannels = 2,
            int resamplerQuality = 60)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _audioLayerLogger = audioLayerLogger ?? throw new ArgumentNullException(nameof(audioLayerLogger));
            _voiceConnection = voiceConnection ?? throw new ArgumentNullException(nameof(voiceConnection));

            var guildIdString = _voiceConnection.Channel.Guild?.Id.ToString() ?? "Unknown Guild";
            _logger.LogInformation("MultiLayerAudioPlayer initializing for Guild ID: {GuildId}", guildIdString);

            _transmitSink = _voiceConnection.GetTransmitSink();
            if (_transmitSink == null)
            {
                _logger.LogError("Failed to get VoiceTransmitSink from VoiceNextConnection for Guild ID: {GuildId}.", guildIdString);
                throw new InvalidOperationException("Failed to get VoiceTransmitSink. Cannot proceed with audio playback.");
            }

            _mixerSampleRate = mixerSampleRate;
            _mixerChannels = mixerChannels;
            _resamplerQuality = resamplerQuality;

            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(_mixerSampleRate, _mixerChannels));
            _mixer.ReadFully = true;
            _logger.LogInformation("MultiLayerAudioPlayer initialized. Mixer SR: {SampleRate}Hz, Channels: {Channels}, ResamplerQuality: {Quality}", _mixerSampleRate, _mixerChannels, _resamplerQuality);

            StartPlaybackEngine();
        }

        private void StartPlaybackEngine()
        {
            if (_transmitSink == null)
            {
                _logger.LogError("Cannot start playback engine: VoiceTransmitSink is null.");
                return;
            }
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = new CancellationTokenSource();

            _playbackTask = Task.Run(async () => {
                try
                {
                    // Assuming NAudioToDiscordBridge.SendStreamAsync handles its own logging for start/stop/errors if needed.
                    await NAudioToDiscordBridge.SendStreamAsync(_mixer.ToWaveProvider16(), _transmitSink, 20, _playbackCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Playback task (SendStreamAsync) cancelled as expected.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in NAudioToDiscordBridge.SendStreamAsync task.");
                }
                finally
                {
                    _logger.LogInformation("Playback task (SendStreamAsync) finished for Guild ID: {GuildId}", _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                }
            }, _playbackCts.Token);
            _logger.LogDebug("Playback engine (re)started for Guild ID: {GuildId}", _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
        }

        public AudioLayer AddLayer(AudioTrack track, bool? loopOverride = null, float? initialVolume = null)
        {
            if (track == null)
            {
                _logger.LogError("AddLayer called with a null track.");
                throw new ArgumentNullException(nameof(track));
            }

            _logger.LogInformation("Attempting to add layer for track: {TrackTitle}, Source: {TrackSource}, LoopOverride: {LoopOverride}, InitialVolume: {InitialVolume} for Guild ID: {GuildId}",
                                 track.Title, track.Source.OriginalString, loopOverride, initialVolume, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");

            try
            {
                var layer = new AudioLayer(
                    track,
                    _audioLayerLogger,
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
                    _logger.LogInformation("Successfully added layer: {TrackTitle} (ID: {LayerId}), Volume: {Volume}, Looping: {Looping} for Guild ID: {GuildId}",
                                         track.Title, layer.Id, layer.Volume, layer.IsLooping, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                    return layer;
                }
                else
                {
                    _logger.LogWarning("Failed to add layer to ConcurrentDictionary (duplicate ID or other ConcurrentDictionary issue for track): {TrackTitle}, Layer ID: {LayerId} for Guild ID: {GuildId}",
                                     track.Title, layer.Id, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                    layer.Dispose();
                    return null;
                }
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "File not found for track {TrackTitle} when trying to add layer. Path: {FilePath} for Guild ID: {GuildId}",
                               track.Title, ex.FileName, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error adding layer for track {TrackTitle} for Guild ID: {GuildId}",
                               track.Title, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                return null;
            }
        }

        private void OnLayerPlaybackEnded(object sender, EventArgs e)
        {
            if (sender is AudioLayer layer)
            {
                _logger.LogInformation("Layer '{TrackTitle}' (ID: {LayerId}) has finished playback and is being removed for Guild ID: {GuildId}.",
                                     layer.Track.Title, layer.Id, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                RemoveLayer(layer.Id, true);
            }
            else
            {
                _logger.LogWarning("OnLayerPlaybackEnded received event from unexpected sender type: {SenderType} for Guild ID: {GuildId}",
                                 sender?.GetType().Name ?? "null", _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
            }
        }

        public void UpdateLayerStates()
        {
            _logger.LogTrace("UpdateLayerStates called. Active layers: {Count} for Guild ID: {GuildId}", _activeLayers.Count, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
            foreach (var layer in _activeLayers.Values.ToList())
            {
                layer.CheckPlaybackEnded();
            }
        }

        public bool RemoveLayer(string layerId, bool autoCleanup = false)
        {
            if (string.IsNullOrEmpty(layerId))
            {
                _logger.LogWarning("RemoveLayer called with null or empty layerId for Guild ID: {GuildId}.", _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                return false;
            }
            if (_activeLayers.TryRemove(layerId, out AudioLayer layer))
            {
                _logger.LogInformation("Removing layer: {TrackTitle} (ID: {LayerId}) for Guild ID: {GuildId}",
                                     layer.Track?.Title ?? "N/A", layer.Id, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                _mixer.RemoveMixerInput(layer.SampleProvider);
                layer.PlaybackEnded -= OnLayerPlaybackEnded;
                layer.Dispose();
                _logger.LogInformation("Successfully removed layer: {TrackTitle} (ID: {LayerId}) for Guild ID: {GuildId}",
                                     layer.Track?.Title ?? "N/A", layer.Id, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                return true;
            }

            if (!autoCleanup)
            {
                _logger.LogWarning("Layer with ID {LayerId} not found for removal (explicit attempt) for Guild ID: {GuildId}.", layerId, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
            }
            else
            {
                _logger.LogTrace("Layer with ID {LayerId} not found for removal (auto-cleanup, might be already removed) for Guild ID: {GuildId}.", layerId, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
            }
            return false;
        }

        public void RemoveLayer(AudioTrack track)
        {
            if (track == null)
            {
                _logger.LogWarning("RemoveLayer called with a null track instance for Guild ID: {GuildId}.", _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                return;
            }
            _logger.LogInformation("Attempting to remove layer by track object: {TrackTitle} for Guild ID: {GuildId}", track.Title, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
            var layerInstance = _activeLayers.FirstOrDefault(kvp => kvp.Value.Track == track).Value;
            if (layerInstance != null)
            {
                RemoveLayer(layerInstance.Id);
            }
            else
            {
                _logger.LogInformation("No active layer found for track: {TrackTitle} during removal attempt by track object for Guild ID: {GuildId}.", track.Title, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
            }
        }

        public AudioLayer GetLayer(string layerId)
        {
            if (_activeLayers.TryGetValue(layerId, out var layer))
            {
                return layer;
            }
            _logger.LogDebug("Layer with ID {LayerId} not found in GetLayer for Guild ID: {GuildId}.", layerId, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
            return null;
        }

        public bool SetVolume(string layerId, float volume)
        {
            if (_activeLayers.TryGetValue(layerId, out AudioLayer layer))
            {
                float oldVolume = layer.Volume;
                layer.Volume = volume;
                _logger.LogInformation("MultiLayerAudioPlayer: Set volume for layer {TrackTitle} (ID: {LayerId}) from {OldVolume} to {NewVolume} for Guild ID: {GuildId}",
                                     layer.Track.Title, layerId, oldVolume, layer.Volume, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                return true;
            }
            _logger.LogWarning("Layer with ID {LayerId} not found for volume adjustment for Guild ID: {GuildId}.", layerId, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
            return false;
        }

        public bool SetLooping(string layerId, bool loop)
        {
            if (_activeLayers.TryGetValue(layerId, out AudioLayer layer))
            {
                if (layer.IsLooping != loop)
                {
                    layer.IsLooping = loop;
                    _logger.LogInformation("MultiLayerAudioPlayer: Set looping for layer {TrackTitle} (ID: {LayerId}) to {LoopState} for Guild ID: {GuildId}",
                                         layer.Track.Title, layerId, loop, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                }
                else
                {
                    _logger.LogDebug("MultiLayerAudioPlayer: Looping for layer {TrackTitle} (ID: {LayerId}) already set to {LoopState} for Guild ID: {GuildId}.",
                                   layer.Track.Title, layerId, loop, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
                }
                return true;
            }
            _logger.LogWarning("Layer with ID {LayerId} not found for loop adjustment for Guild ID: {GuildId}.", layerId, _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild");
            return false;
        }

        public void StopAllLayers()
        {
            var guildIdString = _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild";
            _logger.LogInformation("Stopping all layers for Guild ID: {GuildId}. Current count: {Count}", guildIdString, _activeLayers.Count);
            var layerIds = _activeLayers.Keys.ToList();
            foreach (var layerId in layerIds)
            {
                RemoveLayer(layerId, true);
            }
            _logger.LogInformation("All layers processed for stopping for Guild ID: {GuildId}. Active layers should be 0, actual: {Count}", guildIdString, _activeLayers.Count);
        }

        public void Dispose()
        {
            var guildIdString = _voiceConnection?.Channel.Guild?.Id.ToString() ?? "Unknown Guild (already nulled during shutdown)";
            _logger.LogInformation("Disposing MultiLayerAudioPlayer for Guild ID: {GuildId}...", guildIdString);

            _playbackCts?.Cancel();
            try
            {
                if (_playbackTask != null && !_playbackTask.IsCompleted)
                {
                    _logger.LogDebug("Waiting for playback task to complete for Guild ID: {GuildId}...", guildIdString);
                    bool completed = _playbackTask.Wait(TimeSpan.FromSeconds(2));
                    if (!completed)
                    {
                        _logger.LogWarning("Playback task did not complete within the timeout period during dispose for Guild ID: {GuildId}.", guildIdString);
                    }
                    else
                    {
                        _logger.LogDebug("Playback task completed for Guild ID: {GuildId}.", guildIdString);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Playback task was canceled during dispose for Guild ID: {GuildId}, as expected if StopPlayback was effective.", guildIdString);
            }
            catch (AggregateException ae)
            {
                ae.Handle(ex => {
                    if (ex is OperationCanceledException)
                    {
                        _logger.LogInformation("Playback task cancellation token triggered via AggregateException as expected during dispose for Guild ID: {GuildId}.", guildIdString);
                        return true;
                    }
                    _logger.LogError(ex, "Unexpected exception in AggregateException during playback task shutdown in Dispose for Guild ID: {GuildId}.", guildIdString);
                    return false;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic exception during playback task shutdown in Dispose for Guild ID: {GuildId}.", guildIdString);
            }
            finally
            {
                _playbackCts?.Dispose();
                _playbackCts = null;
                _playbackTask = null;
            }

            StopAllLayers();

            _voiceConnection = null;
            _transmitSink = null;

            _logger.LogInformation("MultiLayerAudioPlayer disposed for Guild ID: {GuildId}.", guildIdString);
        }
    }
}
