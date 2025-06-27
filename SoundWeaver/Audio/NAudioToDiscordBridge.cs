using DSharpPlus.VoiceNext;
using NAudio.Wave;
using System.Threading.Tasks;

namespace SoundWeaver.Audio
{
    public static class NAudioToDiscordBridge
    {
        /// <summary>
        /// Sends an NAudio IWaveProvider stream to a Discord VoiceTransmitSink.
        /// </summary>
        /// <param name="waveProvider">The NAudio wave provider to stream from.</param>
        /// <param name="transmitSink">The Discord voice transmit sink.</param>
        /// <param name="bufferSizeMs">Buffer size in milliseconds. Default is 20ms, matching Discord's typical frame size.</param>
        /// <param name="cancellationToken">Cancellation token to stop streaming.</param>
        public static async Task SendStreamAsync(
            IWaveProvider waveProvider,
            VoiceTransmitSink transmitSink,
            int bufferSizeMs = 20,
            System.Threading.CancellationToken cancellationToken = default)
        {
            // We need to convert to PCM 16-bit stereo 48kHz for Discord if it's not already
            // However, VoiceNext typically handles the Opus encoding.
            // The sink expects PCM data. NAudio's WaveFormat needs to match what DSharpPlus expects.
            // DSharpPlus VoiceNext typically works with 48kHz, 2 channels (stereo), 16-bit PCM.
            // We should ensure our input stream is converted to this format.

            var outputFormat = new WaveFormat(48000, 16, 2); // 48kHz, 16-bit, Stereo
            var resamplingProvider = waveProvider.WaveFormat.SampleRate == outputFormat.SampleRate &&
                                     waveProvider.WaveFormat.Channels == outputFormat.Channels &&
                                     waveProvider.WaveFormat.BitsPerSample == outputFormat.BitsPerSample
                ? null // No resampling needed if formats match (though bits per sample might still differ for float vs pcm16)
                : new MediaFoundationResampler(waveProvider, outputFormat) { ResamplerQuality = 60 };

            var finalProvider = resamplingProvider ?? waveProvider;

            // If the input is float, convert to PCM 16-bit
            ISampleProvider sampleProvider;
            if (finalProvider is IWaveProvider wf && wf.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                sampleProvider = new WaveToSampleProvider(finalProvider);
            }
            else if (finalProvider is ISampleProvider sp)
            {
                sampleProvider = sp;
            }
            else // Assuming PCM if not float, needs conversion to ISampleProvider
            {
                 sampleProvider = finalProvider.ToSampleProvider();
            }

            // Ensure stereo if not already (D#+ VoiceNext prefers stereo)
            if (sampleProvider.WaveFormat.Channels == 1)
            {
                sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
            }

            // Convert to 16-bit PCM if it's float
            var pcm16Provider = sampleProvider.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat ?
                                new SampleToWaveProvider16(sampleProvider) :
                                (sampleProvider as IWaveProvider) ?? sampleProvider.ToWaveProvider();


            // Calculate buffer size based on milliseconds and target format
            // Bytes per millisecond = (SampleRate * Channels * BitsPerSample / 8) / 1000
            int bytesPerMillisecond = (outputFormat.SampleRate * outputFormat.Channels * (outputFormat.BitsPerSample / 8)) / 1000;
            int bufferSize = bytesPerMillisecond * bufferSizeMs;
            var buffer = new byte[bufferSize];
            int bytesRead;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bytesRead = pcm16Provider.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        // End of stream
                        break;
                    }

                    await transmitSink.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }
            }
            finally
            {
                resamplingProvider?.Dispose();
                // transmitSink is managed by VoiceNextConnection, should not be disposed here.
            }
        }
    }
}
