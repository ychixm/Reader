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
