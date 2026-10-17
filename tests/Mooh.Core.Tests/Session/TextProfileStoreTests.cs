using Mooh.Core.Session;

using Xunit;

namespace Mooh.Core.Tests.Session
{
    /// <summary>Checks browser-style profile storage against the same normalized save contract.</summary>
    public sealed class TextProfileStoreTests
    {
        /// <summary>A new storage instance restores progress, options and high scores.</summary>
        [Fact]
        public void ProfileRoundTripsThroughTextStorage()
        {
            string? saved = null;
            TextProfileStore store = new(() => saved, value => saved = value);
            PlayerProfile profile = new();
            profile.MarkLevelPassed(4);
            profile.Options.SoundVolume = 0.25;
            _ = profile.SubmitHighScore(0, "Browser", 999, 4);
            store.Save(profile);
            PlayerProfile loaded = new TextProfileStore(() => saved, _ => { }).Load();
            Assert.Contains(4, loaded.PassedLevels);
            Assert.Equal(0.25, loaded.Options.SoundVolume);
            Assert.Equal("Browser", loaded.HighScores[0][0].Name);
        }

        /// <summary>Invalid and future saves return a playable default profile.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("{broken")]
        [InlineData("null")]
        [InlineData("{\"version\":99}")]
        public void InvalidSaveReturnsDefaults(string? saved)
        {
            PlayerProfile profile = new TextProfileStore(() => saved, _ => { }).Load();
            Assert.Empty(profile.PassedLevels);
            Assert.Equal(1, profile.Version);
        }
    }
}
