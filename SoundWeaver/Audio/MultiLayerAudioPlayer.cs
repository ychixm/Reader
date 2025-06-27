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
    // Represents an active audio layer in the player
    public class AudioLayer : IDisposable
    {
        public string Id { get; }
        public AudioTrack Track { get; }
        public WaveStream WaveStream { get; private set; } // The raw audio stream (e.g., AudioFileReader)
        public ISampleProvider SampleProvider { get; private set; } // The stream after volume/effects, before mixing

        private LoopStream _loopStreamLogic; // Handles looping for this layer
        private VolumeSampleProvider _volumeProvider;

        public bool IsLooping
        {
            get => _loopStreamLogic?.EnableLooping ?? false;
            set
            {
                if (_loopStreamLogic != null)
                    _loopStreamLogic.EnableLooping = value;
                else if (value && WaveStream != null && WaveStream.CanSeek) // Create loopstream if enabling loop on non-looping
                {
                    // This case is tricky if it's already in the mixer. Best to set loop at AddLayer.
                    // For now, assume IsLooping is set at creation via Track.IsLooping.
                }
            }
        }

        public float Volume
        {
            get => _volumeProvider?.Volume ?? 1.0f;
            set
            {
                if (_volumeProvider != null)
                    _volumeProvider.Volume = Math.Clamp(value, 0.0f, 2.0f); // Example clamp
            }
        }

        public event EventHandler PlaybackEnded; // Fires when the non-looping stream ends

        public AudioLayer(AudioTrack track, bool forceLoop = false)
        {
            Id = Guid.NewGuid().ToString(); // Unique ID for this layer instance
            Track = track ?? throw new ArgumentNullException(nameof(track));

            string sourcePath = track.Source.IsFile ? track.Source.LocalPath : track.Source.AbsoluteUri;
            if (track.Source.IsFile && !File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Audio file not found.", sourcePath);
            }

            // For URLs, AudioFileReader would need to download it first or handle streaming if capable.
            // NAudio's AudioFileReader primarily works with local files. For URLs, custom handling or different reader might be needed.
            // Assuming for now source is a local path if IsFile, otherwise it's a direct streamable URL (less common for AudioFileReader).
            WaveStream = new AudioFileReader(sourcePath);

            bool actualLoop = forceLoop || track.IsLooping;
            if (actualLoop)
            {
                _loopStreamLogic = new LoopStream(WaveStream, true); // LoopStream takes ownership of WaveStream if enabled
                _volumeProvider = new VolumeSampleProvider(_loopStreamLogic.ToSampleProvider());
            }
            else
            {
                _volumeProvider = new VolumeSampleProvider(WaveStream.ToSampleProvider());
                // Hook into PlaybackStopped for non-looping streams if WaveStream is IWavePlayer compatible
                // Or handle end-of-stream in the Read method of a wrapper.
                // For MixingSampleProvider, it just stops reading from a source when it ends.
                // We need a way to detect this to fire PlaybackEnded.
                // This can be done by wrapping the WaveStream in another class that monitors Read() calls.
            }

            SampleProvider = _volumeProvider; // This is what gets added to the mixer
            Volume = track.Volume; // Set initial volume from track model
        }

        // Simplified way to signal end, more robust would be to check read bytes.
        public void CheckPlaybackEnded()
        {
            if (WaveStream != null && WaveStream.Position >= WaveStream.Length && !IsLooping)
            {
                PlaybackEnded?.Invoke(this, EventArgs.Empty);
            }
        }


        public void Dispose()
        {
            // SampleProvider is derived from WaveStream here, so disposing WaveStream should be enough
            _loopStreamLogic?.Dispose(); // This will dispose the underlying WaveStream if it was used
            WaveStream?.Dispose(); // Dispose if not used by LoopStream or if LoopStream is null
            WaveStream = null;
            SampleProvider = null;
            _volumeProvider = null;
            _loopStreamLogic = null;
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

        public MultiLayerAudioPlayer(VoiceNextConnection voiceConnection)
        {
            _voiceConnection = voiceConnection ?? throw new ArgumentNullException(nameof(voiceConnection));
            _transmitSink = _voiceConnection.GetTransmitSink();

            // Output format for mixer: Stereo, 48kHz, IEEE Float.
            // DSharpPlus VoiceNext will handle Opus encoding from PCM.
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
            _mixer.ReadFully = true; // Ensures continuous playback even if one source finishes

            StartPlaybackEngine();
        }

        private void StartPlaybackEngine()
        {
            _playbackCts?.Cancel();
            _playbackCts = new CancellationTokenSource();
            // The bridge converts the mixer output (which is ISampleProvider) to IWaveProvider (16-bit PCM)
            _playbackTask = Task.Run(() => NAudioToDiscordBridge.SendStreamAsync(_mixer.ToWaveProvider16(), _transmitSink, 20, _playbackCts.Token), _playbackCts.Token);
        }

        public AudioLayer AddLayer(AudioTrack track, bool? loopOverride = null, float? initialVolume = null)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));

            try
            {
                var layer = new AudioLayer(track, loopOverride ?? track.IsLooping);
                if (initialVolume.HasValue) layer.Volume = initialVolume.Value;
                else layer.Volume = track.Volume; // Use volume from track model if not overridden

                if (_activeLayers.TryAdd(layer.Id, layer))
                {
                    _mixer.AddMixerInput(layer.SampleProvider);
                    layer.PlaybackEnded += OnLayerPlaybackEnded;
                    Console.WriteLine($"Added layer: {track.Title} (ID: {layer.Id})");
                    return layer;
                }
                else
                {
                    layer.Dispose(); // Dispose if couldn't add
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
                RemoveLayer(layer.Id, true); // Automatically remove if it ended and isn't looping
            }
        }

        // Call this periodically to check for non-looping tracks that have ended.
        public void UpdateLayerStates()
        {
            foreach (var layer in _activeLayers.Values.ToList()) // ToList to avoid modification issues during iteration
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
            if(!autoCleanup) Console.WriteLine($"Layer with ID {layerId} not found for removal.");
            return false;
        }

        public void RemoveLayer(AudioTrack track) // Convenience, finds first layer with this track
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
                // This is tricky if the layer was not initially created with LoopStream.
                // The current AudioLayer setup initializes LoopStream at creation.
                // Dynamically adding/removing LoopStream to an existing SampleProvider in the mixer is complex.
                // For now, this will only toggle loop if LoopStream was already there.
                // A more robust solution would involve replacing the SampleProvider in the mixer.
                if (layer.IsLooping != loop) // Only act if there's a change
                {
                     if (layer.Track.Source.IsFile && layer.WaveStream.CanSeek) // Only if seekable and was file based
                     {
                        // This is a simplification. True dynamic change would require re-creating the layer's sample provider chain.
                        // For now, let's assume we'd need to remove and re-add the layer to change looping status post-creation
                        // if it wasn't initially set up with LoopStream.
                        // However, if LoopStream *is* present, its EnableLooping can be toggled.
                        if (layer.IsLooping && !loop) { // Turning loop off
                             layer.IsLooping = false; // loopStream.EnableLooping = false;
                        } else if (!layer.IsLooping && loop) { // Turning loop on
                            // This path is the most problematic if no LoopStream was setup.
                            // For now, we'll say this only works if it was *initially* loopable.
                            // The AudioLayer constructor tries to set up LoopStream if track.IsLooping is true.
                            layer.IsLooping = true; // loopStream.EnableLooping = true;
                            // To make it truly dynamic, you'd replace the ISampleProvider in the mixer.
                            // E.g., _mixer.RemoveMixerInput(...); layer.ReconfigureLooping(loop); _mixer.AddMixerInput(...);
                            Console.WriteLine($"Note: Dynamically enabling looping on a non-initially-looped track has limitations in current setup.");
                        }
                     } else {
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
            foreach (var layerId in _activeLayers.Keys.ToList()) // ToList to allow modification
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
                _playbackTask?.Wait(TimeSpan.FromSeconds(2)); // Wait for playback task to finish
            }
            catch (OperationCanceledException) { /* Expected */ }
            catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is OperationCanceledException)) { /* Expected */ }


            _playbackCts?.Dispose();
            _playbackCts = null;
            _playbackTask = null;

            StopAllLayers(); // Ensures all layers are disposed

            // _transmitSink is managed by VoiceNextConnection, should not be disposed here.
            _voiceConnection = null; // Don't dispose the connection itself, it's managed externally.

            Console.WriteLine("MultiLayerAudioPlayer disposed.");
        }
    }
}
