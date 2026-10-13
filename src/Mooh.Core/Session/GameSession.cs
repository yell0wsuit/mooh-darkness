using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.Core.Session
{
    /// <summary>
    /// Plays a level pack in order, reproducing the recovered progression around
    /// <c>GameInit</c>, <c>GameLvlStart</c> and <c>GameReStart</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original has no lives. A loss sets <c>GST_NoPlay</c>, whose <c>bGame</c> flag is false,
    /// so <c>Main.GamePlayCFunc</c> saves the game and returns to the menu; the level is replayed
    /// only on request through <c>GameReStart</c>. A win sets <c>GST_LvlNext</c>, which marks the
    /// level passed, advances the pack index and re-enters through <c>GameInit</c>.
    /// </para>
    /// <para>
    /// Both paths run <c>GameInit</c>, so every level starts from the same reset: full health,
    /// the default weapon loadout, no bonuses and no keys. Materializing the level afresh
    /// reproduces that reset, because all of it lives on the player entity.
    /// </para>
    /// <para>
    /// The cumulative score is the exception. It lives outside the game module in the high-score
    /// host, where <c>HScoreScoreAdd</c> only ever adds and <c>HScoreLvlNew</c> is a level marker
    /// that never subtracts, so points earned before a restart are kept.
    /// </para>
    /// </remarks>
    public sealed class GameSession
    {
        private readonly DifficultySpeeds _difficultySpeeds;
        private readonly Direction _gravityDirection;
        private readonly IReadOnlyList<LevelDocument> _levels;
        private int _bankedScore;

        /// <summary>Starts a session on a level of a loaded pack.</summary>
        /// <param name="pack">The loaded archival pack.</param>
        /// <param name="difficultySpeeds">The recovered difficulty profile used for every level.</param>
        /// <param name="startingLevelIndex">The zero-based index of the first level to play.</param>
        /// <param name="gravityDirection">The initial cardinal gravity direction for every level.</param>
        /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The pack contains no levels.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="startingLevelIndex"/> is outside the pack, or
        /// <paramref name="gravityDirection"/> is undefined.
        /// </exception>
        public GameSession(
            LoadedLevelPack pack,
            DifficultySpeeds difficultySpeeds,
            int startingLevelIndex = 0,
            Direction gravityDirection = Direction.Down)
        {
            ArgumentNullException.ThrowIfNull(pack);
            _difficultySpeeds = difficultySpeeds ?? throw new ArgumentNullException(nameof(difficultySpeeds));
            _levels = pack.Levels ?? throw new ArgumentException("The pack has no levels.", nameof(pack));
            if (_levels.Count == 0)
            {
                throw new ArgumentException("The pack has no levels.", nameof(pack));
            }

            ArgumentOutOfRangeException.ThrowIfNegative(startingLevelIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startingLevelIndex, _levels.Count);
            if (!Enum.IsDefined(gravityDirection))
            {
                throw new ArgumentOutOfRangeException(nameof(gravityDirection), gravityDirection, null);
            }

            _gravityDirection = gravityDirection;
            LevelIndex = startingLevelIndex;
            Level = MaterializeCurrentLevel();
        }

        /// <summary>Gets the zero-based index of the level being played.</summary>
        public int LevelIndex { get; private set; }

        /// <summary>Gets the number of levels in the pack.</summary>
        public int LevelCount => _levels.Count;

        /// <summary>Gets the live level.</summary>
        public MaterializedLevel Level { get; private set; }

        /// <summary>Gets the live simulation for the current level.</summary>
        public SimulationWorld Simulation => Level.Simulation;

        /// <summary>Gets what the session is currently waiting for.</summary>
        public GameSessionState State { get; private set; } = GameSessionState.Playing;

        /// <summary>Gets the score earned on the current level.</summary>
        public int LevelScore => Level.World.Player?.PlayerBody?.Progress.Score ?? 0;

        /// <summary>Gets the monotonic cumulative score across every level played.</summary>
        public int TotalScore => _bankedScore + LevelScore;

        /// <summary>Replaces the currently held input for the current level's player.</summary>
        /// <param name="input">The latest held cardinal inputs.</param>
        /// <exception cref="InvalidOperationException">The current level has no player.</exception>
        public void SetPlayerInput(PlayerInputState input)
        {
            Simulation.SetPlayerInput(input);
        }

        /// <summary>Advances the current level and decides the session state from its outcome.</summary>
        /// <param name="elapsed">The non-negative elapsed simulation time.</param>
        /// <returns>The ordered events produced by the current level.</returns>
        /// <exception cref="InvalidOperationException">The current level has already been decided.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="elapsed"/> is negative.</exception>
        public IReadOnlyList<SimulationEvent> Advance(TimeSpan elapsed)
        {
            if (State != GameSessionState.Playing)
            {
                throw new InvalidOperationException("The current level has already been decided.");
            }

            IReadOnlyList<SimulationEvent> events = Simulation.Advance(elapsed);
            if (events.Any(item => item is LevelWon))
            {
                State = LevelIndex + 1 < _levels.Count
                    ? GameSessionState.LevelCompleted
                    : GameSessionState.CampaignCompleted;
            }
            else if (events.Any(item => item is LevelLost))
            {
                State = GameSessionState.LevelFailed;
            }

            return events;
        }

        /// <summary>Enters the next level in the pack, applying the recovered per-level reset.</summary>
        /// <exception cref="InvalidOperationException">
        /// The current level has not been completed, or the pack has no further level.
        /// </exception>
        public void AdvanceToNextLevel()
        {
            if (State != GameSessionState.LevelCompleted)
            {
                throw new InvalidOperationException("Only a completed level can be advanced.");
            }

            BankLevelScore();
            LevelIndex++;
            Level = MaterializeCurrentLevel();
            State = GameSessionState.Playing;
        }

        /// <summary>
        /// Replays the current level, matching the recovered <c>GameReStart</c>. The cumulative
        /// score keeps whatever the attempt already earned.
        /// </summary>
        /// <exception cref="InvalidOperationException">The campaign has already been completed.</exception>
        public void RestartLevel()
        {
            if (State is GameSessionState.CampaignCompleted or GameSessionState.LevelCompleted)
            {
                throw new InvalidOperationException("A completed level cannot be replayed.");
            }

            BankLevelScore();
            Level = MaterializeCurrentLevel();
            State = GameSessionState.Playing;
        }

        private void BankLevelScore()
        {
            _bankedScore += LevelScore;
        }

        private MaterializedLevel MaterializeCurrentLevel()
        {
            MaterializedLevel level = LevelMaterializer.Materialize(
                _levels[LevelIndex],
                _gravityDirection,
                _difficultySpeeds);

            // Every level is entered through the recovered GameInit ritual: the player emerges
            // from a door in its start cell.
            _ = level.Simulation.BeginLevel();
            return level;
        }
    }
}
