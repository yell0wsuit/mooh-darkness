using System;
using System.IO;

using Mooh.Core.Content;

using Xunit;

namespace Mooh.Core.Tests.Content
{
    /// <summary>Verifies manifest-driven loading of archival level packs.</summary>
    public sealed class JsonLevelPackLoaderTests : IDisposable
    {
        private readonly string directory = Path.Combine(
            Path.GetTempPath(),
            $"MoohLevelPackTests-{Guid.NewGuid():N}");
        private readonly string outsideFile = Path.Combine(
            Path.GetTempPath(),
            $"MoohOutsideLevel-{Guid.NewGuid():N}.json");

        /// <summary>Verifies levels are loaded in manifest order.</summary>
        [Fact]
        public void TwoLevelsAreLoadedInManifestOrder()
        {
            _ = Directory.CreateDirectory(directory);
            WriteFile("manifest.json", ManifestJson(
                """
                { "order": 1, "name": "first", "file": "first.json" },
                { "order": 2, "name": "second", "file": "second.json" }
                """));
            WriteFile("first.json", LevelJson("first.lvl"));
            WriteFile("second.json", LevelJson("second.lvl"));

            LoadedLevelPack pack = JsonLevelPackLoader.Load(
                Path.Combine(directory, "manifest.json"));

            Assert.Equal("archive.lp", pack.Manifest.Source?.Archive);
            Assert.Equal("test pack", pack.Manifest.Source?.LegacyPackName);
            Assert.Equal(2, pack.Manifest.Levels!.Count);
            Assert.Equal("first", pack.Manifest.Levels[0].Name);
            Assert.Equal("second", pack.Manifest.Levels[1].Name);
            Assert.Equal("first.lvl", pack.Levels[0].Source?.File);
            Assert.Equal("second.lvl", pack.Levels[1].Source?.File);
        }

        /// <summary>Verifies only manifest format version one is supported.</summary>
        [Fact]
        public void UnsupportedManifestVersionIsRejected()
        {
            _ = Directory.CreateDirectory(directory);
            string manifest = ManifestJson(/*lang=json,strict*/ "{ \"order\": 1, \"name\": \"first\", \"file\": \"first.json\" }")
                .Replace("\"formatVersion\": 1", "\"formatVersion\": 2", StringComparison.Ordinal);
            string manifestPath = Path.Combine(directory, "manifest.json");
            WriteFile("manifest.json", manifest);

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelPackLoader.Load(manifestPath));

            Assert.Contains(manifestPath, exception.Message, StringComparison.Ordinal);
            Assert.Contains("formatVersion", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies entry order is contiguous and begins at one.</summary>
        [Fact]
        public void NonContiguousManifestOrderIsRejected()
        {
            _ = Directory.CreateDirectory(directory);
            string manifestPath = Path.Combine(directory, "manifest.json");
            WriteFile("manifest.json", ManifestJson(
                                     /*lang=json,strict*/
                                     "{ \"order\": 2, \"name\": \"first\", \"file\": \"first.json\" }"));

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelPackLoader.Load(manifestPath));

            Assert.Contains(manifestPath, exception.Message, StringComparison.Ordinal);
            Assert.Contains("levels[0].order", exception.Message, StringComparison.Ordinal);
            Assert.Contains("2", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies manifest names and files are unique.</summary>
        [Theory]
        [InlineData("same", "same", "first.json", "second.json", "levels[1].name")]
        [InlineData("first", "second", "same.json", "same.json", "levels[1].file")]
        public void DuplicateManifestNamesOrFilesAreRejected(
            string firstName,
            string secondName,
            string firstFile,
            string secondFile,
            string field)
        {
            _ = Directory.CreateDirectory(directory);
            string manifestPath = Path.Combine(directory, "manifest.json");
            WriteFile("manifest.json", ManifestJson($$"""
                { "order": 1, "name": "{{firstName}}", "file": "{{firstFile}}" },
                { "order": 2, "name": "{{secondName}}", "file": "{{secondFile}}" }
                """));

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelPackLoader.Load(manifestPath));

            Assert.Contains(manifestPath, exception.Message, StringComparison.Ordinal);
            Assert.Contains(field, exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies filenames cannot escape the manifest directory.</summary>
        [Fact]
        public void UnsafeManifestFilenameIsRejectedBeforeOpening()
        {
            _ = Directory.CreateDirectory(directory);
            File.WriteAllText(outsideFile, LevelJson("outside.lvl"));
            string unsafeFile = $"../{Path.GetFileName(outsideFile)}";
            string manifestPath = Path.Combine(directory, "manifest.json");
            WriteFile("manifest.json", ManifestJson(
                $"{{ \"order\": 1, \"name\": \"outside\", \"file\": \"{unsafeFile}\" }}"));

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelPackLoader.Load(manifestPath));

            Assert.Contains(manifestPath, exception.Message, StringComparison.Ordinal);
            Assert.Contains("levels[0].file", exception.Message, StringComparison.Ordinal);
            Assert.Contains("unsafe", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Verifies a manifest cannot reference a missing level file.</summary>
        [Fact]
        public void MissingLevelFileIsRejected()
        {
            _ = Directory.CreateDirectory(directory);
            string manifestPath = Path.Combine(directory, "manifest.json");
            WriteFile("manifest.json", ManifestJson(
                                     /*lang=json,strict*/
                                     "{ \"order\": 1, \"name\": \"missing\", \"file\": \"missing.json\" }"));

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelPackLoader.Load(manifestPath));

            Assert.Contains(manifestPath, exception.Message, StringComparison.Ordinal);
            Assert.Contains("levels[0].file", exception.Message, StringComparison.Ordinal);
            Assert.Contains("missing.json", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies optional legacy source filenames agree with manifest entries.</summary>
        [Fact]
        public void MismatchedLevelSourceFilenameIsRejected()
        {
            _ = Directory.CreateDirectory(directory);
            string manifestPath = Path.Combine(directory, "manifest.json");
            WriteFile("manifest.json", ManifestJson(
                                     /*lang=json,strict*/
                                     "{ \"order\": 1, \"name\": \"first\", \"file\": \"first.json\" }"));
            WriteFile("first.json", LevelJson("different.lvl"));

            LevelValidationException exception = Assert.Throws<LevelValidationException>(
                () => JsonLevelPackLoader.Load(manifestPath));

            Assert.Contains(manifestPath, exception.Message, StringComparison.Ordinal);
            Assert.Contains("levels[0].source.file", exception.Message, StringComparison.Ordinal);
            Assert.Contains("different.lvl", exception.Message, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            if (File.Exists(outsideFile))
            {
                File.Delete(outsideFile);
            }
        }

        private static string ManifestJson(string levels)
        {
            return $$"""
                {
                  "formatVersion": 1,
                  "source": { "archive": "archive.lp", "legacyPackName": "test pack" },
                  "levels": [
                    {{levels}}
                  ]
                }
                """;
        }

        private static string LevelJson(string sourceFile)
        {
            return $$"""
                {
                  "formatVersion": 1,
                  "source": { "file": "{{sourceFile}}", "legacyFormat": "lvl002" },
                  "size": { "width": 2, "height": 2 },
                  "editorPreview": { "x": 0, "y": 0, "width": 10, "height": 10 },
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

        private void WriteFile(string fileName, string contents)
        {
            File.WriteAllText(Path.Combine(directory, fileName), contents);
        }
    }
}
