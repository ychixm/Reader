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
using Microsoft.Win32;

namespace SoundWeaver.UI
{
    public class SoundWeaverControlViewModel : INotifyPropertyChanged, IDisposable
    {
        private string _discordToken;
        private ulong _guildId;
        private ulong _channelId;
        private string _statusMessage;
        private string _playlistPath;
        private AudioTrack _selectedTrack;

        private DiscordBotService _botService;
        private MultiLayerAudioPlayer _audioPlayer;
        private PlaylistManager _playlistManager;

        private bool _isConnecting = false;

        public ObservableCollection<AudioTrack> CurrentPlaylistTracks { get; } = new ObservableCollection<AudioTrack>();
        public ObservableCollection<AudioLayer> ActiveLayers { get; } = new ObservableCollection<AudioLayer>();
        private Playlist _currentPlaylist;
        private AudioLayer _currentPlaylistLayer;

        public string DiscordToken
        {
            get => _discordToken;
            set { _discordToken = value; OnPropertyChanged(); }
        }

        public ulong GuildId
        {
            get => _guildId;
            set { _guildId = value; OnPropertyChanged(); }
        }

        public ulong ChannelId
        {
            get => _channelId;
            set { _channelId = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string PlaylistPath
        {
            get => _playlistPath;
            set { _playlistPath = value; OnPropertyChanged(); ((RelayCommand)LoadPlaylistCommand).CanExecute(null); }
        }

        public AudioTrack SelectedTrack
        {
            get => _selectedTrack;
            set { _selectedTrack = value; OnPropertyChanged(); ((RelayCommand)PlayTrackCommand).CanExecute(null); }
        }

        public ICommand ConnectBotCommand { get; }
        public ICommand LoadPlaylistCommand { get; }
        public ICommand PlayPlaylistCommand { get; }
        public ICommand StopAllAudioCommand { get; }
        public ICommand AddTrackAsLayerCommand { get; }
        public ICommand PlayTrackCommand { get; }
        public ICommand BrowsePlaylistCommand { get; }

        public SoundWeaverControlViewModel()
        {
            _playlistManager = new PlaylistManager();

            ConnectBotCommand = new RelayCommand(async _ => await ConnectBotAsync(), _ => !string.IsNullOrWhiteSpace(DiscordToken) && GuildId > 0 && ChannelId > 0 && !_isConnecting);
            LoadPlaylistCommand = new RelayCommand(async _ => await LoadPlaylistAsync(), _ => !string.IsNullOrWhiteSpace(PlaylistPath));
            PlayPlaylistCommand = new RelayCommand(async _ => await PlayPlaylistAsync(), _ => _currentPlaylist != null && _currentPlaylist.Tracks.Any() && _audioPlayer != null);
            StopAllAudioCommand = new RelayCommand(_ => StopAllAudio(), _ => _audioPlayer != null && ActiveLayers.Any());
            AddTrackAsLayerCommand = new RelayCommand(async _ => await AddSelectedTrackAsLayerAsync(), _ => SelectedTrack != null && _audioPlayer != null);
            PlayTrackCommand = new RelayCommand(async _ => await PlaySelectedTrackAsync(), _ => SelectedTrack != null && _audioPlayer != null);
            BrowsePlaylistCommand = new RelayCommand(_ => BrowseForPlaylist());

            CommandManager.InvalidateRequerySuggested();
        }

        private async Task ConnectBotAsync()
        {
            if (_isConnecting)
                return; // Empêche double clic

            _isConnecting = true;
            CommandManager.InvalidateRequerySuggested(); // Actualise l’état des boutons

            try
            {
                // Shutdown propre de l’ancienne instance s’il y en a une
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

                _botService = new DiscordBotService();
                StatusMessage = "Initializing bot...";
                await _botService.InitializeAsync(DiscordToken);
                StatusMessage = "Bot initialized. Connecting to voice channel...";

                // Tentative de connexion vocale robuste avec auto-reconnect hard si besoin
                bool connected = await _botService.HardResetAndReconnectAsync(DiscordToken, GuildId, ChannelId);
                if (!connected)
                {
                    StatusMessage = "Impossible de connecter le bot au vocal après plusieurs tentatives.";
                    return;
                }
                StatusMessage = $"Connected to voice channel {ChannelId} on guild {GuildId}.";

                // On récupère la connexion vocale DSharpPlus pour instancier le player
                var guild = await _botService.Client.GetGuildAsync(GuildId);
                if (guild == null)
                {
                    StatusMessage = $"Failed to retrieve guild {GuildId} after connecting.";
                    return;
                }
                var voiceConnection = _botService.Voice.GetConnection(guild);
                if (voiceConnection != null)
                {
                    _audioPlayer = new MultiLayerAudioPlayer(voiceConnection);
                    StatusMessage += " AudioPlayer initialized.";
                }
                else
                {
                    StatusMessage = "Failed to establish voice connection for AudioPlayer.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error connecting bot: {ex.Message}";
            }
            finally
            {
                _isConnecting = false;
                CommandManager.InvalidateRequerySuggested();
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
                return;
            }

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
                }
                else
                {
                    StatusMessage = "Playlist loaded but it's empty or failed to load.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading playlist: {ex.Message}";
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task PlayPlaylistAsync()
        {
            if (_currentPlaylist == null || !_currentPlaylist.Tracks.Any() || _audioPlayer == null)
            {
                StatusMessage = "Cannot play: No playlist loaded, playlist is empty, or audio player not ready.";
                return;
            }

            _audioPlayer.StopAllLayers();
            ActiveLayers.Clear();
            _currentPlaylist.Reset();

            PlayNextTrackInPlaylist();
            StatusMessage = $"Playing playlist: {_currentPlaylist.Name}";
        }

        private void PlayNextTrackInPlaylist()
        {
            if (_audioPlayer == null) return;

            var trackToPlay = _currentPlaylist.GetNextTrack();
            if (trackToPlay != null)
            {
                StatusMessage = $"Playing: {trackToPlay.Title}";
                _currentPlaylistLayer = _audioPlayer.AddLayer(trackToPlay, trackToPlay.IsLooping);
                if (_currentPlaylistLayer != null)
                {
                    ActiveLayers.Add(_currentPlaylistLayer);
                    _currentPlaylistLayer.PlaybackEnded += OnCurrentPlaylistTrackEnded;
                }
                else
                {
                    StatusMessage = $"Failed to play track: {trackToPlay.Title}. Skipping.";
                    PlayNextTrackInPlaylist();
                }
            }
            else
            {
                StatusMessage = $"Playlist '{_currentPlaylist.Name}' finished.";
                _currentPlaylistLayer = null;
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnCurrentPlaylistTrackEnded(object sender, EventArgs e)
        {
            if (sender is AudioLayer endedLayer)
            {
                endedLayer.PlaybackEnded -= OnCurrentPlaylistTrackEnded;
                ActiveLayers.Remove(endedLayer);

                if (object.ReferenceEquals(endedLayer, _currentPlaylistLayer))
                {
                    PlayNextTrackInPlaylist();
                }
            }
        }

        private async Task AddSelectedTrackAsLayerAsync()
        {
            if (SelectedTrack == null || _audioPlayer == null)
            {
                StatusMessage = "No track selected or audio player not ready.";
                return;
            }
            StatusMessage = $"Adding '{SelectedTrack.Title}' as a new layer.";
            var newLayer = _audioPlayer.AddLayer(SelectedTrack, SelectedTrack.IsLooping, SelectedTrack.Volume);
            if (newLayer != null)
            {
                ActiveLayers.Add(newLayer);
                newLayer.PlaybackEnded += OnLayerEndedUpdateList;
                StatusMessage = $"Layer '{SelectedTrack.Title}' added.";
            }
            else
            {
                StatusMessage = $"Failed to add layer '{SelectedTrack.Title}'.";
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task PlaySelectedTrackAsync()
        {
            if (SelectedTrack == null || _audioPlayer == null)
            {
                StatusMessage = "No track selected or audio player not ready.";
                return;
            }

            _audioPlayer.StopAllLayers();
            ActiveLayers.Clear();

            StatusMessage = $"Playing track: {SelectedTrack.Title}";
            _currentPlaylistLayer = _audioPlayer.AddLayer(SelectedTrack, SelectedTrack.IsLooping, SelectedTrack.Volume);
            if (_currentPlaylistLayer != null)
            {
                ActiveLayers.Add(_currentPlaylistLayer);
                _currentPlaylistLayer.PlaybackEnded += OnLayerEndedUpdateList;
                StatusMessage = $"Now playing: {SelectedTrack.Title}.";
            }
            else
            {
                StatusMessage = $"Failed to play track: {SelectedTrack.Title}.";
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnLayerEndedUpdateList(object sender, EventArgs e)
        {
            if (sender is AudioLayer endedLayer)
            {
                endedLayer.PlaybackEnded -= OnLayerEndedUpdateList;
                App.Current.Dispatcher.Invoke(() => ActiveLayers.Remove(endedLayer));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void StopAllAudio()
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.StopAllLayers();
                ActiveLayers.Clear();
                _currentPlaylistLayer = null;
                StatusMessage = "All audio stopped.";
            }
            CommandManager.InvalidateRequerySuggested();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            CommandManager.InvalidateRequerySuggested();
        }

        public void Dispose()
        {
            StatusMessage = "Disposing resources...";
            _audioPlayer?.Dispose();
            _botService?.ShutdownAsync().Wait();
            StatusMessage = "Cleanup complete.";
        }
    }
}
