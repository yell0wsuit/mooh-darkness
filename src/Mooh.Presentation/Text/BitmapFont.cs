using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

using Mooh.Core.Content;

namespace Mooh.Presentation.Text
{
    /// <summary>
    /// Reads a recovered bitmap font, the <c>.fnt</c> glyph table beside each font's <c>.png</c>
    /// atlas, and lays out text the way the native renderer does.
    /// </summary>
    /// <remarks>
    /// The format is <c>font</c>, a version, the first and last codes, and the default advance as
    /// two 8.8 fixed-point values, then one 14-byte record per code from the first to the last:
    /// the glyph's width and height in bytes, its x and y offsets and its x and y advances as
    /// signed 8.8 values, and its atlas position as two 16-bit values. A record of zero width has
    /// no glyph. The native lookup, <c>sub_406FA0</c>, places a glyph's left edge at the pen plus
    /// its x offset and its bottom edge at the pen plus its y offset, both shifted down to whole
    /// pixels; a code outside the range draws nothing and moves the pen by the default advance.
    /// </remarks>
    public sealed class BitmapFont
    {
        private const int HeaderSize = 16;
        private const int RecordSize = 14;
        private const double FixedPoint = 256;

        private readonly byte[] _data;
        private readonly int _defaultAdvance;

        private BitmapFont(string name, byte[] data)
        {
            Name = name;
            _data = data;
            FirstCode = data[8];
            LastCode = data[9];
            _defaultAdvance = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(10));
        }

        /// <summary>Gets the font name, such as <c>PanelDigFnt</c>.</summary>
        public string Name
        {
            get;
        }

        /// <summary>Gets the first code the font has a record for.</summary>
        public int FirstCode
        {
            get;
        }

        /// <summary>Gets the last code the font has a record for.</summary>
        public int LastCode
        {
            get;
        }

        /// <summary>Reads a font's glyph table.</summary>
        /// <param name="name">The font name.</param>
        /// <param name="stream">The <c>.fnt</c> bytes.</param>
        /// <returns>The font.</returns>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <exception cref="InvalidDataException">The data is not a recovered glyph table.</exception>
        public static BitmapFont Load(string name, Stream stream)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(stream);
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            byte[] data = copy.ToArray();
            if (data.Length < HeaderSize || !data.AsSpan(0, 4).SequenceEqual("font"u8))
            {
                throw new InvalidDataException($"Font '{name}' is not a font glyph table.");
            }

            int first = data[8];
            int last = data[9];
            return last < first || HeaderSize + ((last - first + 1) * RecordSize) > data.Length
                ? throw new InvalidDataException($"Font '{name}' declares more glyphs than it holds.")
                : new BitmapFont(name, data);
        }

        /// <summary>
        /// Measures a line: the sum of its advances, the native <c>sub_408040</c>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The width in pixels.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        public double Width(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            int width = 0;
            foreach (char character in text)
            {
                width += AdvanceOf(character);
            }

            return width / FixedPoint;
        }

        /// <summary>Lays out a line of text against a point.</summary>
        /// <param name="text">The text.</param>
        /// <param name="x">The point's screen x.</param>
        /// <param name="y">The point's screen y, the baseline the glyph offsets hang from.</param>
        /// <param name="alignment">Where the line sits against the point.</param>
        /// <returns>The glyphs to draw, in order.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        public IReadOnlyList<GlyphPlacement> Layout(string text, double x, double y, TextAlignment alignment)
        {
            ArgumentNullException.ThrowIfNull(text);
            double penX = alignment switch
            {
                TextAlignment.Center => x - Math.Floor(Width(text) * 0.5),
                TextAlignment.Right => x - Width(text),
                TextAlignment.Left => x,
                _ => throw new ArgumentOutOfRangeException(nameof(alignment), alignment, null),
            };
            List<GlyphPlacement> glyphs = [];
            foreach (char character in text)
            {
                if (TryRecord(character, out ReadOnlySpan<byte> record) && record[0] != 0)
                {
                    int width = record[0];
                    int height = record[1];
                    int left = BinaryPrimitives.ReadInt16LittleEndian(record[2..]) >> 8;
                    int bottom = BinaryPrimitives.ReadInt16LittleEndian(record[4..]) >> 8;
                    glyphs.Add(new GlyphPlacement(
                        BinaryPrimitives.ReadUInt16LittleEndian(record[10..]),
                        BinaryPrimitives.ReadUInt16LittleEndian(record[12..]),
                        width,
                        height,
                        penX + left,
                        y + bottom - height));
                }

                penX += AdvanceOf(character) / FixedPoint;
            }

            return glyphs;
        }

        private int AdvanceOf(char character)
        {
            return TryRecord(character, out ReadOnlySpan<byte> record) && record[0] != 0
                ? BinaryPrimitives.ReadInt16LittleEndian(record[6..])
                : _defaultAdvance;
        }

        private bool TryRecord(char character, out ReadOnlySpan<byte> record)
        {
            if (StringTable.TryEncode(character, out byte code) && code >= FirstCode && code <= LastCode)
            {
                record = _data.AsSpan(HeaderSize + ((code - FirstCode) * RecordSize), RecordSize);
                return true;
            }

            record = default;
            return false;
        }
    }
}
