using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Grid;
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

        private readonly Dictionary<GridPosition, TimeSpan> _blastCells = [];
        private readonly TimeProvider _timeProvider;
        private long _lastTimestamp;
        private TimeSpan _simulationTime;

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
                TimeSpan step = elapsed > MaximumFrameStep ? MaximumFrameStep : elapsed;
                IReadOnlyList<SimulationEvent> events = Session.Advance(step);
                _simulationTime += step;
                RecordBlastCells(events);
            }

            return Render();
        }

        /// <summary>
        /// Keeps each reached blast cell drawable for as long as its recovered lock lasts. An
        /// explosion is a timed cell effect rather than an entity, so it has nothing to render
        /// from the world itself. The reach time is taken as the end of the step that reported it,
        /// which is accurate to the frame interval.
        /// </summary>
        private void RecordBlastCells(IReadOnlyList<SimulationEvent> events)
        {
            WorldGrid world = Session.Level.World;
            foreach (SimulationEvent value in events)
            {
                if (value is ExplosionCellReached reached && ShowsBlast(world, reached.Position))
                {
                    _blastCells[reached.Position] =
                        _simulationTime + SimulationWorld.ExplosionCellLockDuration;
                }
            }

            foreach (GridPosition cell in _blastCells
                .Where(entry => entry.Value <= _simulationTime)
                .Select(entry => entry.Key)
                .ToList())
            {
                _ = _blastCells.Remove(cell);
            }
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
                LiveBoardRenderer.RenderSprites(Session.Level.World, _blastCells.Keys),
                Session.Level.World.Width,
                Session.Level.World.Height,
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
        /// Applies the recovered gate on the blast graphic. <c>Game._ObjExplWrd</c> destroys the
        /// cell's <c>CNT_CMaskCellCanExplode</c> carriers and locks the cell unconditionally, but
        /// reaches <c>SfxExplAdd1x1</c> only when
        /// <c>not CellObjCheck(c, 0, CNT_OrMaskShadow, True)</c> — that is, only when nothing
        /// still and non-shadow survived. A cell an immortal wall or a tunnel occupies is
        /// therefore reached, locked and dangerous, but never drawn as a blast.
        /// </summary>
        /// <remarks>
        /// The test runs at the end of the step that reported the reach, which is after that
        /// step's destruction pass and so matches the recovered order. The same gate also governs
        /// <c>_ObjExplJerk</c>, the surrounding shake, which the simulation does not implement
        /// yet; when it lands, the decision belongs in the core rather than here.
        /// </remarks>
        private static bool ShowsBlast(WorldGrid world, GridPosition cell)
        {
            return world.Contains(cell)
                && !world.GetOccupants(cell).Any(
                    occupant => occupant.Motion is null
                        && !occupant.Interactions.HasFlag(InteractionMask.OccupancyTransparent));
        }

        /// <summary>
        /// Discards the interval spanning a level change, so the first frame of a new level does
        /// not advance by the time the previous level's end screen was on display.
        /// </summary>
        private void ResetClock()
        {
            _lastTimestamp = _timeProvider.GetTimestamp();
            _blastCells.Clear();
        }
    }
}
