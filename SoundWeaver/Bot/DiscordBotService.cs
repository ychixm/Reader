using DSharpPlus;
using DSharpPlus.VoiceNext;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SoundWeaver.Bot
{
    public class DiscordBotService : IDisposable
    {
        private DiscordClient _client;
        private VoiceNextExtension _voice;
        private readonly Dictionary<ulong, VoiceNextConnection> _voiceConnections = new();
        private readonly ILogger<DiscordBotService> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public DiscordClient Client => _client; // Pour exposer au ViewModel
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

                await _client.ConnectAsync();

                _logger.LogInformation("Bot Discord connecté avec succès.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'initialisation du bot Discord.");
                throw;
            }
        }

        public async Task JoinVoiceChannelAsync(ulong guildId, ulong channelId)
        {
            try
            {
                _logger.LogInformation("JoinVoiceChannelAsync démarré pour guild {GuildId}, channel {ChannelId}", guildId, channelId);

                var guild = await _client.GetGuildAsync(guildId);
                _logger.LogInformation("Après await GetGuildAsync : {GuildName}", guild?.Name ?? "null");

                var channel = guild?.GetChannel(channelId);
                _logger.LogInformation("Après récupération du salon vocal : {ChannelName}", channel?.Name ?? "null");

                if (channel == null)
                {
                    _logger.LogWarning("Salon vocal {ChannelId} introuvable sur le serveur {GuildId}.", channelId, guildId);
                    throw new Exception("Salon vocal introuvable.");
                }

                // Ajout d'un timeout (10s) pour éviter le lock
                _logger.LogInformation("Avant await _voice.ConnectAsync (timeout 10s)");
                var connectTask = _voice.ConnectAsync(channel);
                if (await Task.WhenAny(connectTask, Task.Delay(10000)) == connectTask)
                {
                    var connection = connectTask.Result;
                    _voiceConnections[guildId] = connection;
                    _logger.LogInformation("Connecté au salon vocal {ChannelName} ({ChannelId}) sur le serveur {GuildName} ({GuildId}).",
                        channel.Name, channelId, guild.Name, guildId);
                }
                else
                {
                    _logger.LogError("Timeout: la connexion vocale Discord n'a pas abouti sous 10s !");
                    throw new TimeoutException("Timeout lors de la connexion vocale Discord !");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception dans JoinVoiceChannelAsync (guild {GuildId} / channel {ChannelId})", guildId, channelId);
                throw;
            }
        }

        public VoiceNextConnection GetConnection(ulong guildId)
        {
            _voiceConnections.TryGetValue(guildId, out var connection);
            return connection;
        }

        public async Task ShutdownAsync()
        {
            try
            {
                _logger.LogWarning("Tentative de shutdown FULL (hard) du bot Discord...");

                foreach (var connection in _voiceConnections.Values)
                {
                    try
                    {
                        connection.Disconnect();
                    }
                    catch { /* ignorer erreurs ici */ }
                }
                _voiceConnections.Clear();

                if (_client != null)
                {
                    await _client.DisconnectAsync();
                    _client.Dispose();
                    _client = null;
                }
                _logger.LogWarning("Shutdown FULL terminé.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'arrêt du bot Discord.");
                throw;
            }
        }

        /// <summary>
        /// Tente un hard reset + reconnexion jusqu'à maxAttempts, retourne true si succès, false sinon.
        /// </summary>
        public async Task<bool> HardResetAndReconnectAsync(string token, ulong guildId, ulong channelId, int maxAttempts = 3, int delayBetweenTriesMs = 2000)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await ShutdownAsync();
                    await Task.Delay(1000);
                    await InitializeAsync(token);
                    await JoinVoiceChannelAsync(guildId, channelId);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HardResetAndReconnect] Tentative {Attempt} échouée.", attempt);
                    if (attempt == maxAttempts)
                        return false;
                    await Task.Delay(delayBetweenTriesMs);
                }
            }
            return false;
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
