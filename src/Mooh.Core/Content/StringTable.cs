using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mooh.Core.Content
{
    /// <summary>Reads a UTF-8 JSON language and string dictionary.</summary>
    public sealed class StringTable
    {
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
        /// <exception cref="InvalidDataException">The JSON string table is malformed.</exception>
        public static StringTable Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            try
            {
                using JsonDocument document = JsonDocument.Parse(stream);
                JsonElement root = document.RootElement;
                string language = root.GetProperty("language").GetString()
                    ?? throw new InvalidDataException("The string table requires a language.");
                Dictionary<string, string> texts = new(StringComparer.Ordinal);
                foreach (JsonProperty entry in root.GetProperty("strings").EnumerateObject())
                {
                    if (!texts.TryAdd(entry.Name, entry.Value.GetString()
                        ?? throw new InvalidDataException($"String '{entry.Name}' is null.")))
                    {
                        throw new InvalidDataException($"String '{entry.Name}' is duplicated.");
                    }
                }
                return new StringTable(language, texts);
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                throw new InvalidDataException("The string table contains invalid JSON.", error);
            }
        }
    }
}
