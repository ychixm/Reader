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
// Assuming Microsoft.Win32.OpenFileDialog for WPF open file dialog
using Microsoft.Win32;


namespace SoundWeaver.UI
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private string _discordToken;
        private ulong _guildId;
        private ulong _channelId;
        private string _statusMessage;
        private string _playlistPath;
        private AudioTrack _selectedTrack; // For potential individual track operations

        private DiscordBotService _botService;
        private MultiLayerAudioPlayer _audioPlayer;
        private PlaylistManager _playlistManager;

        public ObservableCollection<AudioTrack> CurrentPlaylistTracks { get; } = new ObservableCollection<AudioTrack>();
        public ObservableCollection<AudioLayer> ActiveLayers { get; } = new ObservableCollection<AudioLayer>(); // To display active layers
        private Playlist _currentPlaylist;
        private AudioLayer _currentPlaylistLayer; // If playing playlist as a single layer progression

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
            set { _playlistPath = value; OnPropertyChanged(); ((RelayCommand)LoadPlaylistCommand).CanExecute(null); } // Update CanExecute
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
        public ICommand AddTrackAsLayerCommand { get; } // For adding individual files as layers
        public ICommand PlayTrackCommand { get; } // Plays selected track from playlist, potentially as a new primary layer
        public ICommand BrowsePlaylistCommand { get; }


        public MainViewModel()
        {
            _playlistManager = new PlaylistManager();
            // Initialize commands
            ConnectBotCommand = new RelayCommand(async _ => await ConnectBotAsync(), _ => !string.IsNullOrWhiteSpace(DiscordToken) && GuildId > 0 && ChannelId > 0);
            LoadPlaylistCommand = new RelayCommand(async _ => await LoadPlaylistAsync(), _ => !string.IsNullOrWhiteSpace(PlaylistPath));
            PlayPlaylistCommand = new RelayCommand(async _ => await PlayPlaylistAsync(), _ => _currentPlaylist != null && _currentPlaylist.Tracks.Any() && _audioPlayer != null);
            StopAllAudioCommand = new RelayCommand(_ => StopAllAudio(), _ => _audioPlayer != null && ActiveLayers.Any());
            AddTrackAsLayerCommand = new RelayCommand(async _ => await AddSelectedTrackAsLayerAsync(), _ => SelectedTrack != null && _audioPlayer != null);
            PlayTrackCommand = new RelayCommand(async _ => await PlaySelectedTrackAsync(), _ => SelectedTrack != null && _audioPlayer != null);
            BrowsePlaylistCommand = new RelayCommand(_ => BrowseForPlaylist());

            // Default values for testing if needed (remove for production)
            // DiscordToken = "YOUR_BOT_TOKEN";
            // GuildId = 123456789012345678;
            // ChannelId = 123456789012345679;
        }

        private async Task ConnectBotAsync()
        {
            if (_botService != null)
            {
                await _botService.ShutdownAsync();
                _botService.Dispose(); // Assuming DiscordBotService is IDisposable
                 _botService = null;
            }
            if (_audioPlayer != null)
            {
                _audioPlayer.Dispose();
                _audioPlayer = null;
            }

            _botService = new DiscordBotService();
            try
            {
                StatusMessage = "Initializing bot...";
                await _botService.InitializeAsync(DiscordToken);
                StatusMessage = "Bot initialized. Connecting to voice channel...";
                await _botService.JoinVoiceChannelAsync(GuildId, ChannelId);
                StatusMessage = $"Connected to voice channel {ChannelId} on guild {GuildId}.";

                // After joining, get the guild object then the connection
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
            CommandManager.InvalidateRequerySuggested();
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

            _audioPlayer.StopAllLayers(); // Stop current audio before playing new playlist
            ActiveLayers.Clear();
            _currentPlaylist.Reset(); // Start playlist from the beginning

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
                _currentPlaylistLayer = _audioPlayer.AddLayer(trackToPlay, trackToPlay.IsLooping); // Use track's own loop setting for playlist items
                if (_currentPlaylistLayer != null)
                {
                    ActiveLayers.Add(_currentPlaylistLayer); // For UI display
                    _currentPlaylistLayer.PlaybackEnded += OnCurrentPlaylistTrackEnded;
                }
                else
                {
                    StatusMessage = $"Failed to play track: {trackToPlay.Title}. Skipping.";
                    PlayNextTrackInPlaylist(); // Try next track
                }
            }
            else
            {
                StatusMessage = $"Playlist '{_currentPlaylist.Name}' finished.";
                 _currentPlaylistLayer = null;
                // If playlist itself is looping, it would have been handled by GetNextTrack()
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnCurrentPlaylistTrackEnded(object sender, EventArgs e)
        {
            if (sender is AudioLayer endedLayer)
            {
                endedLayer.PlaybackEnded -= OnCurrentPlaylistTrackEnded; // Unsubscribe
                ActiveLayers.Remove(endedLayer); // Remove from UI display
                // Note: MultiLayerAudioPlayer automatically removes the layer from mixer on end if not looping.

                if (object.ReferenceEquals(endedLayer, _currentPlaylistLayer)) // Ensure it's the main playlist track
                {
                     PlayNextTrackInPlaylist(); // Play next
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
                newLayer.PlaybackEnded += OnLayerEndedUpdateList; // For UI cleanup
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

            // Stop current playlist layer if it exists, or all layers if preferred.
            // For this example, let's assume "PlayTrack" means it becomes the primary sound.
            _audioPlayer.StopAllLayers();
            ActiveLayers.Clear();

            StatusMessage = $"Playing track: {SelectedTrack.Title}";
            _currentPlaylistLayer = _audioPlayer.AddLayer(SelectedTrack, SelectedTrack.IsLooping, SelectedTrack.Volume);
            if (_currentPlaylistLayer != null)
            {
                ActiveLayers.Add(_currentPlaylistLayer);
                // If this is meant to be a one-off play, don't hook playlist advancement.
                // If it's part of "previewing" from a playlist, you might not advance the main playlist index.
                // For now, treat as a single track play.
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
                // Run on UI thread if needed for ObservableCollection
                App.Current.Dispatcher.Invoke(() => ActiveLayers.Remove(endedLayer));
                CommandManager.InvalidateRequerySuggested();
            }
        }


        private void StopAllAudio()
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.StopAllLayers();
                ActiveLayers.Clear(); // Clear UI display
                _currentPlaylistLayer = null; // Reset playlist layer
                StatusMessage = "All audio stopped.";
            }
            CommandManager.InvalidateRequerySuggested();
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // Update CanExecute for commands that depend on properties
            // This is a common way, or each property setter can be more specific
            CommandManager.InvalidateRequerySuggested();
        }

        public void Dispose()
        {
            StatusMessage = "Disposing resources...";
            _audioPlayer?.Dispose();
            _botService?.ShutdownAsync().Wait(); // Ensure bot is shut down
            // _botService?.Dispose(); // If IDisposable
            StatusMessage = "Cleanup complete.";
        }
    }
}
