using DSharpPlus;
using DSharpPlus.VoiceNext;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace SoundWeaver.Bot
{
    public class DiscordBotService : IDisposable
    {
        private DiscordClient _client;
        private VoiceNextExtension _voice;
        private readonly Dictionary<ulong, VoiceNextConnection> _voiceConnections = new();
        private readonly ILogger<DiscordBotService> _logger;
        private readonly ILoggerFactory _loggerFactory;

        private bool _isConnectingOrDisconnecting = false;
        private bool _disposed = false;

        public DiscordClient Client => _client; // Expose to ViewModel if needed
        public VoiceNextExtension Voice => _voice;

        public DiscordBotService()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            _logger = _loggerFactory.CreateLogger<DiscordBotService>();
        }

        public async Task InitializeAsync(string token)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DiscordBotService));
            if (_isConnectingOrDisconnecting) throw new InvalidOperationException("Already connecting or disconnecting");

            _isConnectingOrDisconnecting = true;
            try
            {
                _logger.LogInformation("Initialisation du bot Discord...");
                _client = new DiscordClient(new DiscordConfiguration
                {
                    Token = token,
                    TokenType = TokenType.Bot,
                    Intents = DiscordIntents.Guilds | DiscordIntents.GuildVoiceStates,
                    LoggerFactory = _loggerFactory
                });

                _voice = _client.UseVoiceNext();

                // Events pour traçabilité/debug
                _client.Ready += (s, e) =>
                {
                    _logger.LogInformation("Bot Discord READY et connecté à l'API Discord.");
                    return Task.CompletedTask;
                };
                _client.GuildAvailable += (s, e) =>
                {
                    _logger.LogInformation("GuildAvailable: {GuildName}", e.Guild.Name);
                    return Task.CompletedTask;
                };
                _client.SocketClosed += (s, e) =>
                {
                    _logger.LogWarning("Socket Discord fermé ({Code}) : {Reason}", e.CloseCode, e.CloseMessage);
                    return Task.CompletedTask;
                };
                _client.ClientErrored += (s, e) =>
                {
                    _logger.LogError(e.Exception, "Erreur client Discord");
                    return Task.CompletedTask;
                };

                await _client.ConnectAsync();

                _logger.LogInformation("Bot Discord connecté avec succès.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'initialisation du bot Discord.");
                throw;
            }
            finally
            {
                _isConnectingOrDisconnecting = false;
            }
        }

        public async Task JoinVoiceChannelAsync(ulong guildId, ulong channelId)
        {
            var guild = await _client.GetGuildAsync(guildId);
            if (guild == null) throw new Exception($"Guild {guildId} introuvable.");

            var channel = guild.GetChannel(channelId);
            if (channel == null)
                throw new Exception($"Channel {channelId} introuvable sur {guild.Name}.");

            var existingConn = _voice.GetConnection(guild);
            if (existingConn != null)
            {
                _logger.LogInformation("Déconnexion de la session vocale précédente...");
                try
                {
                    existingConn.Disconnect();
                    await Task.Delay(1000); // 1 seconde de marge, indispensable pour Discord/VoiceNext
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erreur lors de la déconnexion de la session vocale précédente.");
                }

            }

            _logger.LogInformation("Avant await _voice.ConnectAsync (timeout 10s)");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var connectionTask = _voice.ConnectAsync(channel);

            var completedTask = await Task.WhenAny(connectionTask, Task.Delay(Timeout.Infinite, cts.Token));
            if (completedTask != connectionTask)
            {
                _logger.LogError("Timeout: la connexion vocale Discord n'a pas abouti sous 10s !");
                throw new TimeoutException("Timeout lors de la connexion vocale Discord !");
            }

            var connection = await connectionTask;
            _voiceConnections[guildId] = connection;

            _logger.LogInformation("Connecté au salon vocal {ChannelName} ({ChannelId}) sur le serveur {GuildName} ({GuildId}).",
                channel.Name, channelId, guild.Name, guildId);
        }
        public VoiceNextConnection GetConnection(ulong guildId)
        {
            _voiceConnections.TryGetValue(guildId, out var connection);
            return connection;
        }

        public async Task ShutdownAsync()
        {
            if (_isConnectingOrDisconnecting) return;

            _isConnectingOrDisconnecting = true;
            try
            {
                _logger.LogWarning("Tentative de shutdown FULL (hard) du bot Discord...");

                foreach (var connection in _voiceConnections.Values)
                {
                    try { connection.Disconnect(); } catch { /* ignore */ }
                }
                _voiceConnections.Clear();

                if (_client != null)
                {
                    try { await _client.DisconnectAsync(); } catch { /* ignore */ }
                    try { _client.Dispose(); } catch { /* ignore */ }
                    _client = null;
                }

                _logger.LogWarning("Shutdown FULL terminé.");
            }
            finally
            {
                _isConnectingOrDisconnecting = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _client?.Dispose(); } catch { /* ignore */ }
        }
    }
}
