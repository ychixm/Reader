using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using SoundWeaver.Audio;
using SoundWeaver.Models;
using SoundWeaver.Playlists;

namespace SoundWeaver.Models
{
    public class PlaylistElementViewModel : BaseViewModel
    {
        public Playlist Playlist { get; private set; }

        public ObservableCollection<AudioTrack> Tracks => Playlist.Tracks;

        public ICommand LoadPlaylistCommand { get; }
        public ICommand PlayPlaylistCommand { get; }
        public ICommand BrowseAndLoadCommand { get; }

        private readonly PlaylistManager _playlistManager;

        public PlaylistElementViewModel(
            Playlist playlist,
            ICommand loadPlaylistCommand,
            ICommand playPlaylistCommand)
        {
            Playlist = playlist;
            LoadPlaylistCommand = loadPlaylistCommand;
            PlayPlaylistCommand = playPlaylistCommand;
            _playlistManager = new PlaylistManager();
            BrowseAndLoadCommand = new RelayCommand<object>(async _ => await BrowseAndLoadAsync());
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
                    // Si tu veux changer la référence : Playlist = loaded; OnPropertyChanged(nameof(Playlist));
                    // Si tu veux garder la même instance, mets à jour ses tracks :
                    Playlist.Tracks.Clear();
                    foreach (var t in loaded.Tracks)
                        Playlist.Tracks.Add(t);

                    Playlist.Name = loaded.Name;
                    OnPropertyChanged(nameof(Tracks));
                    OnPropertyChanged(nameof(Playlist));
                }
            }
        }
    }
}
