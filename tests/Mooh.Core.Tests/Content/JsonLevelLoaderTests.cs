using System.Text.Json;

using Mooh.Core.Content;

using Xunit;

namespace Mooh.Core.Tests.Content
{
    /// <summary>Verifies deserialization of archival JSON level documents.</summary>
    public sealed class JsonLevelLoaderTests
    {
        /// <summary>Verifies all supported version-one fields are preserved.</summary>
        [Fact]
        public void VersionOneDocumentIsDeserialized()
        {
            const string Json = /*lang=json,strict*/ """
                {
                  "formatVersion": 1,
                  "source": { "file": "sample.lvl", "legacyFormat": "lvl002" },
                  "size": { "width": 4, "height": 3 },
                  "editorPreview": { "x": -5, "y": 7, "width": 320, "height": 200 },
                  "objects": [
                    {
                      "id": 1,
                      "type": "OPlayer",
                      "editorIndex": 2,
                      "position": { "x": 1, "y": 2 },
                      "savedState": [
                        { "class": "PlayerState", "value": { "facing": "right" } }
                      ],
                      "properties": { "speed": 1.5 }
                    }
                  ]
                }
                """;

            LevelDocument document = JsonLevelLoader.Load(Json, "inline.json");

            Assert.Equal(1, document.FormatVersion);
            Assert.Equal("sample.lvl", document.Source?.File);
            Assert.Equal("lvl002", document.Source?.LegacyFormat);
            Assert.Equal(new LevelSize(4, 3), document.Size);
            Assert.Equal(new EditorPreview(-5, 7, 320, 200), document.EditorPreview);
            LevelObject levelObject = Assert.Single(document.Objects!);
            Assert.Equal(1, levelObject.Id);
            Assert.Equal("OPlayer", levelObject.Type);
            Assert.Equal(2, levelObject.EditorIndex);
            Assert.Equal(new LevelPosition(1, 2), levelObject.Position);
            LevelSavedState state = Assert.Single(levelObject.SavedState!);
            Assert.Equal("PlayerState", state.Class);
            Assert.Equal("right", state.Value.GetProperty("facing").GetString());
            JsonElement speed = Assert.Single(levelObject.Properties!).Value;
            Assert.Equal(1.5, speed.GetDouble());
        }

        /// <summary>Verifies legacy provenance can be absent.</summary>
        [Fact]
        public void SourceMetadataIsOptional()
        {
            const string Json = /*lang=json,strict*/ """
                {
                  "formatVersion": 1,
                  "size": { "width": 1, "height": 1 },
                  "editorPreview": { "x": 0, "y": 0, "width": 1, "height": 1 },
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

            LevelDocument document = JsonLevelLoader.Load(Json, "without-source.json");

            Assert.Null(document.Source);
        }
    }
}
