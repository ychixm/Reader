using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SoundWeaver.Bot
{
    public class DiscordBotService : IAsyncDisposable
    {
        private readonly ILogger<DiscordBotService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly ConcurrentDictionary<ulong, IAudioClient> _voiceClients = new();

        private bool _disposed = false;
        private bool _isConnecting = false;
        private DateTime _lastDisconnect = DateTime.MinValue;

        private const int _minReconnectDelayMs = 10_000;
        private const int _maxRetries = 5;
        private int _reconnectTries = 0;
        public ILogger Logger => _logger;
        public DiscordSocketClient Client => _client;

        public DiscordBotService(ILogger<DiscordBotService> logger = null)
        {
            _logger = logger ?? LoggerFactory.Create(b => b.AddConsole())
                                .CreateLogger<DiscordBotService>();
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
                LogGatewayIntentWarnings = false
            });

            _client.Log += msg =>
            {
                _logger.Log(msg.Severity switch
                {
                    LogSeverity.Critical => LogLevel.Critical,
                    LogSeverity.Error => LogLevel.Error,
                    LogSeverity.Warning => LogLevel.Warning,
                    LogSeverity.Info => LogLevel.Information,
                    LogSeverity.Verbose => LogLevel.Debug,
                    _ => LogLevel.Trace
                }, msg.Exception, "[Discord.NET] " + msg.Message);
                return Task.CompletedTask;
            };
            _client.Ready += () =>
            {
                _logger.LogInformation("[Discord.NET] Event READY reçu. Latence: " + _client.Latency + "ms");
                return Task.CompletedTask;
            };
            _client.Connected += () =>
            {
                _logger.LogInformation("[Discord.NET] Event CONNECTED.");
                return Task.CompletedTask;
            };
            _client.Disconnected += (ex) =>
            {
                _logger.LogWarning(ex, "[Discord.NET] Event DISCONNECTED.");
                return Task.CompletedTask;
            };
            _client.LatencyUpdated += (old, now) =>
            {
                _logger.LogInformation($"[Discord.NET] Latence gateway: {now} ms (avant: {old} ms)");
                return Task.CompletedTask;
            };
        }

        private async Task OnDisconnected(Exception ex)
        {
            _logger.LogWarning(ex, "Déconnecté de Discord. Attente puis tentative de reconnexion...");
            _lastDisconnect = DateTime.UtcNow;
            _reconnectTries++;

            if (_reconnectTries > _maxRetries)
            {
                _logger.LogCritical("Trop de tentatives de reconnexion. Arrêt du bot.");
                return;
            }

            await Task.Delay(_minReconnectDelayMs * _reconnectTries);
            try
            {
                await _client.StartAsync();
                _logger.LogInformation("Reconnexion Discord.NET réussie.");
                _reconnectTries = 0;
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Erreur de reconnexion Discord.NET (tentative {0})", _reconnectTries);
                await OnDisconnected(err); // Retry récursif avec délai croissant
            }
        }

        public async Task InitializeAsync(string token)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DiscordBotService));
            if (_isConnecting) throw new InvalidOperationException("Déjà en cours de connexion.");

            _isConnecting = true;
            try
            {
                _logger.LogInformation("Initialisation du bot Discord.NET…");
                await _client.LoginAsync(TokenType.Bot, token);

                // Respecte délai si bot relancé souvent
                var sinceLast = DateTime.UtcNow - _lastDisconnect;
                if (sinceLast < TimeSpan.FromMilliseconds(_minReconnectDelayMs))
                    await Task.Delay(TimeSpan.FromMilliseconds(_minReconnectDelayMs) - sinceLast);

                await _client.StartAsync();

                var readyTcs = new TaskCompletionSource<bool>();
                Task OnReady() { readyTcs.TrySetResult(true); return Task.CompletedTask; }
                _client.Ready += OnReady;

                if (await Task.WhenAny(readyTcs.Task, Task.Delay(15000)) != readyTcs.Task)
                    throw new TimeoutException("Discord READY non reçu (<15 s)");

                _client.Ready -= OnReady;
                _logger.LogInformation("Bot Discord prêt.");
                _reconnectTries = 0;
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public async Task<IAudioClient> JoinVoiceChannelAsync(ulong guildId, ulong channelId,
                                                              bool selfDeaf = false, bool selfMute = false,
                                                              int timeoutSec = 10)
        {
            var guild = _client.GetGuild(guildId) ?? throw new Exception($"Guild {guildId} introuvable.");
            var channel = guild.GetVoiceChannel(channelId) ?? throw new Exception($"Salon {channelId} introuvable.");

            // Déconnexion vocale existante
            if (_voiceClients.TryRemove(guildId, out var oldClient))
            {
                _logger.LogInformation("Déconnexion de la session vocale précédente…");
                try { await oldClient.StopAsync(); } catch { }
                await Task.Delay(1000);
            }

            _logger.LogInformation("Connexion voix → {0}/{1} (timeout {2}s)…", guild.Name, channel.Name, timeoutSec);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
            var joinTask = channel.ConnectAsync(selfDeaf, selfMute);

            if (await Task.WhenAny(joinTask, Task.Delay(-1, cts.Token)) != joinTask)
                throw new TimeoutException("Timeout lors de la connexion vocale Discord.NET");

            var audioClient = await joinTask;
            _voiceClients[guildId] = audioClient;

            _logger.LogInformation("Connecté au salon vocal {0} sur {1}.", channel.Name, guild.Name);
            return audioClient;
        }

        public async Task LeaveGuildVoiceAsync(ulong guildId)
        {
            if (_voiceClients.TryRemove(guildId, out var client))
            {
                try { await client.StopAsync(); } catch { }
                _logger.LogInformation("Déconnecté du vocal sur guild {0}.", guildId);
            }
        }

        public async Task ShutdownAsync()
        {
            foreach (var kvp in _voiceClients)
            {
                try { await kvp.Value.StopAsync(); } catch { }
            }
            _voiceClients.Clear();

            if (_client.LoginState != LoginState.LoggedOut)
            {
                await _client.StopAsync();
                await _client.LogoutAsync();
            }
            _lastDisconnect = DateTime.UtcNow;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            try { await ShutdownAsync(); } catch { }
            _client.Dispose();
        }

        public static async Task<bool> PingDiscordGatewayAsync(ILogger logger = null)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                var resp = await client.GetAsync("https://discord.com/api/v10/gateway");
                var body = await resp.Content.ReadAsStringAsync();
                logger?.LogInformation($"[PingDiscordGatewayAsync] StatusCode: {resp.StatusCode} Body: {body}");
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[PingDiscordGatewayAsync] Exception lors du ping gateway.");
                return false;
            }
        }

        public static async Task<bool> TestDiscordTokenAsync(string token, ILogger logger = null)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", token);
                var resp = await client.GetAsync("https://discord.com/api/v10/users/@me");
                var body = await resp.Content.ReadAsStringAsync();
                logger?.LogInformation($"[TestDiscordTokenAsync] StatusCode: {resp.StatusCode} Body: {body}");
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[TestDiscordTokenAsync] Exception lors du test du token.");
                return false;
            }
        }


    }
}
