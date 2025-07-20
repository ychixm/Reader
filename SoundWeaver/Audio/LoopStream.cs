using NAudio.Wave;

namespace SoundWeaver.Audio
{
    public class LoopStream : WaveStream
    {
        private readonly WaveStream _src;
        private bool _loop;

        public LoopStream(WaveStream source, bool enableLoop = true)
        {
            _src  = source;
            _loop = enableLoop;
        }

        public bool EnableLooping
        {
            get => _loop;
            set => _loop = value;
        }

        public override WaveFormat WaveFormat => _src.WaveFormat;
        public override long Length          => _src.Length;

        public override long Position
        {
            get => _src.Position;
            set => _src.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = _src.Read(buffer, offset + total, count - total);
                if (read == 0)
                {
                    if (!_loop) break;
                    _src.Position = 0;           // restart
                    continue;
                }
                total += read;
            }
            return total;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _src.Dispose();
            base.Dispose(disposing);
        }
    }
}
