using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mooh.Core.Content
{
    /// <summary>
    /// Reads the recovered localized string table, <c>strings.bin</c>, which <c>StrHost.StrGet</c>
    /// resolves every text id against — the help texts of the message tables among them.
    /// </summary>
    /// <remarks>
    /// The format is <c>str2</c>, a version, the entry count and the offset of the value index,
    /// then the language name's <c>(length, offset)</c>, the key index and, at that offset, the
    /// value index. Each index entry is a little-endian <c>(offset, length)</c> pair into Windows-1252
    /// text.
    /// </remarks>
    public sealed class StringTable
    {
        private const int HeaderSize = 16;
        private const int EntrySize = 8;

        /// <summary>
        /// The Windows-1252 characters for bytes 0x80 to 0x9F, the only range where it differs from
        /// Latin-1. Unassigned bytes keep their Latin-1 control character.
        /// </summary>
        private static readonly char[] Windows1252High =
        [
            '€', '\u0081', '‚', 'ƒ', '„', '…', '†', '‡',
            'ˆ', '‰', 'Š', '‹', 'Œ', '\u008D', 'Ž', '\u008F',
            '\u0090', '‘', '’', '“', '”', '•', '–', '—',
            '˜', '™', 'š', '›', 'œ', '\u009D', 'ž', 'Ÿ',
        ];

        private readonly Dictionary<string, string> _texts;

        private StringTable(string language, Dictionary<string, string> texts)
        {
            Language = language;
            _texts = texts;
        }

        /// <summary>Gets the table's language name, such as <c>Eng</c>.</summary>
        public string Language
        {
            get;
        }

        /// <summary>Gets the number of texts.</summary>
        public int Count => _texts.Count;

        /// <summary>Gets the text for an id.</summary>
        /// <param name="id">The recovered text id.</param>
        /// <returns>The text.</returns>
        /// <exception cref="KeyNotFoundException">The id is not in the table.</exception>
        public string this[string id] => _texts[id];

        /// <summary>Looks up the text for an id.</summary>
        /// <param name="id">The recovered text id.</param>
        /// <param name="text">The text, when found.</param>
        /// <returns><see langword="true"/> when the id is in the table.</returns>
        public bool TryGetText(string id, out string text)
        {
            if (_texts.TryGetValue(id, out string? found))
            {
                text = found;
                return true;
            }

            text = string.Empty;
            return false;
        }

        /// <summary>
        /// Maps a text character back to its Windows-1252 byte, the code the recovered bitmap fonts
        /// index their glyphs by.
        /// </summary>
        /// <param name="character">The character.</param>
        /// <param name="value">The byte, when the code page has the character.</param>
        /// <returns><see langword="true"/> when the code page has the character.</returns>
        public static bool TryEncode(char character, out byte value)
        {
            int high = Array.IndexOf(Windows1252High, character);
            if (high >= 0)
            {
                value = (byte)(0x80 + high);
                return true;
            }

            if (character is <= (char)0xFF and not (>= '\u0080' and <= '\u009F'))
            {
                value = (byte)character;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>Reads a string table.</summary>
        /// <param name="stream">The table's bytes.</param>
        /// <returns>The table.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidDataException">The data is not a recovered string table.</exception>
        public static StringTable Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            ReadOnlySpan<byte> data = copy.GetBuffer().AsSpan(0, (int)copy.Length);
            if (data.Length < HeaderSize + EntrySize || !data[..4].SequenceEqual("str2"u8))
            {
                throw new InvalidDataException("The data is not a str2 string table.");
            }

            int count = ReadInt(data, 8);
            int valueIndex = ReadInt(data, 12);
            int keyIndex = HeaderSize + EntrySize;
            if (count < 0
                || keyIndex + ((long)count * EntrySize) > data.Length
                || valueIndex < 0
                || valueIndex + ((long)count * EntrySize) > data.Length)
            {
                throw new InvalidDataException("The string table's indexes lie outside its data.");
            }

            string language = ReadText(data, ReadInt(data, HeaderSize + 4), ReadInt(data, HeaderSize));
            Dictionary<string, string> texts = new(count, StringComparer.Ordinal);
            for (int index = 0; index < count; index++)
            {
                string key = ReadText(data, keyIndex + (index * EntrySize));
                texts[key] = ReadText(data, valueIndex + (index * EntrySize));
            }

            return new StringTable(language, texts);
        }

        private static int ReadInt(ReadOnlySpan<byte> data, int offset)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
        }

        private static string ReadText(ReadOnlySpan<byte> data, int entryOffset)
        {
            return ReadText(data, ReadInt(data, entryOffset), ReadInt(data, entryOffset + 4));
        }

        private static string ReadText(ReadOnlySpan<byte> data, int offset, int length)
        {
            if (offset < 0 || length < 0 || (long)offset + length > data.Length)
            {
                throw new InvalidDataException("A string table entry lies outside its data.");
            }

            StringBuilder text = new(length);
            foreach (byte value in data.Slice(offset, length))
            {
                _ = text.Append(value is >= 0x80 and <= 0x9F ? Windows1252High[value - 0x80] : (char)value);
            }

            return text.ToString();
        }
    }
}
