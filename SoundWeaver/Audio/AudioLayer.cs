using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundWeaver.Models;
using System;
using System.IO;

namespace SoundWeaver.Audio
{
    // Represents an active audio layer in the player
    public class AudioLayer : IDisposable
    {
        public string Id { get; }
        public AudioTrack Track { get; }
        public WaveStream WaveStream { get; private set; }
        public ISampleProvider SampleProvider { get; private set; }
        private readonly ILogger<AudioLayer> _logger;

        private LoopStream _loopStreamLogic; // Helper for looping
        private VolumeSampleProvider _volumeProvider;
        private MediaFoundationResampler _resamplerInstance; // To dispose correctly

        public bool IsLooping
        {
            get => _loopStreamLogic?.EnableLooping ?? false;
            set
            {
                if (_loopStreamLogic != null)
                {
                    _logger.LogTrace("Setting IsLooping to {LoopState} for Layer ID: {LayerId}", value, Id);
                    _loopStreamLogic.EnableLooping = value;
                }
                else
                {
                    _logger.LogWarning("Attempted to set IsLooping on Layer ID: {LayerId}, but LoopStream logic is not initialized (likely not a looping track).", Id);
                }
            }
        }

        public float Volume
        {
            get => _volumeProvider?.Volume ?? 1.0f;
            set
            {
                if (_volumeProvider != null)
                {
                    var clampedValue = Math.Clamp(value, 0.0f, 2.0f);
                    _logger.LogTrace("Setting Volume to {Volume} (clamped from {OriginalValue}) for Layer ID: {LayerId}", clampedValue, value, Id);
                    _volumeProvider.Volume = clampedValue;
                }
            }
        }

        public event EventHandler PlaybackEnded;

        public AudioLayer(
            AudioTrack track,
            ILogger<AudioLayer> logger,
            bool forceLoop = false,
            int targetSampleRate = 48000,
            int targetChannels = 2,
            int resamplerQuality = 60)
        {
            Id = Guid.NewGuid().ToString();
            Track = track ?? throw new ArgumentNullException(nameof(track));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("Creating AudioLayer ID: {LayerId} for Track: {TrackTitle}, Source: {TrackSource}, ForcedLoop: {ForceLoop}, TargetSR: {TargetSR}, TargetChannels: {TargetChannels}",
                                 Id, Track.Title, Track.Source.OriginalString, forceLoop, targetSampleRate, targetChannels);

            string sourcePath = track.Source.IsFile ? track.Source.LocalPath : track.Source.AbsoluteUri;

            if (track.Source.IsFile && !File.Exists(sourcePath))
            {
                _logger.LogError("Audio file not found for AudioLayer ID: {LayerId}. Path: {SourcePath}", Id, sourcePath);
                throw new FileNotFoundException($"Audio file not found for track '{Track.Title}'.", sourcePath);
            }

            try
            {
                WaveStream = new AudioFileReader(sourcePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AudioFileReader for AudioLayer ID: {LayerId}, Path: {SourcePath}", Id, sourcePath);
                throw;
            }

            bool actualLoop = forceLoop || track.IsLooping;
            _logger.LogDebug("AudioLayer ID: {LayerId} - Actual loop state: {ActualLoop} (Track.IsLooping: {TrackLoop}, ForceLoop: {ForceLoop})", Id, actualLoop, track.IsLooping, forceLoop);


            if (actualLoop)
            {
                _loopStreamLogic = new LoopStream(WaveStream, true); // LoopStream handles its own WaveStream
                _volumeProvider = new VolumeSampleProvider(_loopStreamLogic.ToSampleProvider());
                _logger.LogDebug("AudioLayer ID: {LayerId} - LoopStream and VolumeSampleProvider (from loop) initialized.", Id);
            }
            else
            {
                // Non-looping, WaveStream is directly used by VolumeSampleProvider's source
                _volumeProvider = new VolumeSampleProvider(WaveStream.ToSampleProvider());
                _logger.LogDebug("AudioLayer ID: {LayerId} - VolumeSampleProvider (from direct WaveStream) initialized.", Id);
            }

            Volume = track.Volume; // Apply initial volume

            SampleProvider = EnsureCompatibleFormat(_volumeProvider, targetSampleRate, targetChannels, resamplerQuality);
            _logger.LogInformation("AudioLayer ID: {LayerId} successfully created. Output WaveFormat: {WaveFormat}", Id, SampleProvider.WaveFormat);
        }

        private ISampleProvider EnsureCompatibleFormat(ISampleProvider input, int sampleRate, int channels, int quality)
        {
            var currentFormat = input.WaveFormat;
            if (currentFormat.SampleRate == sampleRate && currentFormat.Channels == channels)
            {
                _logger.LogDebug("AudioLayer ID: {LayerId} - Input format matches target format ({SampleRate}Hz, {Channels}ch). No resampling needed.", Id, sampleRate, channels);
                return input;
            }

            _logger.LogInformation("AudioLayer ID: {LayerId} - Resampling required. From: {CurrentSampleRate}Hz/{CurrentChannels}ch to {TargetSampleRate}Hz/{TargetChannels}ch. Quality: {Quality}",
                                 Id, currentFormat.SampleRate, currentFormat.Channels, sampleRate, channels, quality);

            _resamplerInstance = new MediaFoundationResampler(input.ToWaveProvider(), new WaveFormat(sampleRate, channels)) // Assuming input bits per sample is fine
            {
                ResamplerQuality = quality // 0-60, 60 is best quality
            };
            return _resamplerInstance.ToSampleProvider();
        }

        // Called by MultiLayerAudioPlayer to check if non-looping tracks have finished
        public void CheckPlaybackEnded()
        {
            if (WaveStream != null && WaveStream.Position >= WaveStream.Length && !IsLooping)
            {
                _logger.LogInformation("AudioLayer ID: {LayerId} - Playback ended for track: {TrackTitle}", Id, Track.Title);
                PlaybackEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            _logger.LogDebug("Disposing AudioLayer ID: {LayerId}, Track: {TrackTitle}", Id, Track?.Title ?? "N/A");

            PlaybackEnded = null; // Remove all subscribers

            _loopStreamLogic?.Dispose(); // Disposes the underlying source stream if it was wrapped
            WaveStream?.Dispose(); // Dispose original stream if not wrapped by LoopStream or if LoopStream doesn't own it (check LoopStream impl)
                                   // Assuming LoopStream disposes its source, WaveStream might be double-disposed if also original.
                                   // Corrected LoopStream should handle this. For now, this is typical NAudio pattern.
            _resamplerInstance?.Dispose();

            // Nullify to prevent reuse and help GC
            WaveStream = null;
            SampleProvider = null;
            _volumeProvider = null; // This is just a wrapper, doesn't own the stream
            _loopStreamLogic = null;
            _resamplerInstance = null;

            _logger.LogInformation("AudioLayer ID: {LayerId} disposed.", Id);
        }
    }
}
