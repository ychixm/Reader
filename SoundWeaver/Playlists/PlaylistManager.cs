using Microsoft.Extensions.Logging;
using SoundWeaver.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SoundWeaver.Playlists
{
    public class PlaylistManager
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private readonly ILogger<PlaylistManager> _logger;

        public PlaylistManager(ILogger<PlaylistManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Playlist> LoadM3U8PlaylistAsync(string m3u8PathOrUrl, string playlistName = null)
        {
            if (string.IsNullOrWhiteSpace(m3u8PathOrUrl))
            {
                _logger.LogError("M3U8 path or URL cannot be null or whitespace.");
                throw new ArgumentNullException(nameof(m3u8PathOrUrl));
            }

            var effectivePlaylistName = playlistName ?? Path.GetFileNameWithoutExtension(m3u8PathOrUrl) ?? "Untitled Playlist";
            _logger.LogInformation("Loading M3U8 playlist '{PlaylistName}' from: {PathOrUrl}", effectivePlaylistName, m3u8PathOrUrl);
            var playlist = new Playlist(effectivePlaylistName);
            List<string> lines;

            if (Uri.TryCreate(m3u8PathOrUrl, UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                // Load from URL
                try
                {
                    var response = await httpClient.GetStringAsync(uriResult);
                    lines = response.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Error fetching playlist from URL '{PathOrUrl}'", m3u8PathOrUrl);
                    return null; // Or throw custom exception
                }
            }
            else
            {
                // Load from local file path
                if (!File.Exists(m3u8PathOrUrl))
                {
                    _logger.LogError("Playlist file not found at '{PathOrUrl}'", m3u8PathOrUrl);
                    return null; // Or throw custom exception
                }
                lines = (await File.ReadAllLinesAsync(m3u8PathOrUrl)).ToList();
            }

            _logger.LogDebug("Parsing M3U8 content for playlist '{PlaylistName}'. Base path: {BasePath}", playlist.Name, baseFilePath);
            return ParseM3U8Content(lines, playlist, Path.GetDirectoryName(m3u8PathOrUrl));
        }

        private Playlist ParseM3U8Content(List<string> lines, Playlist playlist, string baseFilePath = null)
        {
            if (lines == null || !lines.Any())
            {
                _logger.LogWarning("Playlist content for '{PlaylistName}' is empty or null.", playlist.Name);
                return playlist; // Return empty playlist
            }

            // Basic M3U validation: must start with #EXTM3U
            if (!lines[0].Trim().Equals("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid M3U8 file for '{PlaylistName}': Missing #EXTM3U header. Proceeding leniently.", playlist.Name);
                // Depending on strictness, could return null or throw
                // For now, proceed leniently, might be a simple list of files
            }

            string currentTrackTitle = null;
            // bool isExtendedFormat = lines[0].Trim().Equals("#EXTM3U", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#EXT-X-VERSION") || line.StartsWith("#EXT-X-TARGETDURATION") || line.StartsWith("#EXT-X-MEDIA-SEQUENCE") || line.StartsWith("#EXT-X-PLAYLIST-TYPE") || line.StartsWith("#EXT-X-ENDLIST") || line.StartsWith("#EXTM3U"))
                {
                    // Skip metadata lines not directly related to track info or standard comments
                    continue;
                }

                if (line.StartsWith("#EXTINF:"))
                {
                    // Format: #EXTINF:<duration>,<title>
                    // Duration is typically integer seconds, -1 if unknown.
                    // Title is optional.
                    var infoPart = line.Substring("#EXTINF:".Length);
                    var commaIndex = infoPart.IndexOf(',');
                    if (commaIndex != -1 && commaIndex + 1 < infoPart.Length)
                    {
                        currentTrackTitle = infoPart.Substring(commaIndex + 1).Trim();
                    }
                    // Duration can be parsed here if needed: infoPart.Substring(0, commaIndex)
                }
                else if (!line.StartsWith("#")) // Not a comment or directive, assume it's a media URI/path
                {
                    string trackSource = line;

                    // Handle relative paths for local M3U8 files
                    if (!string.IsNullOrEmpty(baseFilePath) && !Uri.IsWellFormedUriString(trackSource, UriKind.Absolute))
                    {
                        trackSource = Path.GetFullPath(Path.Combine(baseFilePath, trackSource));
                    }

                    try
                    {
                        var audioTrack = new AudioTrack(trackSource, currentTrackTitle);
                        // #EXTVLCOPT:loop could set IsLooping if we parse it
                        playlist.AddTrack(audioTrack);
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogWarning(ex, "Skipping invalid track source '{TrackSource}' in playlist '{PlaylistName}'", trackSource, playlist.Name);
                    }
                    currentTrackTitle = null; // Reset for next #EXTINF or plain path
                }
            }
            _logger.LogInformation("Finished parsing M3U8 content for playlist '{PlaylistName}'. Added {TrackCount} tracks.", playlist.Name, playlist.Tracks.Count);
            return playlist;
        }

        // Example usage (conceptual, would be part of a larger system)
        // public async Task PlayPlaylist(Playlist playlist, AudioPlayer audioPlayer)
        // {
        //     if (playlist == null || audioPlayer == null) return;
        //
        //     AudioTrack currentTrack;
        //     while ((currentTrack = playlist.GetNextTrack()) != null)
        //     {
        //         // _logger.LogInformation($"Playing: {currentTrack.Title} from {currentTrack.Source}"); // Example if logger was available
        //         await audioPlayer.PlayFileAsync(currentTrack.Source.IsFile ? currentTrack.Source.LocalPath : currentTrack.Source.AbsoluteUri, currentTrack.IsLooping);
        //
        //         // This is simplistic: PlayFileAsync is asynchronous but doesn't wait for completion here.
        //         // A real player would need to know when a track finishes to play the next one.
        //         // This would involve events from AudioPlayer or a more complex queueing mechanism.
        //         // For now, this just adds all tracks to the mixer if PlayFileAsync adds them non-blockingly.
        //         // If PlayFileAsync blocks, it will play them sequentially.
        //     }
        //
        //     if (playlist.CurrentTrackIndex >= playlist.Tracks.Count && !playlist.IsLooping)
        //     {
        //         // _logger.LogInformation("Playlist finished."); // Example if logger was available
        //     }
        // }
    }
}
