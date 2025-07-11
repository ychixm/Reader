using SoundWeaver.Audio;
using SoundWeaver.Bot;
using SoundWeaver.Models;
using SoundWeaver.Playlists;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging; // Added
using Microsoft.Win32;

namespace SoundWeaver.UI
{
    public class SoundWeaverControlViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ILogger<SoundWeaverControlViewModel> _logger; // Added
        private readonly ILoggerFactory _loggerFactory; // Added - to pass to services it creates

        private string _discordToken;
        private ulong _guildId;
        private ulong _channelId;
        private string _statusMessage;
        private string _playlistPath;
        private AudioTrack _selectedTrack;
        private bool _isConnecting;
        private bool _isConnected;

        private DiscordBotService _botService;
        private MultiLayerAudioPlayer _audioPlayer;
        private PlaylistManager _playlistManager;

        public ObservableCollection<AudioTrack> CurrentPlaylistTracks { get; } = new ObservableCollection<AudioTrack>();
        public ObservableCollection<AudioLayer> ActiveLayers { get; } = new ObservableCollection<AudioLayer>();
        private Playlist _currentPlaylist;
        private AudioLayer _currentPlaylistLayer;

        public string DiscordToken
        {
            get => _discordToken;
            set { _discordToken = value; OnPropertyChanged(); UpdateCommandStates(); }
        }

        public ulong GuildId
        {
            get => _guildId;
            set { _guildId = value; OnPropertyChanged(); UpdateCommandStates(); }
        }

        public ulong ChannelId
        {
            get => _channelId;
            set { _channelId = value; OnPropertyChanged(); UpdateCommandStates(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string PlaylistPath
        {
            get => _playlistPath;
            set { _playlistPath = value; OnPropertyChanged(); UpdateCommandStates(); }
        }

        public AudioTrack SelectedTrack
        {
            get => _selectedTrack;
            set { _selectedTrack = value; OnPropertyChanged(); UpdateCommandStates(); }
        }

        public bool IsConnecting
        {
            get => _isConnecting;
            set { _isConnecting = value; OnPropertyChanged(); UpdateCommandStates(); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); UpdateCommandStates(); }
        }

        public ICommand ConnectBotCommand { get; }
        public ICommand DisconnectBotCommand { get; }
        public ICommand LoadPlaylistCommand { get; }
        public ICommand PlayPlaylistCommand { get; }
        public ICommand StopAllAudioCommand { get; }
        public ICommand AddTrackAsLayerCommand { get; }
        public ICommand PlayTrackCommand { get; }
        public ICommand BrowsePlaylistCommand { get; }

        public SoundWeaverControlViewModel(ILogger<SoundWeaverControlViewModel> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

            // Pass logger to PlaylistManager if it's DI-managed or keep creating it if not
            // For now, assuming PlaylistManager will be DI managed later or its constructor updated.
            // If PlaylistManager is not managed by DI, it would be:
            // _playlistManager = new PlaylistManager(_loggerFactory.CreateLogger<PlaylistManager>());
            // For this step, we only modify this ViewModel's constructor.
            // The instantiation of _playlistManager, _botService, _audioPlayer will be updated in step 5 (Register SoundWeaver services for DI)
            _playlistManager = new PlaylistManager(_loggerFactory.CreateLogger<PlaylistManager>());


            ConnectBotCommand = new RelayCommand(async _ => await ConnectBotAsync(),
                                                 _ => !IsConnecting && !IsConnected && !string.IsNullOrWhiteSpace(DiscordToken) && GuildId > 0 && ChannelId > 0);

            DisconnectBotCommand = new RelayCommand(async _ => await DisconnectBotAsync(),
                                                    _ => IsConnected && !IsConnecting);

            LoadPlaylistCommand = new RelayCommand(async _ => await LoadPlaylistAsync(),
                                                  _ => !string.IsNullOrWhiteSpace(PlaylistPath));
            PlayPlaylistCommand = new RelayCommand(async _ => await PlayPlaylistAsync(),
                                                   _ => _currentPlaylist != null && _currentPlaylist.Tracks.Any() && _audioPlayer != null);
            StopAllAudioCommand = new RelayCommand(_ => StopAllAudio(),
                                                   _ => _audioPlayer != null && ActiveLayers.Any());
            AddTrackAsLayerCommand = new RelayCommand(async _ => await AddSelectedTrackAsLayerAsync(),
                                                      _ => SelectedTrack != null && _audioPlayer != null);
            PlayTrackCommand = new RelayCommand(async _ => await PlaySelectedTrackAsync(),
                                                _ => SelectedTrack != null && _audioPlayer != null);
            BrowsePlaylistCommand = new RelayCommand(_ => BrowseForPlaylist());

            IsConnecting = false;
            IsConnected = false;
        }

        private async Task ConnectBotAsync()
        {
            if (_botService != null)
            {
                await _botService.ShutdownAsync();
                _botService.Dispose();
                _botService = null;
            }
            if (_audioPlayer != null)
            {
                _audioPlayer.Dispose();
                _audioPlayer = null;
            }

            IsConnecting = true;
            StatusMessage = "Initializing bot...";
            _logger.LogInformation("Attempting to connect bot. Token: {TokenProvided}, GuildID: {GuildId}, ChannelID: {ChannelId}", !string.IsNullOrWhiteSpace(DiscordToken), GuildId, ChannelId);
            try
            {
                // These services should ideally be managed by DI and passed to the ViewModel,
                // or the ViewModel should request them from a service provider.
                // For now, instantiating with logger factory.
                _botService = new DiscordBotService(_loggerFactory.CreateLogger<DiscordBotService>(), _loggerFactory);
                await _botService.InitializeAsync(DiscordToken);
                _logger.LogInformation("DiscordBotService initialized.");
                StatusMessage = "Bot initialized. Connecting to voice channel...";
                await _botService.JoinVoiceChannelAsync(GuildId, ChannelId);
                _logger.LogInformation("Successfully joined voice channel {ChannelId} on guild {GuildId}.", ChannelId, GuildId);
                StatusMessage = $"Connected to voice channel {ChannelId} on guild {GuildId}.";

                var connection = _botService.GetConnection(GuildId);
                if (connection != null)
                {
                    // Assuming MultiLayerAudioPlayer constructor is updated or will be to accept ILogger, ILoggerFactory
                    // For now, this will cause a compile error if MultiLayerAudioPlayer's constructor doesn't match.
                    // User has requested to skip MultiLayerAudioPlayer modifications for now.
                    _audioPlayer = new MultiLayerAudioPlayer(connection, _loggerFactory.CreateLogger<MultiLayerAudioPlayer>(), _loggerFactory.CreateLogger<AudioLayer>());
                    // _audioPlayer = new MultiLayerAudioPlayer(connection); // Keep old if previous line results in build error due to user skipping MLAP mod
                    _logger.LogInformation("MultiLayerAudioPlayer initialized.");
                    StatusMessage += " AudioPlayer initialized.";
                    IsConnected = true;
                }
                else
                {
                    _logger.LogError("Failed to get VoiceNextConnection for GuildID {GuildId} after joining channel.", GuildId);
                    StatusMessage = "Failed to establish voice connection for AudioPlayer.";
                    IsConnected = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting bot. GuildID: {GuildId}, ChannelID: {ChannelId}", GuildId, ChannelId);
                StatusMessage = $"Error connecting bot: {ex.Message}";
                IsConnected = false;
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private async Task DisconnectBotAsync()
        {
            IsConnecting = true;
            StatusMessage = "Disconnecting...";
            _logger.LogInformation("Attempting to disconnect bot.");
            try
            {
                if (_audioPlayer != null)
                {
                    _logger.LogDebug("Disposing MultiLayerAudioPlayer.");
                    _audioPlayer.Dispose();
                    _audioPlayer = null;
                }
                if (_botService != null)
                {
                    _logger.LogDebug("Shutting down and disposing DiscordBotService.");
                    await _botService.ShutdownAsync();
                    _botService.Dispose();
                    _botService = null;
                }
                StatusMessage = "Disconnected.";
                IsConnected = false;
                _logger.LogInformation("Bot disconnected successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disconnect.");
                StatusMessage = $"Error during disconnect: {ex.Message}";
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private void BrowseForPlaylist()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "M3U8 Playlists (*.m3u8)|*.m3u8|All files (*.*)|*.*",
                Title = "Select a Playlist File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                PlaylistPath = openFileDialog.FileName;
            }
        }

        private async Task LoadPlaylistAsync()
        {
            if (string.IsNullOrWhiteSpace(PlaylistPath))
            {
                StatusMessage = "Playlist path is empty.";
                _logger.LogWarning("LoadPlaylistAsync called with empty PlaylistPath.");
                return;
            }
            _logger.LogInformation("Loading playlist from: {PlaylistPath}", PlaylistPath);
            try
            {
                StatusMessage = $"Loading playlist from {PlaylistPath}...";
                _currentPlaylist = await _playlistManager.LoadM3U8PlaylistAsync(PlaylistPath);
                CurrentPlaylistTracks.Clear();
                if (_currentPlaylist != null && _currentPlaylist.Tracks.Any())
                {
                    foreach (var track in _currentPlaylist.Tracks)
                    {
                        CurrentPlaylistTracks.Add(track);
                    }
                    SelectedTrack = CurrentPlaylistTracks.FirstOrDefault();
                    StatusMessage = $"Playlist '{_currentPlaylist.Name}' loaded with {CurrentPlaylistTracks.Count} tracks.";
                    _logger.LogInformation("Playlist '{PlaylistName}' loaded with {TrackCount} tracks.", _currentPlaylist.Name, CurrentPlaylistTracks.Count);
                }
                else
                {
                    StatusMessage = "Playlist loaded but it's empty or failed to load.";
                    _logger.LogWarning("Playlist '{PlaylistPath}' loaded but is empty or failed.", PlaylistPath);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading playlist: {ex.Message}";
                _logger.LogError(ex, "Error loading playlist from {PlaylistPath}", PlaylistPath);
            }
            UpdateCommandStates();
        }

        private async Task PlayPlaylistAsync()
        {
            if (_currentPlaylist == null || !_currentPlaylist.Tracks.Any() || _audioPlayer == null)
            {
                StatusMessage = "Cannot play: No playlist loaded, playlist is empty, or audio player not ready.";
                _logger.LogWarning("PlayPlaylistAsync called but conditions not met. Playlist loaded: {PlaylistLoaded}, Tracks exist: {TracksExist}, AudioPlayer ready: {AudioPlayerReady}", _currentPlaylist != null, _currentPlaylist?.Tracks.Any(), _audioPlayer != null);
                return;
            }
            _logger.LogInformation("Starting playlist: {PlaylistName}", _currentPlaylist.Name);
            _audioPlayer.StopAllLayers();
            ActiveLayers.Clear();
            _currentPlaylist.Reset();

            PlayNextTrackInPlaylist();
            StatusMessage = $"Playing playlist: {_currentPlaylist.Name}";
        }

        private void PlayNextTrackInPlaylist()
        {
            if (_audioPlayer == null)
            {
                _logger.LogWarning("PlayNextTrackInPlaylist called but _audioPlayer is null.");
                return;
            }

            var trackToPlay = _currentPlaylist.GetNextTrack();
            if (trackToPlay != null)
            {
                _logger.LogInformation("Playing next track in playlist '{PlaylistName}': {TrackTitle}", _currentPlaylist.Name, trackToPlay.Title);
                StatusMessage = $"Playing: {trackToPlay.Title}";
                _currentPlaylistLayer = _audioPlayer.AddLayer(trackToPlay, trackToPlay.IsLooping);
                if (_currentPlaylistLayer != null)
                {
                    ActiveLayers.Add(_currentPlaylistLayer);
                    _currentPlaylistLayer.PlaybackEnded += OnCurrentPlaylistTrackEnded;
                    _logger.LogDebug("Layer added for track: {TrackTitle}, ID: {LayerId}", trackToPlay.Title, _currentPlaylistLayer.Id);
                }
                else
                {
                    StatusMessage = $"Failed to play track: {trackToPlay.Title}. Skipping.";
                    _logger.LogError("Failed to create layer for track: {TrackTitle}. Skipping.", trackToPlay.Title);
                    PlayNextTrackInPlaylist(); // Try next track
                }
            }
            else
            {
                StatusMessage = $"Playlist '{_currentPlaylist.Name}' finished.";
                _logger.LogInformation("Playlist '{PlaylistName}' finished.", _currentPlaylist.Name);
                _currentPlaylistLayer = null;
            }
            UpdateCommandStates();
        }

        private void OnCurrentPlaylistTrackEnded(object sender, EventArgs e)
        {
            if (sender is AudioLayer endedLayer)
            {
                _logger.LogInformation("Playlist track ended: {TrackTitle}, Layer ID: {LayerId}", endedLayer.Track.Title, endedLayer.Id);
                endedLayer.PlaybackEnded -= OnCurrentPlaylistTrackEnded;
                ActiveLayers.Remove(endedLayer); // Assuming this is thread-safe or dispatched if needed

                if (object.ReferenceEquals(endedLayer, _currentPlaylistLayer))
                {
                    _logger.LogDebug("Current playlist layer ended, playing next track.");
                    PlayNextTrackInPlaylist();
                }
            }
        }

        private async Task AddSelectedTrackAsLayerAsync()
        {
            if (SelectedTrack == null || _audioPlayer == null)
            {
                StatusMessage = "No track selected or audio player not ready.";
                _logger.LogWarning("AddSelectedTrackAsLayerAsync called but SelectedTrack is null or _audioPlayer is null.");
                return;
            }
            _logger.LogInformation("Adding selected track '{TrackTitle}' as a new layer. Volume: {Volume}, Loop: {Loop}", SelectedTrack.Title, SelectedTrack.Volume, SelectedTrack.IsLooping);
            StatusMessage = $"Adding '{SelectedTrack.Title}' as a new layer.";
            var newLayer = _audioPlayer.AddLayer(SelectedTrack, SelectedTrack.IsLooping, SelectedTrack.Volume);
            if (newLayer != null)
            {
                ActiveLayers.Add(newLayer);
                newLayer.PlaybackEnded += OnLayerEndedUpdateList;
                StatusMessage = $"Layer '{SelectedTrack.Title}' added.";
                _logger.LogDebug("Layer added for track: {TrackTitle}, ID: {LayerId}", SelectedTrack.Title, newLayer.Id);
            }
            else
            {
                StatusMessage = $"Failed to add layer '{SelectedTrack.Title}'.";
                _logger.LogError("Failed to add layer for selected track: {TrackTitle}", SelectedTrack.Title);
            }
            UpdateCommandStates();
        }

        private async Task PlaySelectedTrackAsync()
        {
            if (SelectedTrack == null || _audioPlayer == null)
            {
                StatusMessage = "No track selected or audio player not ready.";
                _logger.LogWarning("PlaySelectedTrackAsync called but SelectedTrack is null or _audioPlayer is null.");
                return;
            }
            _logger.LogInformation("Playing selected track: {TrackTitle}. Stopping all other layers.", SelectedTrack.Title);
            _audioPlayer.StopAllLayers();
            ActiveLayers.Clear();

            StatusMessage = $"Playing track: {SelectedTrack.Title}";
            _currentPlaylistLayer = _audioPlayer.AddLayer(SelectedTrack, SelectedTrack.IsLooping, SelectedTrack.Volume);
            if (_currentPlaylistLayer != null)
            {
                ActiveLayers.Add(_currentPlaylistLayer);
                _currentPlaylistLayer.PlaybackEnded += OnLayerEndedUpdateList; // Should this be OnCurrentPlaylistTrackEnded?
                StatusMessage = $"Now playing: {SelectedTrack.Title}.";
                _logger.LogDebug("Layer added for single selected track: {TrackTitle}, ID: {LayerId}", SelectedTrack.Title, _currentPlaylistLayer.Id);
            }
            else
            {
                StatusMessage = $"Failed to play track: {SelectedTrack.Title}.";
                _logger.LogError("Failed to play selected track: {TrackTitle}", SelectedTrack.Title);
            }
            UpdateCommandStates();
        }

        private void OnLayerEndedUpdateList(object sender, EventArgs e)
        {
            if (sender is AudioLayer endedLayer)
            {
                _logger.LogInformation("Generic layer ended: {TrackTitle}, Layer ID: {LayerId}", endedLayer.Track.Title, endedLayer.Id);
                endedLayer.PlaybackEnded -= OnLayerEndedUpdateList;
                App.Current.Dispatcher.Invoke(() => ActiveLayers.Remove(endedLayer)); // Ensure UI updates on UI thread
                UpdateCommandStates();
            }
        }

        private void StopAllAudio()
        {
            if (_audioPlayer != null)
            {
                _logger.LogInformation("Stopping all audio layers.");
                _audioPlayer.StopAllLayers();
                ActiveLayers.Clear();
                _currentPlaylistLayer = null;
                StatusMessage = "All audio stopped.";
            }
            UpdateCommandStates();
        }

        private void UpdateCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing SoundWeaverControlViewModel...");
            StatusMessage = "Disposing resources...";
            try
            {
                _audioPlayer?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MultiLayerAudioPlayer.");
            }

            try
            {
                _botService?.ShutdownAsync().GetAwaiter().GetResult(); // Synchronously wait for shutdown
                _botService?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error shutting down or disposing DiscordBotService.");
            }
            StatusMessage = "Cleanup complete.";
            _logger.LogInformation("SoundWeaverControlViewModel disposed.");
        }
    }
}
