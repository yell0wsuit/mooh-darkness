using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Decides whether a level has been won or lost, reproducing the recovered <c>__GStatePlay</c>
    /// loop. A level is won once no entity carries <see cref="InteractionMask.LevelEndLock"/>,
    /// provided at least one carrier existed when the level started, and lost once player health
    /// reaches zero. Each decision is followed by a fixed settle before it becomes observable.
    /// </summary>
    internal sealed class LevelOutcomeSystem
    {
        /// <summary>Matches the recovered <c>CNT_WTime_Win1</c> pause before the win is announced.</summary>
        private static readonly TimeSpan WinAnnounceDelay = TimeSpan.FromMilliseconds(500);

        /// <summary>Matches the recovered <c>CNT_WTime_Win2</c> pause after the win is announced.</summary>
        private static readonly TimeSpan WinSettleDelay = TimeSpan.FromMilliseconds(1500);

        /// <summary>Matches the recovered <c>CNT_WTime_Los</c> pause before the loss is reported.</summary>
        private static readonly TimeSpan LossSettleDelay = TimeSpan.FromMilliseconds(1500);

        /// <summary>
        /// Matches the recovered <c>CNT_WTime_Win1</c> pause the timeout branch takes after
        /// <c>SndClockEnd</c>, which reuses the win constants rather than the loss one.
        /// </summary>
        private static readonly TimeSpan TimeoutAnnounceDelay = TimeSpan.FromMilliseconds(500);

        /// <summary>Matches the recovered <c>CNT_WTime_Win2</c> pause after the timeout is announced.</summary>
        private static readonly TimeSpan TimeoutSettleDelay = TimeSpan.FromMilliseconds(1500);

        /// <summary>Absorbs the sub-tick residue left by repeated substep subtraction.</summary>
        private static readonly TimeSpan TimerTolerance = TimeSpan.FromTicks(1);

        private readonly bool _startedWithLockCarriers;
        private readonly bool _usesHealthLossCondition;
        private readonly WorldGrid _world;
        private LevelSettlePhase _phase;
        private TimeSpan _remaining;

        /// <summary>Captures the start-of-level lock-carrier snapshot.</summary>
        /// <param name="world">The authoritative world.</param>
        /// <param name="difficultySpeeds">The active recovered difficulty profile.</param>
        internal LevelOutcomeSystem(WorldGrid world, DifficultySpeeds difficultySpeeds)
        {
            _world = world;
            _startedWithLockCarriers = HasLockCarrier(world);
            _usesHealthLossCondition = !difficultySpeeds.IsTimed;
        }

        private enum LevelSettlePhase
        {
            None,
            WinAnnounce,
            WinSettle,
            LossSettle,
            TimeoutAnnounce,
            TimeoutSettle,
        }

        /// <summary>
        /// Gets whether the loss branch has fired and is settling. The recovered branch puts the
        /// player into <c>ST_Dead</c> at that moment, whatever emptied the health.
        /// </summary>
        internal bool IsSettlingLoss => _phase == LevelSettlePhase.LossSettle;

        /// <summary>Gets the decided outcome, or <see cref="LevelOutcome.InProgress"/>.</summary>
        internal LevelOutcome Outcome { get; private set; }

        /// <summary>Gets the seconds until the pending settle can complete, when time-bound.</summary>
        /// <returns>The remaining settle seconds, or <see langword="null"/> when none is pending.</returns>
        internal double? SecondsUntilSettle()
        {
            return _phase != LevelSettlePhase.None && _remaining > TimeSpan.Zero
                ? _remaining.TotalSeconds
                : null;
        }

        /// <summary>Advances a pending settle and then evaluates the recovered outcome conditions.</summary>
        /// <param name="elapsedSeconds">The elapsed simulation seconds.</param>
        /// <param name="events">The ordered event sink.</param>
        internal void Advance(double elapsedSeconds, List<SimulationEvent> events)
        {
            AdvanceSettle(TimeSpan.FromSeconds(elapsedSeconds), events);
            Observe();
        }

        /// <summary>
        /// Evaluates the recovered outcome conditions without consuming time. The loss condition is
        /// tested first because the original tests it in an earlier branch whose inner wait keeps
        /// the win branch from running in the same tick.
        /// </summary>
        internal void Observe()
        {
            if (Outcome != LevelOutcome.InProgress || _phase != LevelSettlePhase.None)
            {
                return;
            }

            if (_world.Player is not Entity player || player.PlayerBody is null)
            {
                return;
            }

            // The recovered timed branch is `if bTimed: ... elif health <= 0`, so on the timed
            // profile the clock is the only loss condition and it is tested before the win.
            if (player.HealthBody?.Clock?.HasRunOut == true)
            {
                _phase = LevelSettlePhase.TimeoutAnnounce;
                _remaining = TimeoutAnnounceDelay;
                return;
            }

            if (_usesHealthLossCondition && player.HealthBody?.IsDead == true)
            {
                _phase = LevelSettlePhase.LossSettle;
                _remaining = LossSettleDelay;
                return;
            }

            if (_startedWithLockCarriers && !HasLockCarrier(_world))
            {
                _phase = LevelSettlePhase.WinAnnounce;
                _remaining = WinAnnounceDelay;
            }
        }

        private static bool HasLockCarrier(WorldGrid world)
        {
            return world.Entities.Any(entity => entity.Interactions.HasFlag(InteractionMask.LevelEndLock));
        }

        /// <summary>
        /// Runs the pending settle. The win condition is deliberately never rechecked: the original
        /// commits to the win as soon as it observes the empty lock set, so a carrier that appears
        /// during the settle cannot cancel it.
        /// </summary>
        private void AdvanceSettle(TimeSpan elapsed, List<SimulationEvent> events)
        {
            if (_phase == LevelSettlePhase.None)
            {
                return;
            }

            if (_remaining - elapsed > TimerTolerance)
            {
                _remaining -= elapsed;
                return;
            }

            _remaining = TimeSpan.Zero;

            switch (_phase)
            {
                case LevelSettlePhase.WinAnnounce:
                    _phase = LevelSettlePhase.WinSettle;
                    _remaining = WinSettleDelay;
                    return;
                case LevelSettlePhase.WinSettle:
                    if (IsPlayerHoldingLevelEnd())
                    {
                        return;
                    }

                    Complete(LevelOutcome.Won, events);
                    return;
                case LevelSettlePhase.LossSettle:
                    Complete(LevelOutcome.Lost, events);
                    return;
                case LevelSettlePhase.TimeoutAnnounce:
                    _phase = LevelSettlePhase.TimeoutSettle;
                    _remaining = TimeoutSettleDelay;
                    return;
                case LevelSettlePhase.TimeoutSettle:
                    if (IsPlayerHoldingLevelEnd())
                    {
                        return;
                    }

                    Complete(LevelOutcome.Lost, events);
                    return;
                case LevelSettlePhase.None:
                default:
                    return;
            }
        }

        /// <summary>
        /// Reports the recovered <c>PObj.State.bLockLvlEnd</c> hold, which
        /// <see cref="PlayerBody.HoldsLevelEndOpen"/> states once for every carrier.
        /// </summary>
        private bool IsPlayerHoldingLevelEnd()
        {
            return _world.Player?.PlayerBody?.HoldsLevelEndOpen == true;
        }

        private void Complete(LevelOutcome outcome, List<SimulationEvent> events)
        {
            _phase = LevelSettlePhase.None;
            _remaining = TimeSpan.Zero;
            Outcome = outcome;
            EntityId playerId = _world.Player!.Id;
            events.Add(outcome == LevelOutcome.Won
                ? new LevelWon(playerId)
                : new LevelLost(playerId));
        }
    }
}
