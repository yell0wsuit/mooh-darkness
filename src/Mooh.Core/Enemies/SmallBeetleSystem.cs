using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Movement;
using Mooh.Core.Simulation;

namespace Mooh.Core.Enemies
{
    /// <summary>
    /// Advances spawned small beetles, which chase the player instead of walking a fixed route.
    /// </summary>
    /// <remarks>
    /// This models <c>GameObjSmBeetle._OnCell</c>. On every cell the beetle recomputes the offset
    /// to the player and tries three directions in order: the dominant axis toward the player, the
    /// minor axis toward the player, then the minor axis away from it. The dominant axis away is
    /// never tried, so a beetle pinned against a wall behind the player circles rather than
    /// retreating. When all three are blocked it waits out <c>CNT_OnCellReCheck</c> and tries
    /// again.
    /// </remarks>
    internal sealed class SmallBeetleSystem
    {
        /// <summary>The recovered <c>CNT_OPrm_AirSlow</c> factor applied against counter-motion.</summary>
        private const double CounterMotionMultiplier = 0.75;

        private static readonly TimeSpan TimerTolerance = TimeSpan.FromTicks(1);

        private readonly Random _random;
        private readonly WorldGrid _world;

        internal SmallBeetleSystem(WorldGrid world, Random random)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>Gets the seconds until the earliest beetle timer expires, if any.</summary>
        internal double? SecondsUntilNextTimer()
        {
            TimeSpan? remaining = _world.Entities
                .Where(entity => entity.SmallBeetleBody is not null)
                .SelectMany(entity => Timers(entity.SmallBeetleBody!))
                .Where(value => value > TimeSpan.Zero)
                .Min(value => (TimeSpan?)value);
            return remaining?.TotalSeconds;
        }

        /// <summary>
        /// Advances one beetle's lifetime, blast immunity and blocked-decision wait.
        /// </summary>
        /// <param name="beetle">The beetle to advance.</param>
        /// <param name="elapsedSeconds">The substep length in seconds.</param>
        /// <param name="expire">Removes a beetle whose lifetime has run out.</param>
        internal static void AdvanceTimers(Entity beetle, double elapsedSeconds, Action<Entity> expire)
        {
            ArgumentNullException.ThrowIfNull(beetle);
            ArgumentNullException.ThrowIfNull(expire);
            SmallBeetleBody body = beetle.SmallBeetleBody
                ?? throw new InvalidOperationException("Advancing requires a small-beetle body.");

            TimeSpan elapsed = TimeSpan.FromSeconds(elapsedSeconds);
            body.RemainingImmunity = Reduce(body.RemainingImmunity, elapsed);
            if (body.State == SmallBeetleState.Waiting)
            {
                body.RemainingRecheck = Reduce(body.RemainingRecheck, elapsed);
                if (body.RemainingRecheck == TimeSpan.Zero)
                {
                    body.State = SmallBeetleState.Deciding;
                }
            }

            body.RemainingLifetime = Reduce(body.RemainingLifetime, elapsed);
            if (body.RemainingLifetime == TimeSpan.Zero)
            {
                expire(beetle);
            }
        }

        /// <summary>Marks a beetle that finished a flight segment as ready to decide again.</summary>
        /// <param name="beetle">The beetle that reached its committed cell.</param>
        internal static void CompleteFlight(Entity beetle)
        {
            ArgumentNullException.ThrowIfNull(beetle);
            SmallBeetleBody body = beetle.SmallBeetleBody
                ?? throw new InvalidOperationException("Beetle flight requires a small-beetle body.");
            body.State = SmallBeetleState.Deciding;
        }

        /// <summary>Picks and commits one beetle's next flight segment.</summary>
        /// <param name="beetle">The deciding beetle.</param>
        /// <param name="resolutionOrder">The order in which simultaneous boundaries resolve.</param>
        /// <param name="events">The ordered event list to append to.</param>
        internal void Decide(Entity beetle, int resolutionOrder, List<SimulationEvent> events)
        {
            SmallBeetleBody body = beetle.SmallBeetleBody!;
            Entity? player = _world.Player;
            if (player is null)
            {
                StartWait(body);
                return;
            }

            foreach (Direction direction in Candidates(beetle.Position, player.Position))
            {
                GridPosition destination = beetle.Position.Offset(direction);
                if (!CanFly(beetle.Position, direction))
                {
                    continue;
                }

                GridPosition source = beetle.Position;
                _world.CommitMoves([new PlannedMove(beetle.Id, source, destination)]);
                // The recovered segment speed carries Rand.FRange(0.9, 1.1), drawn per segment.
                double jitter = 0.9 + (0.2 * _random.NextDouble());
                KinematicsSystem.Start(
                    beetle,
                    MotionCause.MonsterWalking,
                    direction,
                    (CellQuery.HasCounterMotion(_world, destination, direction)
                        ? body.MoveSpeed * CounterMotionMultiplier
                        : body.MoveSpeed) * jitter,
                    resolutionOrder);
                body.State = SmallBeetleState.Flying;
                events.Add(new EnemyMoveStarted(beetle.Id, source, destination));
                return;
            }

            StartWait(body);
        }

        /// <summary>
        /// Yields the recovered candidate directions. <c>GameBase.PntDirGet</c> resolves the
        /// dominant axis of the offset first, breaking a tie in favor of the horizontal axis, and
        /// its second form resolves the minor axis; the third candidate is the minor axis flipped
        /// through <c>DIR_MaskPos</c>.
        /// </summary>
        private static IEnumerable<Direction> Candidates(GridPosition from, GridPosition to)
        {
            int offsetX = to.X - from.X;
            int offsetY = to.Y - from.Y;
            Direction horizontal = offsetX > 0 ? Direction.Right : Direction.Left;
            Direction vertical = offsetY > 0 ? Direction.Down : Direction.Up;
            bool horizontalDominates = Math.Abs(offsetX) >= Math.Abs(offsetY);
            Direction dominant = horizontalDominates ? horizontal : vertical;
            Direction minor = horizontalDominates ? vertical : horizontal;

            yield return dominant;
            yield return minor;
            yield return minor.Opposite();
        }

        /// <summary>
        /// Applies the recovered flight test, <c>_OnCellCan</c>. A beetle inside a tunnel may only
        /// leave through a side the tunnel's <c>DirMap</c> opens. It may then enter a cell with no
        /// still <c>CNT_CMaskPartsCollide</c> occupant — the player excluded, so the beetle flies
        /// into the player to be absorbed — or a tunnel whose <c>DirMap</c> carries it on.
        /// </summary>
        private bool CanFly(GridPosition from, Direction direction)
        {
            if (TunnelAt(from) is TunnelShape own && own.Route(direction.Opposite(), ignoreOneWay: true) is null)
            {
                return false;
            }

            GridPosition destination = from.Offset(direction);
            return _world.Contains(destination) && (!_world.GetOccupants(destination).Any(
                    occupant => occupant.Motion is null && occupant.Interactions.HasFlag(InteractionMask.BlocksFlight))
                || TunnelAt(destination)?.Route(direction, ignoreOneWay: true) is not null);
        }

        private TunnelShape? TunnelAt(GridPosition cell)
        {
            return _world.Contains(cell)
                ? _world.GetOccupants(cell)
                    .Select(occupant => occupant.TunnelBody?.Shape)
                    .FirstOrDefault(shape => shape is not null)
                : null;
        }

        private static void StartWait(SmallBeetleBody body)
        {
            body.State = SmallBeetleState.Waiting;
            body.RemainingRecheck = SmallBeetleBody.RecheckDelay;
        }

        private static IEnumerable<TimeSpan> Timers(SmallBeetleBody body)
        {
            yield return body.RemainingLifetime;
            yield return body.RemainingImmunity;
            if (body.State == SmallBeetleState.Waiting)
            {
                yield return body.RemainingRecheck;
            }
        }

        private static TimeSpan Reduce(TimeSpan remaining, TimeSpan elapsed)
        {
            return remaining - elapsed > TimerTolerance ? remaining - elapsed : TimeSpan.Zero;
        }
    }
}
