using System;
using System.IO;
using System.Linq;

using Mooh.Core.Session;

using Xunit;

namespace Mooh.Core.Tests.Session
{
    /// <summary>Verifies persistent progress, checkpoint metadata, options, and native high-score ordering.</summary>
    public sealed class PlayerProfileTests
    {
        /// <summary>Every difficulty owns its own four-row table.</summary>
        [Fact]
        public void HighScoresHaveThreeIndependentFourEntryTables()
        {
            PlayerProfile profile = new();

            Assert.Equal(3, profile.HighScores.Count);
            Assert.All(profile.HighScores.SelectMany(table => table), entry => Assert.Equal("Empty", entry.Name));
            Assert.All(profile.HighScores, table => Assert.Equal(4, table.Count));
            Assert.Equal([20, 15, 10, 5], profile.HighScores[0].Select(entry => entry.Score));
            Assert.Equal([40, 35, 30, 25], profile.HighScores[1].Select(entry => entry.Score));
            Assert.Equal([60, 55, 50, 45], profile.HighScores[2].Select(entry => entry.Score));
        }

        /// <summary>New equal scores precede existing rows regardless of their level metadata.</summary>
        [Fact]
        public void NewEqualScoreRanksBeforeOlderEntries()
        {
            PlayerProfile profile = new();
            Assert.Equal(0, profile.SubmitHighScore(0, "Older", 1000, 120));
            Assert.Equal(0, profile.SubmitHighScore(0, "Newer", 1000, 1));

            Assert.Equal("Newer", profile.HighScores[0][0].Name);
            Assert.Equal("Older", profile.HighScores[0][1].Name);
            Assert.Equal(4, profile.HighScores[0].Count);
            Assert.Equal(40, profile.HighScores[1][0].Score);
        }

        /// <summary>The native name limit is fourteen characters and the last entered name is remembered.</summary>
        [Fact]
        public void HighScoreNamesUseTheNativeLimitAndAreRemembered()
        {
            PlayerProfile profile = new();

            _ = profile.SubmitHighScore(0, "abcdefghijklmnop", 1000, 1);

            Assert.Equal("abcdefghijklmn", profile.HighScores[0][0].Name);
            Assert.Equal("abcdefghijklmn", profile.LastPlayerName);
        }

        /// <summary>A score below the final row does not modify the table.</summary>
        [Fact]
        public void NonQualifyingScoreDoesNotChangeTheTable()
        {
            PlayerProfile profile = new();
            HighScoreEntry[] before = [.. profile.HighScores[0]];

            Assert.Null(profile.SubmitHighScore(0, "Low", 4, 149));
            Assert.Equal(before, profile.HighScores[0]);
        }

        /// <summary>Progress finds the first gap and preserves previously passed levels.</summary>
        [Fact]
        public void ProgressUnlocksTheFirstUnpassedLevel()
        {
            PlayerProfile profile = new();
            profile.MarkLevelPassed(2);
            profile.MarkLevelPassed(0);
            profile.MarkLevelPassed(0);

            Assert.Equal(1, profile.FirstUnpassedLevel(150));
            Assert.True(profile.IsLevelUnlocked(0, 150));
            Assert.True(profile.IsLevelUnlocked(1, 150));
            Assert.True(profile.IsLevelUnlocked(2, 150));
            Assert.False(profile.IsLevelUnlocked(3, 150));
            Assert.Equal(2, profile.PassedLevels.Count);
        }

        /// <summary>Options, pass flags, checkpoint metadata and scores survive an atomic save.</summary>
        [Fact]
        public void ProfileRoundTripsAllPersistentFields()
        {
            using TemporaryDirectory directory = new();
            PlayerProfileStore store = new(directory.Path);
            PlayerProfile profile = new()
            {
                Options = new PlayerOptions { SoundVolume = 0.375, MusicVolume = 0.75, Fullscreen = true, FullColor = false },
                SavedGame = new ResumeCheckpoint(17, 2, 12345)
            };
            profile.MarkLevelPassed(16);
            _ = profile.SubmitHighScore(2, "Chicken", 12345, 17);

            store.Save(profile);
            PlayerProfile restored = store.Load();

            Assert.Equal(0.375, restored.Options.SoundVolume);
            Assert.Equal(0.75, restored.Options.MusicVolume);
            Assert.True(restored.Options.Fullscreen);
            Assert.False(restored.Options.FullColor);
            Assert.Equal(profile.SavedGame, restored.SavedGame);
            Assert.Equal([16], restored.PassedLevels);
            Assert.Equal(profile.HighScores[2], restored.HighScores[2]);
            Assert.Equal("Chicken", restored.LastPlayerName);
            _ = Assert.Single(Directory.GetFiles(directory.Path));
        }

        /// <summary>Missing and corrupted files leave a usable fresh profile.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("{")]
        [InlineData("null")]
        [InlineData("[]")]
        public void MissingOrCorruptFilesReturnDefaults(string? contents)
        {
            using TemporaryDirectory directory = new();
            PlayerProfileStore store = new(directory.Path);
            if (contents is not null)
            {
                File.WriteAllText(store.FilePath, contents);
            }

            PlayerProfile profile = store.Load();

            Assert.Equal(3, profile.HighScores.Count);
            Assert.All(profile.HighScores.SelectMany(table => table), entry => Assert.Equal("Empty", entry.Name));
            Assert.All(profile.HighScores, table => Assert.Equal(4, table.Count));
            Assert.Empty(profile.PassedLevels);
            Assert.Null(profile.SavedGame);
        }

        /// <summary>Structurally valid but incomplete user data is normalized without crashing.</summary>
        [Fact]
        public void MalformedFieldsAreNormalized()
        {
            using TemporaryDirectory directory = new();
            PlayerProfileStore store = new(directory.Path);
            File.WriteAllText(store.FilePath,
                                     /*lang=json,strict*/
                                     "{\"options\":{\"soundVolume\":-5,\"musicVolume\":2},\"passedLevels\":[-1,2,2],\"savedGame\":{\"levelIndex\":-1,\"difficulty\":8,\"totalScore\":-5},\"highScores\":null}");

            PlayerProfile profile = store.Load();

            Assert.Equal(0, profile.Options.SoundVolume);
            Assert.Equal(1, profile.Options.MusicVolume);
            Assert.Equal([2], profile.PassedLevels);
            Assert.Null(profile.SavedGame);
            Assert.All(profile.HighScores, table => Assert.Equal(4, table.Count));
        }

        /// <summary>Replacing a profile updates the target without leaving temporary files behind.</summary>
        [Fact]
        public void RepeatedSavesReplaceThePreviousProfile()
        {
            using TemporaryDirectory directory = new();
            PlayerProfileStore store = new(directory.Path);
            PlayerProfile profile = new();
            store.Save(profile);
            profile.MarkLevelPassed(4);

            store.Save(profile);

            Assert.Equal([4], store.Load().PassedLevels);
            _ = Assert.Single(Directory.GetFiles(directory.Path));
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mooh-profile-tests-" + Guid.NewGuid().ToString("N"));
                _ = Directory.CreateDirectory(Path);
            }

            public string Path
            {
                get;
            }

            public void Dispose()
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
