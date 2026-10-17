using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mooh.Core.Content
{
    /// <summary>Reads a UTF-8 JSON language and string dictionary.</summary>
    public sealed class StringTable
    {
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
                Dictionary<string, string> texts = [with(StringComparer.Ordinal)];
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
