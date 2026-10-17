using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Mooh.Presentation.Audio
{
    /// <summary>Reads JSON sound aliases with file choices, buffer counts, gain and pitch ranges.</summary>
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
        /// <param name="stream">The <c>Sound.json</c> bytes.</param>
        /// <returns>The catalog.</returns>
        /// <exception cref="InvalidDataException">A line is malformed.</exception>
        public static SoundCatalog Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            try
            {
                using JsonDocument document = JsonDocument.Parse(stream);
                Dictionary<string, SoundDefinition> sounds = new(StringComparer.Ordinal);
                foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                {
                    JsonElement sound = entry.Value;
                    string[] files = [.. sound.GetProperty("files").EnumerateArray().Select(file => file.GetString()
                        ?? throw new InvalidDataException("A sound filename is null."))];
                    int buffers = sound.GetProperty("buffers").GetInt32();
                    if (buffers < 1 || !sounds.TryAdd(entry.Name, new SoundDefinition(entry.Name, files, buffers,
                        Range(sound.GetProperty("volume")), Range(sound.GetProperty("frequency")))))
                    {
                        throw new InvalidDataException($"Sound '{entry.Name}' has invalid buffers or is duplicated.");
                    }
                }
                return new SoundCatalog(sounds);
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                throw new InvalidDataException("The sound catalog contains invalid JSON.", error);
            }
        }

        private static (double, double) Range(JsonElement range)
        {
            if (range.GetArrayLength() != 2)
            {
                throw new InvalidDataException("A sound range requires two values.");
            }
            double min = range[0].GetDouble(), max = range[1].GetDouble();
            return double.IsFinite(min) && double.IsFinite(max) && min >= 0 && max >= min
                ? (min, max) : throw new InvalidDataException("A sound range must be finite, ordered and nonnegative.");
        }
    }
}
