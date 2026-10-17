using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mooh.Presentation.Music
{
    /// <summary>One <c>Music.json</c> track: which module plays from which order position, how loud.</summary>
    /// <param name="Name">The track's name, such as <c>game</c>.</param>
    /// <param name="File">The module, without extension.</param>
    /// <param name="Position">The order position to start at.</param>
    /// <param name="Volume">The master gain.</param>
    public sealed record MusicTrack(string Name, string File, int Position, double Volume);

    /// <summary>Reads named JSON music cues with module, order position and gain.</summary>
    public static class MusicCatalog
    {
        /// <summary>Reads the tracks.</summary>
        /// <param name="stream">The <c>Music.json</c> bytes.</param>
        /// <returns>The tracks by name.</returns>
        /// <exception cref="InvalidDataException">A line is malformed.</exception>
        public static IReadOnlyDictionary<string, MusicTrack> Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            try
            {
                using JsonDocument document = JsonDocument.Parse(stream);
                Dictionary<string, MusicTrack> tracks = [with(StringComparer.Ordinal)];
                foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                {
                    JsonElement track = entry.Value;
                    string file = track.GetProperty("file").GetString()
                        ?? throw new InvalidDataException("A music cue requires a module filename.");
                    int position = track.GetProperty("position").GetInt32();
                    double volume = track.GetProperty("volume").GetDouble();
                    if (position < 0 || !double.IsFinite(volume) || volume < 0
                        || !tracks.TryAdd(entry.Name, new MusicTrack(entry.Name, file, position, volume)))
                    {
                        throw new InvalidDataException($"Music cue '{entry.Name}' is invalid or duplicated.");
                    }
                }
                return tracks;
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                throw new InvalidDataException("The music catalog contains invalid JSON.", error);
            }
        }
    }
}
