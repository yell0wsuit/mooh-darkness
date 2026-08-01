using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mooh.Core.Content
{
    /// <summary>Loads manifest-driven archival JSON level packs.</summary>
    public static class JsonLevelPackLoader
    {
        /// <summary>Loads a manifest and each level it lists.</summary>
        /// <param name="manifestPath">The path to the pack manifest.</param>
        /// <returns>The manifest and loaded levels in manifest order.</returns>
        public static LoadedLevelPack Load(string manifestPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

            LevelPackManifest manifest;
            using (FileStream stream = File.OpenRead(manifestPath))
            {
                manifest = JsonSerializer.Deserialize(
                    stream,
                    LevelJsonSerializerContext.Default.LevelPackManifest)
                    ?? throw new LevelValidationException($"{manifestPath}: manifest cannot be null.");
            }

            ValidateManifest(manifest, manifestPath);

            string directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))
                ?? throw new LevelValidationException($"{manifestPath}: manifest directory is unavailable.");
            List<LevelDocument> levels = [];
            for (int index = 0; index < manifest.Levels!.Count; index++)
            {
                LevelManifestEntry entry = manifest.Levels[index];
                string levelPath = Path.GetFullPath(Path.Combine(directory, entry.File!));
                if (!IsWithinDirectory(levelPath, directory))
                {
                    throw new LevelValidationException(
                        $"{manifestPath}: level file '{entry.File}' resolves outside the manifest directory.");
                }

                if (!File.Exists(levelPath))
                {
                    throw new LevelValidationException(
                        $"{manifestPath}: levels[{index}].file does not exist: '{entry.File}'.");
                }

                using FileStream stream = File.OpenRead(levelPath);
                LevelDocument document = JsonLevelLoader.Load(stream, levelPath);
                string? sourceFile = document.Source?.File;
                string expectedSourceFile = Path.ChangeExtension(entry.File!, ".lvl")!;
                if (sourceFile is not null
                    && !string.Equals(sourceFile, expectedSourceFile, StringComparison.Ordinal))
                {
                    throw new LevelValidationException(
                        $"{manifestPath}: levels[{index}].source.file must be '{expectedSourceFile}'; found '{sourceFile}'.");
                }

                levels.Add(document);
            }

            return new LoadedLevelPack(manifest, levels);
        }

        private static void ValidateManifest(LevelPackManifest manifest, string manifestPath)
        {
            if (manifest.FormatVersion != 1)
            {
                throw new LevelValidationException(
                    $"{manifestPath}: formatVersion must be 1; found {manifest.FormatVersion}.");
            }

            if (manifest.Levels is null)
            {
                throw new LevelValidationException($"{manifestPath}: levels cannot be null.");
            }

            HashSet<string> names = [];
            HashSet<string> files = [];
            for (int index = 0; index < manifest.Levels.Count; index++)
            {
                LevelManifestEntry entry = manifest.Levels[index];
                int expectedOrder = index + 1;
                if (entry.Order != expectedOrder)
                {
                    throw new LevelValidationException(
                        $"{manifestPath}: levels[{index}].order must be {expectedOrder}; found {entry.Order}.");
                }

                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    throw new LevelValidationException(
                        $"{manifestPath}: levels[{index}].name cannot be blank.");
                }

                if (!names.Add(entry.Name))
                {
                    throw new LevelValidationException(
                        $"{manifestPath}: levels[{index}].name duplicates '{entry.Name}'.");
                }

                if (string.IsNullOrWhiteSpace(entry.File))
                {
                    throw new LevelValidationException(
                        $"{manifestPath}: levels[{index}].file cannot be blank.");
                }

                if (Path.IsPathFullyQualified(entry.File)
                    || !string.Equals(entry.File, Path.GetFileName(entry.File), StringComparison.Ordinal))
                {
                    throw new LevelValidationException(
                        $"{manifestPath}: levels[{index}].file is unsafe: '{entry.File}'.");
                }

                if (!files.Add(entry.File))
                {
                    throw new LevelValidationException(
                        $"{manifestPath}: levels[{index}].file duplicates '{entry.File}'.");
                }
            }
        }

        private static bool IsWithinDirectory(string path, string directory)
        {
            string relativePath = Path.GetRelativePath(directory, path);
            return !Path.IsPathFullyQualified(relativePath)
                && !string.Equals(relativePath, "..", StringComparison.Ordinal)
                && !relativePath.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                && !relativePath.StartsWith(
                    $"..{Path.AltDirectorySeparatorChar}",
                    StringComparison.Ordinal);
        }
    }
}
