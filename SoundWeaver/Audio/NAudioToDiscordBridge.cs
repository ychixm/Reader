using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Discord.Audio;
using NAudio.Wave;

namespace SoundWeaver.Audio
{
    /// <summary>
    /// Convertit un IWaveProvider (PCM 16-bit 48 kHz stéréo) en paquets 20 ms
    /// et l’envoie dans un <see cref="AudioOutStream"/> Discord.NET.
    /// </summary>
    public static class NAudioToDiscordBridge
    {
        public static async Task SendPcmAsync(
            IWaveProvider waveProvider,
            AudioOutStream discordStream,
            int frameMs = 20,
            CancellationToken ct = default)
        {
            var wf = waveProvider.WaveFormat;
            if (wf.SampleRate != 48000 || wf.Channels != 2 || wf.BitsPerSample != 16)
                throw new InvalidDataException("Input must be 48 kHz 16-bit stereo PCM.");

            int bytesPerMs = wf.AverageBytesPerSecond / 1000;
            int frameBytes = bytesPerMs * frameMs;
            byte[] buffer = new byte[frameBytes];

            while (!ct.IsCancellationRequested)
            {
                int read = waveProvider.Read(buffer, 0, frameBytes);
                if (read == 0) break;          // fin de stream

                await discordStream.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
    }
}
