using System;

namespace SoundWeaver.Models
{
    public class AudioTrack
    {
        public string Title { get; set; }
        public Uri Source { get; set; } // Can be local file path or URL
        public bool IsLooping { get; set; } // For individual track looping
        public TimeSpan Duration { get; set; } // Optional: for display or pre-loading info
        public string Artist { get; set; } // Optional metadata
        public string Album { get; set; } // Optional metadata

        // Runtime properties, not typically part of M3U8 but useful for player
        public float Volume { get; set; } = 1.0f; // Default volume

        public AudioTrack(string source, string title = null)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentNullException(nameof(source));

            if (!Uri.TryCreate(source, UriKind.Absolute, out Uri parsedUri))
            {
                // Try to treat as a local file path
                if (System.IO.Path.IsPathRooted(source) || !source.Contains("://"))
                {
                    parsedUri = new Uri(System.IO.Path.GetFullPath(source));
                }
                else
                {
                    throw new ArgumentException("Invalid source URI or path.", nameof(source));
                }
            }

            Source = parsedUri;
            Title = title ?? (parsedUri.IsFile ? System.IO.Path.GetFileNameWithoutExtension(parsedUri.LocalPath) : parsedUri.Segments.LastOrDefault() ?? "Unknown Title");
        }
    }

    public class Playlist
    {
        public string Name { get; set; }
        public List<AudioTrack> Tracks { get; private set; }
        public bool IsLooping { get; set; } // For whole playlist looping
        public int CurrentTrackIndex { get; set; } = -1; // -1 means not started or finished

        public Playlist(string name)
        {
            Name = name;
            Tracks = new List<AudioTrack>();
        }

        public void AddTrack(AudioTrack track)
        {
            Tracks.Add(track);
        }

        public AudioTrack GetNextTrack()
        {
            if (Tracks.Count == 0) return null;

            CurrentTrackIndex++;
            if (CurrentTrackIndex >= Tracks.Count)
            {
                if (IsLooping)
                {
                    CurrentTrackIndex = 0;
                }
                else
                {
                    CurrentTrackIndex = Tracks.Count; // Indicate end
                    return null;
                }
            }
            return Tracks[CurrentTrackIndex];
        }

        public AudioTrack GetCurrentTrack()
        {
            if (CurrentTrackIndex >= 0 && CurrentTrackIndex < Tracks.Count)
            {
                return Tracks[CurrentTrackIndex];
            }
            return null;
        }

        public void Reset()
        {
            CurrentTrackIndex = -1;
        }
    }
}
