using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Mooh.Presentation.Music
{
    /// <summary>One <c>Music.ini</c> track: which module plays from which order position, how loud.</summary>
    /// <param name="Name">The track's name, such as <c>game</c>.</param>
    /// <param name="File">The module, without extension.</param>
    /// <param name="Position">The order position to start at.</param>
    /// <param name="Volume">The master gain.</param>
    public sealed record MusicTrack(string Name, string File, int Position, double Volume);

    /// <summary>Reads the recovered <c>Music.ini</c>: <c>TrackName,FileName,Pos,Vol</c> per line.</summary>
    public static class MusicCatalog
    {
        /// <summary>Reads the tracks.</summary>
        /// <param name="stream">The <c>Music.ini</c> bytes.</param>
        /// <returns>The tracks by name.</returns>
        /// <exception cref="InvalidDataException">A line is malformed.</exception>
        public static IReadOnlyDictionary<string, MusicTrack> Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            using StreamReader reader = new(stream, Encoding.Latin1);
            Dictionary<string, MusicTrack> tracks = [with(StringComparer.Ordinal)];
            while (reader.ReadLine() is string line)
            {
                string entry = line.Trim();
                if (entry.Length == 0 || entry[0] == ';')
                {
                    continue;
                }

                string[] fields = entry.Split(',', StringSplitOptions.TrimEntries);
                if (fields.Length < 4
                    || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int position)
                    || !double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double volume))
                {
                    throw new InvalidDataException($"The music line '{entry}' is malformed.");
                }

                tracks[fields[0]] = new MusicTrack(fields[0], fields[1], position, volume);
            }

            return tracks;
        }
    }
}
