using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using SoundWeaver.Audio;
using SoundWeaver.Playlists;

namespace SoundWeaver.Models
{
    public class PlaylistElementViewModel : BaseViewModel
    {
        public Playlist Playlist { get; private set; }
        public ObservableCollection<AudioTrack> Tracks => Playlist.Tracks;

        public ICommand PlayPlaylistCommand { get; }
        public ICommand BrowseAndLoadCommand { get; }
        public ICommand DeletePlaylistCommand { get; }

        private readonly SoundWeaverControlViewModel _parentVm;
        private readonly PlaylistManager _playlistManager;
        public bool IsLoadAvailable => Playlist.Tracks.Count == 0;
        public bool IsLoaded => Playlist.Tracks.Count > 0;

        public PlaylistElementViewModel(
            Playlist playlist,
            ICommand playPlaylistCommand,
            SoundWeaverControlViewModel parentVm)
        {
            Playlist = playlist;
            PlayPlaylistCommand = playPlaylistCommand;
            _playlistManager = new PlaylistManager();
            _parentVm = parentVm;

            BrowseAndLoadCommand = new RelayCommand<object>(async _ => await BrowseAndLoadAsync());
            DeletePlaylistCommand = new RelayCommand<object>(_ => DeleteSelf());

            Playlist.Tracks.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsLoadAvailable));
            };
            Playlist.Tracks.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsLoaded));
            };
        }

        private async Task BrowseAndLoadAsync()
        {
            var ofd = new OpenFileDialog()
            {
                Filter = "M3U8 Playlist (*.m3u8)|*.m3u8|All files (*.*)|*.*",
                Title = "Choisir une playlist M3U8"
            };

            if (ofd.ShowDialog() == true)
            {
                var loaded = await _playlistManager.LoadM3U8PlaylistAsync(ofd.FileName);
                if (loaded != null)
                {
                    Playlist.Tracks.Clear();
                    foreach (var t in loaded.Tracks)
                        Playlist.Tracks.Add(t);

                    Playlist.Name = loaded.Name;
                    OnPropertyChanged(nameof(Tracks));
                    OnPropertyChanged(nameof(Playlist));

                }
            }
        }

        private void DeleteSelf()
        {
            _parentVm.RemovePlaylist(this);
        }
    }
}
