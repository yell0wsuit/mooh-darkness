using System;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Session;
using Mooh.Core.Simulation;

namespace Mooh.SimulationRunner
{
    /// <summary>
    /// Drives a <see cref="GameSession"/> from wall-clock time and held directional input.
    /// </summary>
    /// <remarks>
    /// The elapsed time handed to the simulation is clamped, because a browser throttles its
    /// frame loop when the tab is hidden. Without the clamp, returning to the tab would advance
    /// the deterministic simulation by the whole idle period in one step.
    /// </remarks>
    public sealed class PlaySession
    {
        private static readonly TimeSpan MaximumFrameStep = TimeSpan.FromMilliseconds(100);

        private readonly TimeProvider _timeProvider;
        private long _lastTimestamp;

        /// <summary>Starts a play session on a loaded pack.</summary>
        /// <param name="pack">The loaded archival pack.</param>
        /// <param name="startingLevelIndex">The zero-based index of the first level to play.</param>
        /// <param name="timeProvider">The clock used to measure frame intervals.</param>
        /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
        public PlaySession(
            LoadedLevelPack pack,
            int startingLevelIndex = 0,
            TimeProvider? timeProvider = null)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            Session = new GameSession(pack, DifficultySpeeds.Standard, startingLevelIndex);
            _lastTimestamp = _timeProvider.GetTimestamp();
        }

        /// <summary>Gets the underlying pack session.</summary>
        public GameSession Session { get; }

        /// <summary>
        /// Advances the simulation by the time elapsed since the previous frame and renders it.
        /// A decided level stops advancing but still renders, so the outcome stays visible.
        /// </summary>
        /// <param name="input">The directions currently held down.</param>
        /// <returns>The rendered frame.</returns>
        public PlayFrame Advance(PlayerInputState input)
        {
            long now = _timeProvider.GetTimestamp();
            TimeSpan elapsed = _timeProvider.GetElapsedTime(_lastTimestamp, now);
            _lastTimestamp = now;

            if (Session.State == GameSessionState.Playing)
            {
                Session.SetPlayerInput(input);
                _ = Session.Advance(elapsed > MaximumFrameStep ? MaximumFrameStep : elapsed);
            }

            return Render();
        }

        /// <summary>Replays the current level, discarding the current attempt.</summary>
        public void RestartLevel()
        {
            if (Session.State is not (GameSessionState.CampaignCompleted
                or GameSessionState.LevelCompleted))
            {
                Session.RestartLevel();
            }

            ResetClock();
        }

        /// <summary>Enters the next level when the current one has been completed.</summary>
        public void AdvanceToNextLevel()
        {
            if (Session.State == GameSessionState.LevelCompleted)
            {
                Session.AdvanceToNextLevel();
            }

            ResetClock();
        }

        /// <summary>Renders the current state without advancing time.</summary>
        /// <returns>The rendered frame.</returns>
        public PlayFrame Render()
        {
            PlayerBody? body = Session.Level.World.Player?.PlayerBody;
            return new PlayFrame(
                LiveBoardRenderer.Render(Session.Level.World),
                Session.LevelIndex + 1,
                Session.LevelCount,
                Session.TotalScore,
                Session.Level.World.Player?.HealthBody?.Current ?? 0,
                body?.Progress.Grenades ?? 0,
                Session.Level.World.Entities.Count(
                    entity => entity.Interactions.HasFlag(InteractionMask.LevelEndLock)),
                Session.State.ToString(),
                (body?.Reaction.Kind ?? PlayerReactionKind.Normal).ToString());
        }

        /// <summary>
        /// Discards the interval spanning a level change, so the first frame of a new level does
        /// not advance by the time the previous level's end screen was on display.
        /// </summary>
        private void ResetClock()
        {
            _lastTimestamp = _timeProvider.GetTimestamp();
        }
    }
}
