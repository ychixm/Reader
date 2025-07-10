using DSharpPlus.VoiceNext;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundWeaver.Models; // Assuming AudioTrack will be here
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SoundWeaver.Audio
{
    public class AudioPlayer : IDisposable
    {
        private VoiceNextConnection _voiceConnection;
        private VoiceTransmitSink _transmitSink;
        private MixingSampleProvider _mixer;
        private CancellationTokenSource _playbackCts;
        private Task _playbackTask;

        // Store readers to dispose them later, maybe tie them to an AudioTrack model instance
        private ConcurrentDictionary<string, IWavePlayer> _outputDevices; // For local playback if needed
        private ConcurrentDictionary<string, WaveStream> _fileReaders; // For managing active streams

        public AudioPlayer(VoiceNextConnection voiceConnection)
        {
            _voiceConnection = voiceConnection ?? throw new ArgumentNullException(nameof(voiceConnection));
            _transmitSink = _voiceConnection.GetTransmitSink();

            // Mixer should be in a format suitable for Discord: 48kHz. DSharpPlus VoiceNext handles Opus encoding.
            // We will aim for stereo IEEE float for the mixer, then convert to PCM 16-bit stereo for the sink.
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)); // Stereo, 48kHz
            _mixer.ReadFully = true; // Important for continuous playback

            _outputDevices = new ConcurrentDictionary<string, IWavePlayer>();
            _fileReaders = new ConcurrentDictionary<string, WaveStream>();
        }

        public async Task PlayFileAsync(string filePath, bool loop = false, float volume = 1.0f)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found {filePath}");
                return;
            }

            try
            {
                var audioFileReader = new AudioFileReader(filePath); // NAudio handles various formats

                // Store reader for potential disposal or management
                // For simplicity, using filepath as key, but a unique track ID would be better
                _fileReaders[filePath] = audioFileReader;


                var volumeSampleProvider = new VolumeSampleProvider(audioFileReader.ToSampleProvider())
                {
                    Volume = volume
                };

                // Add to mixer. If looping, wrap it.
                // For proper looping, the LoopStream or a similar mechanism should be used before converting to SampleProvider
                // Or, handle loop by re-adding to mixer when PlaybackStopped event fires on a reader.
                // For now, a simple conceptual loop:
                if (loop)
                {
                    var loopStream = new LoopStream(audioFileReader); // LoopStream needs a WaveStream
                    _mixer.AddMixerInput(new WaveToSampleProvider(loopStream));
                }
                else
                {
                     _mixer.AddMixerInput(volumeSampleProvider);
                }


                // If playback task isn't running, start it.
                if (_playbackTask == null || _playbackTask.IsCompleted)
                {
                    _playbackCts?.Cancel(); // Cancel previous if any
                    _playbackCts = new CancellationTokenSource();
                    _playbackTask = Task.Run(() => NAudioToDiscordBridge.SendStreamAsync(_mixer.ToWaveProvider16(), _transmitSink, 20, _playbackCts.Token));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing file {filePath}: {ex.Message}");
                // Clean up the specific reader if it failed
                if (_fileReaders.TryRemove(filePath, out var reader))
                {
                    reader.Dispose();
                }
            }
        }

        public void StopPlayback()
        {
            _playbackCts?.Cancel();
            _playbackTask = null; // Allow it to be restarted

            // Clear all inputs from mixer and dispose readers
            _mixer.RemoveAllMixerInputs();
            foreach (var key in _fileReaders.Keys)
            {
                if (_fileReaders.TryRemove(key, out var reader))
                {
                    reader.Dispose();
                }
            }
        }

        // Placeholder for more advanced controls like removing a specific track or changing volume
        public void RemoveTrack(string filePath) // Ideally use a track ID
        {
            // This is complex with MixingSampleProvider as it doesn't directly expose inputs by ID.
            // Would need to re-create the mixer or use a more advanced mixing setup.
            // For now, this is a limitation. MultiLayerAudioPlayer will address this better.
            Console.WriteLine("RemoveTrack is not fully implemented for simple AudioPlayer. Use StopPlayback or wait for MultiLayerAudioPlayer.");
        }


        public void Dispose()
        {
            StopPlayback();
            _playbackCts?.Dispose();
            _transmitSink?.Dispose(); // Sink is from VoiceConnection, D#+ might manage its lifetime
            _voiceConnection = null; // Don't dispose, it's managed externally

            foreach (var readerEntry in _fileReaders)
            {
                readerEntry.Value.Dispose();
            }
            _fileReaders.Clear();

            // If using _outputDevices for local playback
            foreach (var deviceEntry in _outputDevices)
            {
                deviceEntry.Value.Dispose();
            }
            _outputDevices.Clear();
        }
    }

    // Helper class for looping WaveStream, NAudio doesn't have a built-in one that's easily integrated here.
    // This is a simplified version. A more robust one would handle seeking precisely.
    public class LoopStream : WaveStream
    {
        private readonly WaveStream _sourceStream;
        private bool _enableLooping;

        public LoopStream(WaveStream sourceStream, bool enableLooping = true)
        {
            _sourceStream = sourceStream;
            _enableLooping = enableLooping;
            this.WaveFormat = sourceStream.WaveFormat;
        }

        public bool EnableLooping
        {
            get => _enableLooping;
            set => _enableLooping = value;
        }

        public override WaveFormat WaveFormat { get; }

        public override long Length => _sourceStream.Length; // Can be problematic if true length is unknown or if we want infinite

        public override long Position
        {
            get => _sourceStream.Position;
            set => _sourceStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = _sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    if (_sourceStream.Position == 0 || !_enableLooping)
                    {
                        // Either end of stream and not looping, or an issue
                        break;
                    }
                    // Loop
                    _sourceStream.Position = 0;
                }
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sourceStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
