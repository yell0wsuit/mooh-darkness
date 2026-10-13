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

        /// <summary>
        /// The recovered <c>CNT_HlpShTime</c>, how long <c>HlpAdd</c> holds the help panel open.
        /// Reading the same table again re-arms it, which is what the original does when the
        /// requested text is already showing.
        /// </summary>
        private static readonly TimeSpan HelpTextHold = TimeSpan.FromMilliseconds(5000);

        private readonly Dictionary<GridPosition, TimeSpan> _blastCells = [];
        private readonly TimeProvider _timeProvider;
        private string? _helpText;
        private TimeSpan _helpTextExpiry;
        private long _lastTimestamp;
        private TimeSpan _simulationTime;

        /// <summary>Starts a play session on a loaded pack.</summary>
        /// <param name="pack">The loaded archival pack.</param>
        /// <param name="startingLevelIndex">The zero-based index of the first level to play.</param>
        /// <param name="timeProvider">The clock used to measure frame intervals.</param>
        /// <param name="difficulty">The recovered difficulty profile, standard when omitted.</param>
        /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
        public PlaySession(
            LoadedLevelPack pack,
            int startingLevelIndex = 0,
            TimeProvider? timeProvider = null,
            DifficultySpeeds? difficulty = null)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            Session = new GameSession(pack, difficulty ?? DifficultySpeeds.Standard, startingLevelIndex);
            _lastTimestamp = _timeProvider.GetTimestamp();
        }

        /// <summary>Gets the underlying pack session.</summary>
        public GameSession Session
        {
            get;
        }

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
                RecordHelpText(events);
            }

            return Render();
        }

        /// <summary>
        /// Keeps each reached blast cell drawable for as long as its recovered lock lasts. An
        /// explosion is a timed cell effect rather than an entity, so it has nothing to render
        /// from the world itself. The reach time is taken as the end of the step that reported it,
        /// which is accurate to the frame interval.
        /// </summary>
        /// <remarks>
        /// A world rotation needs nothing here even though it moves every cell. No blast can be
        /// reached while the world spins, because the turn freezes the simulation, and the recovered
        /// spin lasts 785 ms against a 500 ms cell lock, so every marker taken before it has already
        /// been pruned by the time the world turns.
        /// </remarks>
        private void RecordBlastCells(IReadOnlyList<SimulationEvent> events)
        {
            WorldGrid world = Session.Level.World;
            foreach (SimulationEvent value in events)
            {
                if (value is ExplosionCellReached reached && SimulationWorld.ShowsBlast(world, reached.Position))
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

        private void RecordHelpText(IReadOnlyList<SimulationEvent> events)
        {
            foreach (SimulationEvent value in events)
            {
                if (value is HelpTextRequested requested)
                {
                    _helpText = requested.HelpTextId;
                    _helpTextExpiry = _simulationTime + HelpTextHold;
                }
            }

            if (_helpText is not null && _helpTextExpiry <= _simulationTime)
            {
                _helpText = null;
            }
        }

        /// <summary>Selects a weapon with the recovered <c>WeapCurrSet</c>, as a number key does.</summary>
        /// <param name="weapon">The weapon to select.</param>
        public void SelectWeapon(WeaponKind weapon)
        {
            if (Session.State == GameSessionState.Playing)
            {
                _ = Session.Level.Simulation.SelectWeapon(weapon);
            }
        }

        /// <summary>Selects the next stocked weapon, as releasing the switch control does.</summary>
        public void SelectNextWeapon()
        {
            if (Session.State == GameSessionState.Playing)
            {
                _ = Session.Level.Simulation.SelectNextWeapon();
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
                (body?.Reaction.Kind ?? PlayerReactionKind.Normal).ToString(),
                _helpText,
                Session.Level.World.Player?.HealthBody?.Clock?.Remaining.TotalSeconds,
                (body?.Progress.CurrentWeapon ?? WeaponKind.Grenade).ToString(),
                body?.Progress.Rockets ?? 0,
                body?.Progress.Mines ?? 0,
                body?.Progress.BigBombs ?? 0);
        }

        /// <summary>
        /// Discards the interval spanning a level change, so the first frame of a new level does
        /// not advance by the time the previous level's end screen was on display.
        /// </summary>
        private void ResetClock()
        {
            _lastTimestamp = _timeProvider.GetTimestamp();
            _blastCells.Clear();
            _helpText = null;
        }
    }
}
