using System;

using Mooh.Core.Content;

using Xunit;

namespace Mooh.Core.Tests.Content
{
    /// <summary>Verifies validation of individual archival level documents.</summary>
    public sealed class LevelValidationTests
    {
        /// <summary>Verifies only format version one is supported.</summary>
        [Fact]
        public void UnsupportedFormatVersionIsRejected()
        {
            string json = ValidJson.Replace("\"formatVersion\": 1", "\"formatVersion\": 2", StringComparison.Ordinal);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load(json, "unsupported.json"));

            Assert.Contains("unsupported.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains("formatVersion", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies level dimensions must both be present and positive.</summary>
        [Theory]
        [InlineData("\"size\": { \"width\": 2, \"height\": 2 }", "\"size\": { \"width\": 0, \"height\": 2 }", "size.width")]
        [InlineData("\"size\": { \"width\": 2, \"height\": 2 }", "\"size\": { \"width\": 2, \"height\": -1 }", "size.height")]
        [InlineData("\"size\": { \"width\": 2, \"height\": 2 },", "", "size.width")]
        public void MissingOrNonPositiveDimensionsAreRejected(
            string oldValue,
            string newValue,
            string field)
        {
            string json = ValidJson.Replace(oldValue, newValue, StringComparison.Ordinal);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load(json, "dimensions.json"));

            Assert.Contains("dimensions.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains(field, exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies editor preview dimensions must be positive.</summary>
        [Theory]
        [InlineData("\"width\": 100, \"height\": 100", "\"width\": 0, \"height\": 100", "editorPreview.width")]
        [InlineData("\"width\": 100, \"height\": 100", "\"width\": 100, \"height\": -1", "editorPreview.height")]
        public void NonPositiveEditorPreviewDimensionsAreRejected(
            string oldValue,
            string newValue,
            string field)
        {
            string json = ValidJson.Replace(oldValue, newValue, StringComparison.Ordinal);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load(json, "preview.json"));

            Assert.Contains("preview.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains(field, exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies object and extensible-value collections cannot be null.</summary>
        [Theory]
        [InlineData("\"objects\": [", "\"ignoredObjects\": [", "objects")]
        [InlineData("\"savedState\": []", "\"savedState\": null", "objects[1].savedState")]
        [InlineData("\"properties\": {}", "\"properties\": null", "objects[1].properties")]
        public void NullCollectionsAreRejected(string oldValue, string newValue, string field)
        {
            string json = ValidJson.Replace(oldValue, newValue, StringComparison.Ordinal);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load(json, "collections.json"));

            Assert.Contains("collections.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains(field, exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies IDs are contiguous and ordered from one.</summary>
        [Fact]
        public void NonContiguousObjectIdsAreRejected()
        {
            string json = ValidJson.Replace("\"id\": 1", "\"id\": 2", StringComparison.Ordinal);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load(json, "ids.json"));

            Assert.Contains("ids.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains("objects[0].id", exception.Message, StringComparison.Ordinal);
            Assert.Contains("2", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies every object has a nonblank legacy type.</summary>
        [Theory]
        [InlineData("\"OPlayer\"", "\"   \"")]
        [InlineData("\"OPlayer\"", "null")]
        public void BlankObjectTypesAreRejected(string oldValue, string newValue)
        {
            string json = ValidJson.Replace(oldValue, newValue, StringComparison.Ordinal);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load(json, "types.json"));

            Assert.Contains("types.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains("objects[1].type", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies editor indices cannot be negative.</summary>
        [Fact]
        public void NegativeEditorIndicesAreRejected()
        {
            string json = ValidJson.Replace("\"editorIndex\": 0", "\"editorIndex\": -1", StringComparison.Ordinal);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load(json, "indices.json"));

            Assert.Contains("indices.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains("objects[1].editorIndex", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies object coordinates remain within the declared level size.</summary>
        [Theory]
        [InlineData("\"x\": 0, \"y\": 0", "\"x\": -1, \"y\": 0", "position.x")]
        [InlineData("\"x\": 0, \"y\": 0", "\"x\": 2, \"y\": 0", "position.x")]
        [InlineData("\"x\": 0, \"y\": 0", "\"x\": 0, \"y\": -1", "position.y")]
        [InlineData("\"x\": 0, \"y\": 0", "\"x\": 0, \"y\": 2", "position.y")]
        public void OutOfBoundsCoordinatesAreRejected(
            string oldValue,
            string newValue,
            string field)
        {
            string json = ValidJson.Replace(oldValue, newValue, StringComparison.Ordinal);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load(json, "coordinates.json"));

            Assert.Contains("coordinates.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains($"objects[1].{field}", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies a level cannot omit its player.</summary>
        [Fact]
        public void ZeroPlayersAreRejected()
        {
            string json = ValidJson.Replace("OPlayer", "OBlock", StringComparison.Ordinal);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load(json, "zero-player.json"));

            Assert.Contains("zero-player.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains("objects.type OPlayer", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies a level cannot contain multiple players.</summary>
        [Fact]
        public void MultiplePlayersAreRejected()
        {
            const string FirstPlayer = """
                {
                  "id": 1,
                  "type": "OPlayer",
                  "editorIndex": 0,
                  "position": { "x": 1, "y": 1 },
                  "savedState": [],
                  "properties": {}
                },
                """;
            string json = ValidJson
                .Replace("\"id\": 1", "\"id\": 2", StringComparison.Ordinal)
                .Replace("\"objects\": [", $"\"objects\": [{FirstPlayer}", StringComparison.Ordinal);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load(json, "multiple-players.json"));

            Assert.Contains("multiple-players.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains("objects.type OPlayer", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies malformed JSON is reported as contextual level validation.</summary>
        [Fact]
        public void MalformedJsonIsWrappedWithSourceContext()
        {
            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelLoader.Load("{ not-json", "broken-level.json"));

            Assert.Contains("broken-level.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains("JSON", exception.Message, StringComparison.Ordinal);
            _ = Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
        }

        private const string ValidJson = /*lang=json,strict*/ """
            {
              "formatVersion": 1,
              "size": { "width": 2, "height": 2 },
              "editorPreview": { "x": 0, "y": 0, "width": 100, "height": 100 },
              "objects": [
                {
                  "id": 1,
                  "type": "OPlayer",
                  "editorIndex": 0,
                  "position": { "x": 0, "y": 0 },
                  "savedState": [],
                  "properties": {}
                }
              ]
            }
            """;
    }
}
