using System.Collections.Generic;

namespace Mooh.Presentation.Music
{
    /// <summary>One pattern cell as the player reads it, <c>sub_10005B70</c>.</summary>
    /// <param name="Note">0 to 95 a pitch, 128 no new note, 129 key off.</param>
    /// <param name="Instrument">The one-based instrument, 0 for none.</param>
    /// <param name="Volume">0 to 0x80 a volume, 0x81 and up use the sample's.</param>
    /// <param name="Effect">The effect.</param>
    /// <param name="Param">The effect's parameter.</param>
    public readonly record struct SkaleCell(int Note, int Instrument, int Volume, int Effect, int Param)
    {
        /// <summary>Gets a cell with every field at its default, <c>sub_10007CE0</c>.</summary>
        public static SkaleCell Empty { get; } = new(128, 0, 129, 0, 0);
    }

    /// <summary>One pattern: so many rows of cells per channel.</summary>
    /// <param name="index">The pattern number.</param>
    /// <param name="rows">The row count.</param>
    public sealed class SkalePattern(int index, int rows)
    {
        private readonly Dictionary<int, SkaleCell[]> _channels = [];

        /// <summary>Gets the pattern number.</summary>
        public int Index { get; } = index;

        /// <summary>Gets the row count.</summary>
        public int Rows { get; } = rows;

        /// <summary>Gets the cells by channel.</summary>
        public IReadOnlyDictionary<int, SkaleCell[]> Channels => _channels;

        internal void SetChannel(int channel, SkaleCell[] cells)
        {
            _channels[channel] = cells;
        }
    }
}
