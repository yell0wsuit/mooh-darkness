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

            LevelDocument document;
            try
            {
                document = JsonSerializer.Deserialize(
                    json,
                    LevelJsonSerializerContext.Default.LevelDocument)
                    ?? throw new JsonException("The document is null.");
            }
            catch (JsonException exception)
            {
                throw new LevelValidationException(
                    $"{sourceName}: invalid JSON level document.",
                    exception);
            }

            Validate(document, sourceName);
            return document;
        }

        /// <summary>Loads a level document from a UTF-8 JSON stream.</summary>
        /// <param name="json">The JSON stream.</param>
        /// <param name="sourceName">A name used to identify the source in errors.</param>
        /// <returns>The deserialized archival document.</returns>
        public static LevelDocument Load(Stream json, string sourceName)
        {
            ArgumentNullException.ThrowIfNull(json);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

            LevelDocument document;
            try
            {
                document = JsonSerializer.Deserialize(
                    json,
                    LevelJsonSerializerContext.Default.LevelDocument)
                    ?? throw new JsonException("The document is null.");
            }
            catch (JsonException exception)
            {
                throw new LevelValidationException(
                    $"{sourceName}: invalid JSON level document.",
                    exception);
            }

            Validate(document, sourceName);
            return document;
        }

        private static void Validate(LevelDocument document, string sourceName)
        {
            if (document.FormatVersion != 1)
            {
                throw new LevelValidationException(
                    $"{sourceName}: formatVersion must be 1; found {document.FormatVersion}.");
            }

            if (document.Size.Width <= 0)
            {
                throw new LevelValidationException(
                    $"{sourceName}: size.width must be positive; found {document.Size.Width}.");
            }

            if (document.Size.Height <= 0)
            {
                throw new LevelValidationException(
                    $"{sourceName}: size.height must be positive; found {document.Size.Height}.");
            }

            if (document.EditorPreview.Width <= 0)
            {
                throw new LevelValidationException(
                    $"{sourceName}: editorPreview.width must be positive; found {document.EditorPreview.Width}.");
            }

            if (document.EditorPreview.Height <= 0)
            {
                throw new LevelValidationException(
                    $"{sourceName}: editorPreview.height must be positive; found {document.EditorPreview.Height}.");
            }

            if (document.Objects is null)
            {
                throw new LevelValidationException($"{sourceName}: objects cannot be null.");
            }

            int playerCount = 0;
            for (int index = 0; index < document.Objects.Count; index++)
            {
                LevelObject levelObject = document.Objects[index];
                int expectedId = index + 1;
                if (levelObject.Id != expectedId)
                {
                    throw new LevelValidationException(
                        $"{sourceName}: objects[{index}].id must be {expectedId}; found {levelObject.Id}.");
                }

                if (string.IsNullOrWhiteSpace(levelObject.Type))
                {
                    throw new LevelValidationException(
                        $"{sourceName}: objects[{levelObject.Id}].type cannot be blank.");
                }

                if (string.Equals(levelObject.Type, "OPlayer", StringComparison.Ordinal))
                {
                    playerCount++;
                }

                if (levelObject.EditorIndex < 0)
                {
                    throw new LevelValidationException(
                        $"{sourceName}: objects[{levelObject.Id}].editorIndex cannot be negative; found {levelObject.EditorIndex}.");
                }

                if (levelObject.Position.X < 0 || levelObject.Position.X >= document.Size.Width)
                {
                    throw new LevelValidationException(
                        $"{sourceName}: objects[{levelObject.Id}].position.x must be in [0,{document.Size.Width}); found {levelObject.Position.X}.");
                }

                if (levelObject.Position.Y < 0 || levelObject.Position.Y >= document.Size.Height)
                {
                    throw new LevelValidationException(
                        $"{sourceName}: objects[{levelObject.Id}].position.y must be in [0,{document.Size.Height}); found {levelObject.Position.Y}.");
                }

                if (levelObject.SavedState is null)
                {
                    throw new LevelValidationException(
                        $"{sourceName}: objects[{levelObject.Id}].savedState cannot be null.");
                }

                if (levelObject.Properties is null)
                {
                    throw new LevelValidationException(
                        $"{sourceName}: objects[{levelObject.Id}].properties cannot be null.");
                }
            }

            if (playerCount != 1)
            {
                throw new LevelValidationException(
                    $"{sourceName}: objects.type OPlayer must occur exactly once; found {playerCount}.");
            }
        }
    }
}
