using DSharpPlus;
using DSharpPlus.VoiceNext;
using System.Threading.Tasks;

namespace SoundWeaver.Bot
{
    public class DiscordBotService
    {
        public DiscordClient Client { get; private set; }
        public VoiceNextExtension Voice { get; private set; }

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
            var vnc = Voice.GetConnection(guild); // Corrected method
            if (vnc != null && vnc.TargetChannel.Id == channelId && vnc.IsConnected)
            {
                // Already connected to this channel
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
            Client.Dispose();
            Voice = null; // Or Voice.Dispose() if available and needed
        }
    }
}
