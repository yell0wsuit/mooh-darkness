using System;
using System.IO;
using System.Text.Json;

namespace Mooh.Core.Content
{
    /// <summary>Loads archival JSON level documents without materializing runtime entities.</summary>
    public static class JsonLevelLoader
    {
        /// <summary>Loads a level document from JSON text.</summary>
        /// <param name="json">The JSON document.</param>
        /// <param name="sourceName">A name used to identify the source in errors.</param>
        /// <returns>The deserialized archival document.</returns>
        public static LevelDocument Load(string json, string sourceName)
        {
            ArgumentNullException.ThrowIfNull(json);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

            return JsonSerializer.Deserialize(json, LevelJsonSerializerContext.Default.LevelDocument)
                ?? throw new JsonException($"{sourceName}: document is null.");
        }

        /// <summary>Loads a level document from a UTF-8 JSON stream.</summary>
        /// <param name="json">The JSON stream.</param>
        /// <param name="sourceName">A name used to identify the source in errors.</param>
        /// <returns>The deserialized archival document.</returns>
        public static LevelDocument Load(Stream json, string sourceName)
        {
            ArgumentNullException.ThrowIfNull(json);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

            return JsonSerializer.Deserialize(json, LevelJsonSerializerContext.Default.LevelDocument)
                ?? throw new JsonException($"{sourceName}: document is null.");
        }
    }
}
