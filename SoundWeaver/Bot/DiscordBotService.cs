using DSharpPlus;
using DSharpPlus.VoiceNext;
using System; // Added for IDisposable
using System.Threading.Tasks;

namespace SoundWeaver.Bot
{
    public class DiscordBotService : IDisposable
    {
        public DiscordClient Client { get; private set; }
        public VoiceNextExtension Voice { get; private set; }
        private bool _disposed = false; // To detect redundant calls

        public async Task InitializeAsync(string token)
        {
            Client = new DiscordClient(new DiscordConfiguration
            {
                Token = token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.GuildVoiceStates
            });

            Voice = Client.UseVoiceNext();
            await Client.ConnectAsync();
        }

        public async Task JoinVoiceChannelAsync(ulong guildId, ulong channelId)
        {
            var guild = await Client.GetGuildAsync(guildId);
            if (guild == null)
            {
                // Log or handle guild not found
                return;
            }

            var channel = guild.GetChannel(channelId);
            if (channel == null || channel.Type != ChannelType.Voice)
            {
                // Log or handle channel not found or not a voice channel
                // You might want to throw an exception or log more specifically here
                System.Console.WriteLine($"Channel {channelId} not found or not a voice channel.");
                return;
            }

            // Check if already connected or connecting to this guild
            var vnc = Voice.GetConnection(guild);
            // The VoiceNextConnection object itself existing for the guild and pointing to the target channel
            // is a strong indicator. The IsConnected property might not be on VoiceNextConnection directly.
            // If vnc is not null and target channel matches, we assume it's either connected or connecting.
            // Attempting to connect again might be redundant or cause issues.
            // DSharpPlus often handles this by either returning the existing connection or updating it.
            // For simplicity, if vnc exists and matches channel, we'll assume it's fine.
            if (vnc != null && vnc.TargetChannel.Id == channelId)
            {
                // Already connected to this channel or connection attempt is in progress via this object.
                System.Console.WriteLine($"Already have a VNC for guild {guildId} targeting channel {channelId}.");
                return;
            }

            await channel.ConnectAsync();
        }

        public async Task DisconnectAsync(ulong guildId)
        {
            var guild = await Client.GetGuildAsync(guildId);
            if (guild == null)
            {
                return;
            }

            var vnc = Voice.GetConnection(guild); // Corrected method
            if (vnc != null)
            {
                vnc.Disconnect();
            }
        }

        public async Task ShutdownAsync()
        {
            await Client.DisconnectAsync();
            // Client.Dispose(); // Client will be disposed by the Dispose method
            Voice = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Dispose managed state (managed objects).
                if (Client != null)
                {
                    // It's good practice to ensure disconnection before disposal if not already handled by ShutdownAsync
                    // However, ShutdownAsync is usually called before Dispose in typical application flow.
                    // Client.DisconnectAsync().Wait(); // Blocking here can be problematic.
                    Client.Dispose();
                    Client = null;
                }
                // VoiceNextExtension itself might not be IDisposable, or its resources are tied to DiscordClient.
                Voice = null;
            }

            // Free unmanaged resources (unmanaged objects) and override a finalizer below.
            // Set large fields to null.
            _disposed = true;
        }

        // Override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DiscordBotService()
        // {
        //     Dispose(false);
        // }
    }
}
