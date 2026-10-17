using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Mooh.Presentation.Music
{
    /// <summary>Editable JSON tracker data replayed by the Skale engine, with external Ogg samples.</summary>
    public sealed class SkaleModule
    {
        private SkaleModule()
        {
        }

        /// <summary>Gets the title.</summary>
        public string Title { get; private set; } = string.Empty;

        /// <summary>Gets the initial tempo in beats per minute.</summary>
        public int Bpm { get; private set; } = 125;

        /// <summary>Gets the initial ticks per row.</summary>
        public int Speed { get; private set; } = 6;

        /// <summary>Gets the module's final mixer gain, independent of the user's playback volume.</summary>
        public float MasterGain { get; private set; } = 1;

        /// <summary>Gets the order list: pattern numbers by position.</summary>
        public IReadOnlyList<int> Order { get; private set; } = [];

        /// <summary>Gets how many order positions the song uses.</summary>
        public int SongLength
        {
            get; private set;
        }

        /// <summary>Gets the patterns by number.</summary>
        public IReadOnlyDictionary<int, SkalePattern> Patterns { get; private set; } = new Dictionary<int, SkalePattern>();

        /// <summary>Gets the instruments by zero-based number.</summary>
        public IReadOnlyDictionary<int, SkaleInstrument> Instruments { get; private set; } = new Dictionary<int, SkaleInstrument>();

        /// <summary>Gets the number of channels the patterns use.</summary>
        public int ChannelCount
        {
            get; private set;
        }

        /// <summary>Loads a JSON module and its sample files relative to the module directory.</summary>
        public static SkaleModule Load(string path)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
            return Load(File.ReadAllBytes(path), file =>
            {
                string sample = Path.GetFullPath(Path.Combine(directory, file));
                return !sample.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    ? throw new InvalidDataException("A module sample must stay inside its music directory.")
                    : File.ReadAllBytes(sample);
            });
        }

        /// <summary>Reads JSON tracker data with an optional resolver for external sample files.</summary>
        public static SkaleModule Load(byte[] data, Func<string, byte[]>? loadSample = null)
        {
            ArgumentNullException.ThrowIfNull(data);
            try
            {
                using JsonDocument document = JsonDocument.Parse(data);
                JsonElement root = document.RootElement;
                if (root.GetProperty("formatVersion").GetInt32() != 1)
                {
                    throw new InvalidDataException("Unsupported tracker JSON version.");
                }
                SkaleModule module = new()
                {
                    Title = root.GetProperty("title").GetString() ?? string.Empty,
                    Bpm = root.GetProperty("bpm").GetInt32(),
                    Speed = root.GetProperty("speed").GetInt32(),
                    MasterGain = root.GetProperty("masterGain").GetSingle(),
                    Order = [.. root.GetProperty("order").EnumerateArray().Select(value => value.GetInt32())],
                    SongLength = root.GetProperty("songLength").GetInt32(),
                    ChannelCount = root.GetProperty("channelCount").GetInt32(),
                };
                if (module.Bpm <= 0 || module.Speed <= 0 || module.ChannelCount is < 1 or > 256
                    || module.SongLength < 1 || module.SongLength > module.Order.Count
                    || !float.IsFinite(module.MasterGain) || module.MasterGain < 0)
                {
                    throw new InvalidDataException("Invalid tracker tempo, order length, channels or gain.");
                }
                Dictionary<int, SkalePattern> patterns = [];
                foreach (JsonElement item in root.GetProperty("patterns").EnumerateArray())
                {
                    SkalePattern pattern = new(item.GetProperty("index").GetInt32(), item.GetProperty("rows").GetInt32());
                    if (pattern.Rows is < 1 or > 256 || !patterns.TryAdd(pattern.Index, pattern))
                    {
                        throw new InvalidDataException("Invalid or duplicated pattern.");
                    }
                    foreach (JsonElement channel in item.GetProperty("channels").EnumerateArray())
                    {
                        int index = channel.GetProperty("index").GetInt32();
                        SkaleCell[] cells = [.. channel.GetProperty("cells").EnumerateArray().Select(cell => new SkaleCell(
                            cell.GetProperty("note").GetInt32(), cell.GetProperty("instrument").GetInt32(),
                            cell.GetProperty("volume").GetInt32(), cell.GetProperty("effect").GetInt32(), cell.GetProperty("param").GetInt32()))];
                        if (index < 0 || index >= module.ChannelCount || cells.Length < pattern.Rows || pattern.Channels.ContainsKey(index))
                        {
                            throw new InvalidDataException("Invalid or duplicated pattern channel.");
                        }
                        pattern.SetChannel(index, cells);
                    }
                }
                Dictionary<int, SkaleInstrument> instruments = [];
                foreach (JsonElement item in root.GetProperty("instruments").EnumerateArray())
                {
                    SkaleInstrument instrument = SkaleInstrument.Read(item, loadSample);
                    if (!instruments.TryAdd(instrument.Index, instrument))
                    {
                        throw new InvalidDataException("Duplicated tracker instrument.");
                    }
                }
                module.Patterns = patterns;
                module.Instruments = instruments;
                return module;
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
            {
                throw new InvalidDataException("The tracker module contains invalid JSON.", error);
            }
        }
    }
}
