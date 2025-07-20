using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SoundWeaver.Models
{
    public class PlaylistElementViewModel : BaseViewModel
    {
        public Playlist Playlist { get; }

        public ObservableCollection<AudioTrack> Tracks => Playlist.Tracks;

        public ICommand LoadPlaylistCommand { get; }
        public ICommand PlayPlaylistCommand { get; }

        public PlaylistElementViewModel(
            Playlist playlist,
            ICommand loadPlaylistCommand,
            ICommand playPlaylistCommand)
        {
            Playlist = playlist;
            LoadPlaylistCommand = loadPlaylistCommand;
            PlayPlaylistCommand = playPlaylistCommand;
        }
    }
}