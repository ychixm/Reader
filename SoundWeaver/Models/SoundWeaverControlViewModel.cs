using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using SoundWeaver.Audio;
using SoundWeaver.Bot;
using SoundWeaver.Playlists;
using Utils;

namespace SoundWeaver.Models
{
    public class SoundWeaverControlViewModel : INotifyPropertyChanged, IDisposable
    {
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
        private SoundWeaverSettings _settings;

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



        private int _mixerSampleRate = 48000;
        public int MixerSampleRate
        {
            get => _mixerSampleRate;
            set { _mixerSampleRate = value; OnPropertyChanged(); }
        }
        private int _bitrateMin = 8_000;
        private int _bitrateMax = 96_000;
        private int _targetBitrate = 64_000;

        public int BitrateMin
        {
            get => _bitrateMin;
            private set { _bitrateMin = value; OnPropertyChanged(); }
        }
        public int BitrateMax
        {
            get => _bitrateMax;
            private set { _bitrateMax = value; OnPropertyChanged(); }
        }
        public int TargetBitrate
        {
            get => _targetBitrate;
            set
            {
                int clamped = Math.Clamp(value, BitrateMin, BitrateMax);
                if (_targetBitrate != clamped)
                {
                    _targetBitrate = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private int _resamplerQuality = 60;
        public int ResamplerQuality
        {
            get => _resamplerQuality;
            set { _resamplerQuality = Math.Clamp(value, 1, 60); OnPropertyChanged(); }
        }

        private ChannelBitrateSetting _currentChannelBitrateSetting;
        public ChannelBitrateSetting CurrentChannelBitrateSetting
        {
            get => _currentChannelBitrateSetting;
            set
            {
                _currentChannelBitrateSetting = value;
                BitrateMax = value?.DiscordBitrateCap ?? 96000;
                TargetBitrate = value?.Bitrate ?? 64000;
                OnPropertyChanged(nameof(CurrentChannelBitrateSetting));
                OnPropertyChanged(nameof(BitrateMax));
                OnPropertyChanged(nameof(TargetBitrate));
            }
        }
        public SoundWeaverControlViewModel()
        {
            _playlistManager = new PlaylistManager();

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

            _settings = AppSettingsService.LoadModuleSettings("SoundWeaver", () => new SoundWeaverSettings());
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
            try
            {
                _botService = new DiscordBotService();
                await _botService.InitializeAsync(DiscordToken);
                StatusMessage = "Bot initialized. Connecting to voice channel...";
                await _botService.JoinVoiceChannelAsync(GuildId, ChannelId);
                StatusMessage = $"Connected to voice channel {ChannelId} on guild {GuildId}.";

                var connection = _botService.GetConnection(GuildId);
                if (connection != null)
                {
                    var voiceChannel = connection.TargetChannel;
                    int discordCap = voiceChannel.Bitrate ?? 96000;
                    string channelName = voiceChannel.Name ?? $"Channel_{ChannelId}";

                    // --- Recherche ou cr�ation de la config pour ce salon ---
                    var chanList = _settings.ChannelBitrates ??= new List<ChannelBitrateSetting>();
                    var chanSetting = chanList.FirstOrDefault(x => x.ChannelId == ChannelId);
                    if (chanSetting == null)
                    {
                        chanSetting = new ChannelBitrateSetting
                        {
                            ChannelId = ChannelId,
                            ChannelName = channelName,
                            DiscordBitrateCap = discordCap,
                            Bitrate = Math.Min(64000, discordCap)
                        };
                        chanList.Add(chanSetting);
                    }
                    else
                    {
                        chanSetting.ChannelName = channelName;
                        if (chanSetting.DiscordBitrateCap != discordCap)
                        {
                            chanSetting.DiscordBitrateCap = discordCap;
                            if (chanSetting.Bitrate > discordCap)
                                chanSetting.Bitrate = discordCap;
                        }
                    }
                    // --- Enregistre et s�lectionne la config
                    AppSettingsService.SaveModuleSettings("SoundWeaver", _settings);

                    // --- Applique dynamiquement
                    UpdateBitrateBounds(chanSetting.DiscordBitrateCap, _settings.SelectedChannels);
                    TargetBitrate = chanSetting.Bitrate;

                    _audioPlayer = new MultiLayerAudioPlayer(
                                       connection,
                                       MixerSampleRate,
                                       _settings.SelectedChannels,
                                       ResamplerQuality);
                    StatusMessage += " AudioPlayer initialized.";
                    IsConnected = true;
                }
                else
                {
                    StatusMessage = "Failed to establish voice connection for AudioPlayer.";
                    IsConnected = false;
                }
            }
            catch (Exception ex)
            {
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
            try
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.Dispose();
                    _audioPlayer = null;
                }
                if (_botService != null)
                {
                    await _botService.ShutdownAsync();
                    _botService.Dispose();
                    _botService = null;
                }
                StatusMessage = "Disconnected.";
                IsConnected = false;
            }
            catch (Exception ex)
            {
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
            UpdateCommandStates();
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
            UpdateCommandStates();
        }

        private void OnCurrentPlaylistTrackEnded(object sender, EventArgs e)
        {
            if (sender is AudioLayer endedLayer)
            {
                endedLayer.PlaybackEnded -= OnCurrentPlaylistTrackEnded;
                ActiveLayers.Remove(endedLayer);

                if (ReferenceEquals(endedLayer, _currentPlaylistLayer))
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
            UpdateCommandStates();
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
            UpdateCommandStates();
        }

        private void OnLayerEndedUpdateList(object sender, EventArgs e)
        {
            if (sender is AudioLayer endedLayer)
            {
                endedLayer.PlaybackEnded -= OnLayerEndedUpdateList;
                System.Windows.Application.Current.Dispatcher.Invoke(() => ActiveLayers.Remove(endedLayer));
                UpdateCommandStates();
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
            StatusMessage = "Disposing resources...";
            _audioPlayer?.Dispose();
            _botService?.ShutdownAsync().Wait();
            StatusMessage = "Cleanup complete.";
        }

        /// <summary>
        /// Recalcule les bornes min?/?max � partir du cap Discord et du nombre de canaux choisis.
        /// </summary>
        private void UpdateBitrateBounds(int discordCapBps, int channelCount)
        {
            BitrateMax = discordCapBps;
            BitrateMin = 8_000 * channelCount;
            TargetBitrate = Math.Clamp(TargetBitrate, BitrateMin, BitrateMax);
        }

        private ChannelBitrateSetting GetOrCreateChannelSetting(ulong channelId, string channelName, int discordCap)
        {
            var setting = _settings.ChannelBitrates.FirstOrDefault(x => x.ChannelId == channelId);
            if (setting == null)
            {
                setting = new ChannelBitrateSetting
                {
                    ChannelId = channelId,
                    ChannelName = channelName,
                    DiscordBitrateCap = discordCap,
                    Bitrate = Math.Min(64000, discordCap) // Valeur initiale safe
                };
                _settings.ChannelBitrates.Add(setting);
                SaveSettings();
            }
            // MAJ du cap si besoin
            if (setting.DiscordBitrateCap != discordCap)
            {
                setting.DiscordBitrateCap = discordCap;
                if (setting.Bitrate > discordCap)
                    setting.Bitrate = discordCap;
                SaveSettings();
            }
            return setting;
        }

        // Pour sauver les modifs
        private void SaveSettings()
        {
            AppSettingsService.SaveModuleSettings("SoundWeaver", _settings);
        }
    }
}
