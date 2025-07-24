using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SoundWeaver.Audio;
using SoundWeaver.Bot;
using SoundWeaver.Playlists;
using SoundWeaver.ViewModels;
using Utils;

namespace SoundWeaver.Models
{
    public class SoundWeaverControlViewModel : INotifyPropertyChanged, IDisposable
    {
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

        public ObservableCollection<PlaylistElementViewModel> Playlists { get; } = new ObservableCollection<PlaylistElementViewModel>();
        public ICommand AddPlaylistCommand { get; }
        public ICommand LoadPlaylistElementCommand { get; }
        public ICommand PlayPlaylistElementCommand { get; }

        public ObservableCollection<AudioTrack> CurrentPlaylistTracks { get; } = new ObservableCollection<AudioTrack>();
        public ObservableCollection<AudioLayer> ActiveLayers { get; } = new ObservableCollection<AudioLayer>();
        private Playlist _currentPlaylist;
        private AudioLayer _currentPlaylistLayer;

        private ChannelSetting _selectedChannelSetting;
        public ChannelSetting SelectedChannelSetting
        {
            get => _selectedChannelSetting;
            set { _selectedChannelSetting = value; OnPropertyChanged(); }
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
        private DateTime _lastDisconnect = DateTime.MinValue;
        private const int _reconnectDelayMs = 7000;
        private int _connectRetries = 0;
        private const int _maxConnectRetries = 5;

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

        private ChannelSetting _currentChannelBitrateSetting;
        public ChannelSetting CurrentChannelBitrateSetting
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

        private ObservableCollection<ChannelSetting> _channelSettings = new ObservableCollection<ChannelSetting>();
        public ObservableCollection<ChannelSetting> ChannelSettings
        {
            get => _channelSettings;
            set { _channelSettings = value; OnPropertyChanged(); }
        }

        // Propriété pour afficher/masquer la popup
        private bool _isAddChannelDialogOpen;
        public bool IsAddChannelDialogOpen
        {
            get => _isAddChannelDialogOpen;
            set { _isAddChannelDialogOpen = value; OnPropertyChanged(); }
        }

        // ViewModel dédié à la popup (pour le binding)
        private AddChannelDialogViewModel? _addChannelDialogVM;
        public AddChannelDialogViewModel? AddChannelDialogVM
        {
            get => _addChannelDialogVM;
            set { _addChannelDialogVM = value; OnPropertyChanged(); }
        }

        public ICommand ShowAddChannelDialogCommand { get; }


        public SoundWeaverControlViewModel()
        {
            _playlistManager = new PlaylistManager();

            ConnectBotCommand = new RelayCommand<object>(async _ => await ConnectBotAsync(),
                                                 _ => !IsConnecting && !IsConnected && SelectedChannelSetting != null);

            DisconnectBotCommand = new RelayCommand<object>(async _ => await DisconnectBotAsync(),
                                                    _ => IsConnected && !IsConnecting);

            LoadPlaylistCommand = new RelayCommand<object>(async _ => await LoadPlaylistAsync(),
                                                  _ => !string.IsNullOrWhiteSpace(PlaylistPath));
            PlayPlaylistCommand = new RelayCommand<object>(async _ => await PlayPlaylistAsync(),
                                                   _ => _currentPlaylist != null && _currentPlaylist.Tracks.Any() && _audioPlayer != null);
            StopAllAudioCommand = new RelayCommand<object>(_ => StopAllAudio(),
                                                   _ => _audioPlayer != null && ActiveLayers.Any());
            AddTrackAsLayerCommand = new RelayCommand<object>(async _ => await AddSelectedTrackAsLayerAsync(),
                                                      _ => SelectedTrack != null && _audioPlayer != null);
            PlayTrackCommand = new RelayCommand<object>(async _ => await PlaySelectedTrackAsync(),
                                                _ => SelectedTrack != null && _audioPlayer != null);
            BrowsePlaylistCommand = new RelayCommand<object>(_ => BrowseForPlaylist());
            ShowAddChannelDialogCommand = new RelayCommand<object>(_ => OpenAddChannelDialog());

            // *********** NOUVELLES COMMANDES PLAYLIST ELEMENTS ************
            AddPlaylistCommand = new RelayCommand<object>(_ => ExecuteAddPlaylist());
            LoadPlaylistElementCommand = new RelayCommand<Playlist>(async playlist => await ExecuteLoadPlaylistElementAsync(playlist));
            PlayPlaylistElementCommand = new RelayCommand<Playlist>(async playlist => await ExecutePlayPlaylistElementAsync(playlist));

            IsConnecting = false;
            IsConnected = false;

            LoadSettings();
            ChannelSettings = new ObservableCollection<ChannelSetting>(_settings.ChannelSettings ?? new List<ChannelSetting>());
        }

        public void LoadSettings()
        {
            _settings = AppSettingsService.LoadModuleSettings("SoundWeaver", () => new SoundWeaverSettings());
        }

        private void ExecuteAddPlaylist()
        {
            var newPlaylist = new Playlist("Nom de la playlist"); ;
            Playlists.Add(new PlaylistElementViewModel(newPlaylist, PlayPlaylistElementCommand, this));
        }

        private async Task ExecuteLoadPlaylistElementAsync(Playlist playlist)
        {
            // Charge la playlist et affiche ses tracks dans le usercontrol (selon ton modèle)
            if (playlist == null)
                return;

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Chargement playlist '{playlist.Name}'...";
                });
                // Exemple : simulateur de chargement
                await Task.Delay(500);
                // Implémente la logique métier ici (ou bind tracks dans le VM)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Playlist '{playlist.Name}' chargée.";
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Erreur chargement playlist : {ex.Message}";
                });
            }
        }

        private async Task ExecutePlayPlaylistElementAsync(Playlist playlist)
        {
            // Démarre la lecture de la playlist sélectionnée (logique à adapter)
            if (playlist == null || playlist.Tracks == null || !playlist.Tracks.Any())
            {
                StatusMessage = "Impossible de lire la playlist?: vide ou non chargée.";
                return;
            }
            // Exemples?: stop tous les layers puis joue le 1er
            _audioPlayer?.StopAllLayers();
            ActiveLayers.Clear();
            _currentPlaylistLayer = null;

            var firstTrack = playlist.Tracks.FirstOrDefault();
            if (firstTrack != null)
            {
                StatusMessage = $"Lecture playlist?: {playlist.Name}";
                _currentPlaylistLayer = _audioPlayer.AddLayer(firstTrack, firstTrack.IsLooping);
                if (_currentPlaylistLayer != null)
                {
                    ActiveLayers.Add(_currentPlaylistLayer);
                    _currentPlaylistLayer.PlaybackEnded += OnLayerEndedUpdateList;
                }
            }
        }

        // ************* FIN LOGIQUE SCROLLER PLAYLISTS *******************

        private async Task ConnectBotAsync()
        {
            if (_botService != null)
            {
                await _botService.ShutdownAsync();
                await _botService.DisposeAsync();
                _botService = null;
            }
            if (_audioPlayer != null)
            {
                _audioPlayer.Dispose();
                _audioPlayer = null;
            }

            IsConnecting = true;
            StatusMessage = "Preflight checks: ping Discord gateway…";

            _botService = new DiscordBotService();

            // Diagnostic AVANT login Discord.NET
            bool gatewayOk = await DiscordBotService.PingDiscordGatewayAsync(_botService.Logger);
            bool tokenOk = await DiscordBotService.TestDiscordTokenAsync(_settings.DiscordToken, _botService.Logger);

            if (!gatewayOk)
            {
                StatusMessage = "? Impossible de joindre la gateway Discord. Réseau coupé, proxy, ou Discord HS ?";
                IsConnecting = false;
                _botService = null;
                return;
            }
            if (!tokenOk)
            {
                StatusMessage = "? Token Discord invalide, bot banni/disabled, ou token mal copié.";
                IsConnecting = false;
                _botService = null;
                return;
            }

            StatusMessage = "Gateway Discord OK. Token OK. Initialisation du bot...";

            try
            {
                await _botService.InitializeAsync(_settings.DiscordToken);
                StatusMessage = "Bot initialized. Connecting to voice channel...";

                // Connexion vocale et récupération du IAudioClient
                if (SelectedChannelSetting == null)
                {
                    StatusMessage = "Aucun salon sélectionné.";
                    return;
                }
                var audioClient = await _botService.JoinVoiceChannelAsync(SelectedChannelSetting.GuildId, SelectedChannelSetting.ChannelId);
                StatusMessage = $"Connected to voice channel {SelectedChannelSetting.ChannelId} on guild {SelectedChannelSetting.GuildId}.";

                if (audioClient != null)
                {
                    var guild = _botService.Client.GetGuild(SelectedChannelSetting.GuildId);
                    var channel = guild?.GetVoiceChannel(SelectedChannelSetting.ChannelId);
                    int discordCap = channel?.Bitrate ?? 96000;
                    string channelName = channel?.Name ?? $"Channel_{SelectedChannelSetting.ChannelId}";

                    var chanList = _settings.ChannelSettings ??= new List<ChannelSetting>();
                    var chanSetting = chanList.FirstOrDefault(x => x.ChannelId == SelectedChannelSetting.ChannelId);
                    if (chanSetting == null)
                    {
                        chanSetting = new ChannelSetting
                        {
                            ChannelId = SelectedChannelSetting.ChannelId,
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

                    SaveSettings();

                    UpdateBitrateBounds(chanSetting.DiscordBitrateCap, _settings.SelectedChannels);
                    TargetBitrate = chanSetting.Bitrate;

                    _audioPlayer = new MultiLayerAudioPlayer(
                                       audioClient,
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
                Console.WriteLine($"[SoundWeaver VM] Exception lors de la connexion Discord: {ex}");

                if ((ex is Discord.Net.WebSocketClosedException wsEx && wsEx.CloseCode == 4006) ||
                     ex.ToString().Contains("Session is no longer valid") ||
                     ex.ToString().Contains("WebSocketClosedException"))
                {
                    StatusMessage = $"Session Discord.NET invalide (4006). Attente 10s puis tentative de reconnexion…";
                    await Task.Delay(10_000);
                    await ConnectBotAsync();
                    return;
                }
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
                    await _botService.DisposeAsync();
                    _botService = null;
                }
                _lastDisconnect = DateTime.UtcNow; // <--- TRACK la dernière déco
                StatusMessage = "Disconnected.";
                IsConnected = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during disconnect: {ex.Message}";
                Console.WriteLine($"[SoundWeaver VM] Exception lors du disconnect: {ex}");
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
        /// Recalcule les bornes min?/?max à partir du cap Discord et du nombre de canaux choisis.
        /// </summary>
        private void UpdateBitrateBounds(int discordCapBps, int channelCount)
        {
            BitrateMax = discordCapBps;
            BitrateMin = 8_000 * channelCount;
            TargetBitrate = Math.Clamp(TargetBitrate, BitrateMin, BitrateMax);
        }

        private ChannelSetting GetOrCreateChannelSetting(ulong channelId, string channelName, int discordCap)
        {
            var setting = _settings.ChannelSettings.FirstOrDefault(x => x.ChannelId == channelId);
            if (setting == null)
            {
                setting = new ChannelSetting
                {
                    ChannelId = channelId,
                    ChannelName = channelName,
                    DiscordBitrateCap = discordCap,
                    Bitrate = Math.Min(64000, discordCap) // Valeur initiale safe
                };
                _settings.ChannelSettings.Add(setting);
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
            _settings.ChannelSettings = ChannelSettings.ToList();
            AppSettingsService.SaveModuleSettings("SoundWeaver", _settings);
        }

        public void AddPlaylist()
        {
            var playlist = new Playlist("Nouvelle Playlist");
            var vm = new PlaylistElementViewModel(playlist, PlayPlaylistCommand, this);
            Playlists.Add(vm);
        }

        public void RemovePlaylist(PlaylistElementViewModel vm)
        {
            if (Playlists.Contains(vm))
                Playlists.Remove(vm);
        }

        private void OpenAddChannelDialog()
        {
            var dialogVM = new AddChannelDialogViewModel
            {
                ExistingChannels = ChannelSettings.ToList(),
                BotToken = _settings.DiscordToken
            };

            var dialog = new Vue.AddChannelDialogWindow
            {
                DataContext = dialogVM,
                Owner = Application.Current.MainWindow
            };

            dialogVM.RequestClose += (channel) =>
            {
                dialog.DialogResultChannel = channel;
                dialog.CloseDialog(channel != null); 
            };

            bool? result = dialog.ShowDialog();
            if (result == true && dialog.DialogResultChannel != null)
            {
                ChannelSettings.Add(dialog.DialogResultChannel);
                SaveSettings();
            }
        }

    }

}
