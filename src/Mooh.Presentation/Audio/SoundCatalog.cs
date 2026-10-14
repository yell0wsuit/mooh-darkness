using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Mooh.Presentation.Audio
{
    /// <summary>
    /// Reads the recovered <c>Sound.ini</c>, which the original's <c>soundCash.Load</c> reads: one
    /// line per alias, <c>Alias,File1;File2;...,Buffers,Volume,Frequency</c>, where a volume or a
    /// frequency is a value, a <c>from-to</c> range or empty for 1. Lines starting with <c>;</c> are
    /// comments and with <c>#</c> are disabled.
    /// </summary>
    public sealed class SoundCatalog
    {
        private readonly Dictionary<string, SoundDefinition> _sounds;

        private SoundCatalog(Dictionary<string, SoundDefinition> sounds)
        {
            _sounds = sounds;
        }

        /// <summary>Gets an alias.</summary>
        /// <param name="alias">The alias.</param>
        /// <returns>The alias's definition.</returns>
        /// <exception cref="KeyNotFoundException">There is no such alias.</exception>
        public SoundDefinition this[string alias] => _sounds[alias];

        /// <summary>Gets every alias.</summary>
        public IEnumerable<SoundDefinition> Sounds => _sounds.Values;

        /// <summary>Gets whether an alias exists.</summary>
        /// <param name="alias">The alias.</param>
        /// <returns><see langword="true"/> when it exists.</returns>
        public bool Contains(string alias)
        {
            return _sounds.ContainsKey(alias);
        }

        /// <summary>Reads a catalog.</summary>
        /// <param name="stream">The <c>Sound.ini</c> bytes.</param>
        /// <returns>The catalog.</returns>
        /// <exception cref="InvalidDataException">A line is malformed.</exception>
        public static SoundCatalog Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            // The comments are Windows-1251 Russian; the entries themselves are ASCII.
            using StreamReader reader = new(stream, Encoding.Latin1);
            Dictionary<string, SoundDefinition> sounds = [with(StringComparer.Ordinal)];
            while (reader.ReadLine() is string line)
            {
                string entry = line.Trim();
                if (entry.Length == 0 || entry[0] is ';' or '#')
                {
                    continue;
                }

                string[] fields = entry.Split(',');
                if (fields.Length < 3)
                {
                    throw new InvalidDataException($"The sound line '{entry}' has too few fields.");
                }

                string[] files = [.. fields[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
                int buffers = int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ? count : 1;
                sounds[fields[0].Trim()] = new SoundDefinition(
                    fields[0].Trim(),
                    files,
                    Math.Max(buffers, 1),
                    Range(fields.ElementAtOrDefault(3)),
                    Range(fields.ElementAtOrDefault(4)));
            }

            return new SoundCatalog(sounds);
        }

        private static (double, double) Range(string? field)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return (1, 1);
            }

            string[] ends = field.Split('-', StringSplitOptions.TrimEntries);
            double from = double.Parse(ends[0], CultureInfo.InvariantCulture);
            double to = ends.Length > 1 ? double.Parse(ends[1], CultureInfo.InvariantCulture) : from;
            return (from, to);
        }
    }
}
