using System;
using System.Collections.Generic;
using System.Linq;

namespace Mooh.Core.Session
{
    /// <summary>Stores completed levels, player options, high scores, and saved-run metadata.</summary>
    public sealed class PlayerProfile
    {
        /// <summary>The three original difficulty profiles.</summary>
        public const int DifficultyCount = 3;

        /// <summary>The original number of rows in each high-score table.</summary>
        public const int HighScoreCount = 4;

        /// <summary>Gets or sets the persistent data version.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Gets or sets the audio and window options.</summary>
        public PlayerOptions Options { get; set; } = new();

        /// <summary>Gets or sets the zero-based levels completed in the shipped pack.</summary>
        public List<int> PassedLevels { get; set; } = [];

        /// <summary>Gets or sets the name remembered for the next qualifying score.</summary>
        public string LastPlayerName { get; set; } = "Player";

        /// <summary>Gets or sets saved-run metadata for restarting the saved level.</summary>
        public ResumeCheckpoint? SavedGame
        {
            get; set;
        }

        /// <summary>Gets or sets one score table per difficulty, ordered from highest score to lowest.</summary>
        public List<List<HighScoreEntry>> HighScores { get; set; } = CreateDefaultTables();

        /// <summary>Marks a level passed without duplicating its completion flag.</summary>
        /// <param name="levelIndex">The zero-based completed level.</param>
        public void MarkLevelPassed(int levelIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(levelIndex);
            if (!PassedLevels.Contains(levelIndex))
            {
                PassedLevels.Add(levelIndex);
                PassedLevels.Sort();
            }
        }

        /// <summary>Finds the first uncompleted level, retaining the last level after the entire pack is passed.</summary>
        /// <param name="levelCount">The number of levels available.</param>
        /// <returns>The zero-based level to select.</returns>
        public int FirstUnpassedLevel(int levelCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levelCount);
            for (int index = 0; index < levelCount; index++)
            {
                if (!PassedLevels.Contains(index))
                {
                    return index;
                }
            }

            return levelCount - 1;
        }

        /// <summary>Reports whether a level has been passed or is available as the next unpassed level.</summary>
        /// <param name="levelIndex">The requested level.</param>
        /// <param name="levelCount">The pack's level count.</param>
        /// <returns>Whether the level can be selected.</returns>
        public bool IsLevelUnlocked(int levelIndex, int levelCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levelCount);
            return levelIndex >= 0 && levelIndex < levelCount
                && (PassedLevels.Contains(levelIndex) || levelIndex <= FirstUnpassedLevel(levelCount));
        }

        /// <summary>Inserts a qualifying score. New equal scores rank before existing entries; level metadata does not break ties.</summary>
        /// <param name="difficulty">The zero-based difficulty table.</param>
        /// <param name="name">The player name, limited to the original fourteen characters.</param>
        /// <param name="score">The cumulative run score.</param>
        /// <param name="levelIndex">The last level reached.</param>
        /// <returns>The zero-based inserted rank, or null when the score does not qualify.</returns>
        public int? SubmitHighScore(int difficulty, string name, int score, int levelIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(difficulty);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(difficulty, DifficultyCount);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentOutOfRangeException.ThrowIfNegative(score);
            ArgumentOutOfRangeException.ThrowIfNegative(levelIndex);
            Normalize();
            List<HighScoreEntry> table = HighScores[difficulty];
            int rank = table.FindIndex(entry => score >= entry.Score);
            if (rank < 0)
            {
                return null;
            }

            LastPlayerName = NormalizeName(name);
            table.Insert(rank, new HighScoreEntry(LastPlayerName, score, levelIndex));
            table.RemoveAt(HighScoreCount);
            return rank;
        }

        internal void Normalize()
        {
            LastPlayerName = NormalizeName(LastPlayerName);
            Options ??= new PlayerOptions();
            Options.SoundVolume = NormalizeVolume(Options.SoundVolume);
            Options.MusicVolume = NormalizeVolume(Options.MusicVolume);
            PassedLevels = [.. (PassedLevels ?? []).Where(index => index >= 0).Distinct().Order()];
            if (SavedGame is { } checkpoint
                && (checkpoint.LevelIndex < 0 || checkpoint.Difficulty is < 0 or >= DifficultyCount || checkpoint.TotalScore < 0))
            {
                SavedGame = null;
            }

            List<List<HighScoreEntry>> normalized = [];
            for (int difficulty = 0; difficulty < DifficultyCount; difficulty++)
            {
                IEnumerable<HighScoreEntry> entries = HighScores is not null && difficulty < HighScores.Count
                    ? HighScores[difficulty] ?? [] : [];
                List<HighScoreEntry> table = [.. entries.Where(entry => entry is not null && entry.Score >= 0 && entry.LevelIndex >= 0)
                    .OrderByDescending(entry => entry.Score)
                    .Take(HighScoreCount)
                    .Select(entry => entry with { Name = NormalizeName(entry.Name) })];
                List<HighScoreEntry> defaults = CreateDefaultTable(difficulty);
                while (table.Count < HighScoreCount)
                {
                    table.Add(defaults[table.Count]);
                }

                normalized.Add([.. table.OrderByDescending(entry => entry.Score)]);
            }

            HighScores = normalized;
        }

        private static double NormalizeVolume(double value)
        {
            return double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1;
        }

        private static string NormalizeName(string? value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "Player" : value;
            return name.Length > 14 ? name[..14] : name;
        }

        private static List<List<HighScoreEntry>> CreateDefaultTables()
        {
            return [.. Enumerable.Range(0, DifficultyCount).Select(CreateDefaultTable)];
        }

        private static List<HighScoreEntry> CreateDefaultTable(int difficulty)
        {
            return [.. Enumerable.Range(0, HighScoreCount)
                .Select(rank => new HighScoreEntry("Empty", 5 * ((HighScoreCount * (difficulty + 1)) - rank), 0))];
        }
    }

    /// <summary>Stores normalized audio levels and the preferred window mode.</summary>
    public sealed class PlayerOptions
    {
        /// <summary>Gets or sets effects volume, from zero to one.</summary>
        public double SoundVolume { get; set; } = 1;

        /// <summary>Gets or sets music volume, from zero to one.</summary>
        public double MusicVolume { get; set; } = 1;

        /// <summary>Gets or sets whether full-color output is preferred over the original 16-bit mode.</summary>
        public bool FullColor { get; set; } = true;

        /// <summary>Gets or sets whether the game should open fullscreen.</summary>
        public bool Fullscreen
        {
            get; set;
        }
    }

    /// <summary>Captures the level, difficulty and cumulative score restored at the start of a continued level.</summary>
    /// <param name="LevelIndex">The zero-based current level.</param>
    /// <param name="Difficulty">The zero-based difficulty.</param>
    /// <param name="TotalScore">The cumulative score at save time.</param>
    public sealed record ResumeCheckpoint(int LevelIndex, int Difficulty, int TotalScore);

    /// <summary>Stores a player's qualifying cumulative score and last level reached.</summary>
    /// <param name="Name">The player name.</param>
    /// <param name="Score">The cumulative score.</param>
    /// <param name="LevelIndex">The zero-based last level reached.</param>
    public sealed record HighScoreEntry(string Name, int Score, int LevelIndex);
}
