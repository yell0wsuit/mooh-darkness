using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Movement;
using Mooh.Core.Simulation;

namespace Mooh.Core.Enemies
{
    internal sealed class BlueMonsterSystem
    {
        private const double CounterMotionMultiplier = 0.75;

        private static readonly TimeSpan TimerTolerance = TimeSpan.FromTicks(1);

        private readonly WorldGrid _world;

        internal BlueMonsterSystem(WorldGrid world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        internal double? SecondsUntilNextTimer()
        {
            TimeSpan? remaining = _world.Entities
                .Where(
                    entity => entity.BlueMonsterBody is { IsFrozen: false }
                        && entity.BlueMonsterBody.State is BlueMonsterState.TurningLeft
                        or BlueMonsterState.TurningRight
                        or BlueMonsterState.AttackWindup
                        or BlueMonsterState.AttackRecovery
                        or BlueMonsterState.AttackTurnaround)
                .Min(entity => entity.BlueMonsterBody?.Remaining);
            return remaining?.TotalSeconds;
        }

        internal void Decide(
            Entity monster,
            int resolutionOrder,
            List<SimulationEvent> events)
        {
            BlueMonsterBody body = monster.BlueMonsterBody
                ?? throw new InvalidOperationException("Blue-monster decisions require a blue-monster body.");
            GridPosition forward = monster.Position.Offset(body.FacingDirection);
            Direction leftDirection = body.FacingDirection.TurnLeft();
            GridPosition left = monster.Position.Offset(leftDirection);

            if (HasStrikeTarget(forward))
            {
                StartAttack(monster, body, forward, events);
                return;
            }

            if (HasStrikeTarget(left) || CellQuery.CanMonsterEnter(_world, monster, left))
            {
                StartTurn(monster, body, turnLeft: true, events);
                return;
            }

            if (CellQuery.CanMonsterEnter(_world, monster, forward))
            {
                StartWalking(monster, body, forward, resolutionOrder, events);
                return;
            }

            StartTurn(monster, body, turnLeft: false, events);
        }

        internal void AdvanceTimer(
            Entity monster,
            double elapsedSeconds,
            List<SimulationEvent> events,
            Action<DamageRequest> applyDamage,
            Func<Entity, EntityId, bool> applyShake)
        {
            ArgumentNullException.ThrowIfNull(applyDamage);
            ArgumentNullException.ThrowIfNull(applyShake);
            BlueMonsterBody? body = monster.BlueMonsterBody;
            if (body is null
                || body.IsFrozen
                || body.State is not (BlueMonsterState.TurningLeft
                or BlueMonsterState.TurningRight
                or BlueMonsterState.AttackWindup
                or BlueMonsterState.AttackRecovery
                or BlueMonsterState.AttackTurnaround))
            {
                return;
            }

            TimeSpan elapsed = SimulationTime.Elapsed(elapsedSeconds);
            if (body.Remaining - elapsed > TimerTolerance)
            {
                body.Remaining -= elapsed;
                return;
            }

            CompleteTimedState(monster, body, events, applyDamage, applyShake);
        }

        internal static void CompleteWalking(Entity monster)
        {
            ArgumentNullException.ThrowIfNull(monster);
            BlueMonsterBody body = monster.BlueMonsterBody
                ?? throw new InvalidOperationException("Blue-monster walking requires a blue-monster body.");
            body.State = BlueMonsterState.Deciding;
        }

        /// <summary>
        /// Applies the recovered <c>IceOn</c>: motion stops, the monster snaps to the physically
        /// nearest cell and its timed state is suspended with whatever remains of it.
        /// </summary>
        internal void Freeze(Entity monster, List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(monster);
            ArgumentNullException.ThrowIfNull(events);
            BlueMonsterBody body = monster.BlueMonsterBody
                ?? throw new InvalidOperationException("Freezing requires a blue-monster body.");
            if (body.IsFrozen)
            {
                return;
            }

            MotionState? motion = monster.Motion;
            if (motion is { Cause: MotionCause.MonsterWalking, Progress: < 0.5 })
            {
                _world.Relocate(
                    monster.Id,
                    monster.Position.Offset(motion.SegmentDirection.Opposite()));
            }

            body.IsFrozen = true;
            KinematicsSystem.Stop(monster);
            events.Add(new EnemyFrozen(monster.Id));
        }

        /// <summary>
        /// Applies the recovered <c>IceOff</c>, whose <c>StateMove</c> resumes the interrupted
        /// state from frame zero. A saved walk resumes directly without another navigation decision.
        /// </summary>
        internal void Thaw(Entity monster, List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(monster);
            ArgumentNullException.ThrowIfNull(events);
            BlueMonsterBody body = monster.BlueMonsterBody
                ?? throw new InvalidOperationException("Thawing requires a blue-monster body.");
            if (!body.IsFrozen)
            {
                return;
            }

            body.IsFrozen = false;
            TimeSpan strikePhase = body.AttackWindupDuration * 0.25;
            TimeSpan recovery = (body.FacingDirection == Direction.Left
                ? body.LeftAttackRecoveryDuration : body.AttackRecoveryDuration) - body.TurnDuration;
            // Recovery contains a recoil animation followed by the first quarter-turn.
            // Restart only the native animation that was active when ice interrupted it.
            body.Remaining = body.State switch
            {
                BlueMonsterState.TurningLeft or BlueMonsterState.TurningRight or BlueMonsterState.AttackTurnaround => body.TurnDuration,
                BlueMonsterState.AttackWindup => body.Remaining <= strikePhase
                    ? strikePhase : body.AttackWindupDuration,
                BlueMonsterState.AttackRecovery => body.Remaining <= body.TurnDuration
                    ? body.TurnDuration : recovery,
                BlueMonsterState.Deciding or BlueMonsterState.DecidingAfterLeftTurn or BlueMonsterState.Walking => TimeSpan.Zero,
                _ => throw new InvalidOperationException("Undefined blue-monster state.")
            };
            events.Add(new EnemyThawed(monster.Id));
            if (body.State == BlueMonsterState.Walking)
            {
                GridPosition destination = monster.Position.Offset(body.FacingDirection);
                if (_world.Contains(destination))
                {
                    StartWalking(monster, body, destination, 0, events);
                }
                else
                {
                    body.State = BlueMonsterState.Deciding;
                }
            }
        }

        internal void DecideAfterLeftTurn(
            Entity monster,
            int resolutionOrder,
            List<SimulationEvent> events)
        {
            BlueMonsterBody body = monster.BlueMonsterBody!;
            GridPosition forward = monster.Position.Offset(body.FacingDirection);
            if (HasStrikeTarget(forward))
            {
                StartAttack(monster, body, forward, events);
                return;
            }

            if (CellQuery.CanMonsterEnter(_world, monster, forward))
            {
                StartWalking(monster, body, forward, resolutionOrder, events);
                return;
            }

            StartTurn(monster, body, turnLeft: false, events);
        }

        private void StartWalking(
            Entity monster,
            BlueMonsterBody body,
            GridPosition destination,
            int resolutionOrder,
            List<SimulationEvent> events)
        {
            GridPosition source = monster.Position;
            _world.CommitMoves([new PlannedMove(monster.Id, source, destination)]);
            KinematicsSystem.Start(
                monster,
                MotionCause.MonsterWalking,
                body.FacingDirection,
                CellQuery.HasCounterMotion(_world, destination, body.FacingDirection)
                    ? body.MoveSpeed * CounterMotionMultiplier
                    : body.MoveSpeed,
                resolutionOrder);
            body.State = BlueMonsterState.Walking;
            events.Add(new EnemyMoveStarted(monster.Id, source, destination));
        }

        /// <summary>
        /// Applies the recovered forward- and left-cell test. The original inspects the cell with
        /// every interaction bit except the monster-shadow bit, so any occupant that is not
        /// explicitly transparent to monsters blocks the walk or the left-hand turn.
        /// </summary>
        /// <summary>
        /// Reports whether a cell holds anything the monster strikes. The original tests the cell
        /// for the blue-strike mask bit, which the player and a fixed set of objects carry.
        /// </summary>
        private bool HasStrikeTarget(GridPosition position)
        {
            return _world.Contains(position)
                && _world.GetOccupants(position).Any(IsStrikeTarget);
        }

        private static bool IsStrikeTarget(Entity entity)
        {
            return entity.IsLivingPlayer
                || entity.Interactions.HasFlag(InteractionMask.BlueMonsterTarget);
        }

        private void CompleteTimedState(
            Entity monster,
            BlueMonsterBody body,
            List<SimulationEvent> events,
            Action<DamageRequest> applyDamage,
            Func<Entity, EntityId, bool> applyShake)
        {
            switch (body.State)
            {
                case BlueMonsterState.TurningLeft:
                    CompleteTurn(monster, body, turnLeft: true, events);
                    break;
                case BlueMonsterState.TurningRight:
                    CompleteTurn(monster, body, turnLeft: false, events);
                    break;
                case BlueMonsterState.AttackWindup:
                    ResolveStrike(monster, body, events, applyDamage, applyShake);
                    break;
                case BlueMonsterState.AttackRecovery:
                    body.FacingDirection = body.FacingDirection.Opposite();
                    body.State = BlueMonsterState.AttackTurnaround;
                    body.Remaining = body.TurnDuration;
                    break;
                case BlueMonsterState.AttackTurnaround:
                    // The final turnaround phase reuses a left-turn state, whose animation ends
                    // in the forward-only decision rather than the left-inspecting one.
                    body.State = BlueMonsterState.DecidingAfterLeftTurn;
                    body.Remaining = TimeSpan.Zero;
                    events.Add(new EnemyTurnCompleted(monster.Id, body.FacingDirection));
                    break;
                case BlueMonsterState.Deciding:
                case BlueMonsterState.DecidingAfterLeftTurn:
                case BlueMonsterState.Walking:
                    throw new InvalidOperationException("A non-timed blue-monster state cannot expire.");
                default:
                    throw new InvalidOperationException("Undefined blue-monster state.");
            }
        }

        private static void CompleteTurn(
            Entity monster,
            BlueMonsterBody body,
            bool turnLeft,
            List<SimulationEvent> events)
        {
            body.FacingDirection = turnLeft
                ? body.FacingDirection.TurnLeft()
                : body.FacingDirection.TurnRight();
            body.State = turnLeft
                ? BlueMonsterState.DecidingAfterLeftTurn
                : BlueMonsterState.Deciding;
            body.Remaining = TimeSpan.Zero;
            events.Add(new EnemyTurnCompleted(monster.Id, body.FacingDirection));
        }

        /// <summary>
        /// Resolves the strike impact. The original rechecks the faced cell and calls
        /// <c>OnShake</c> on every occupant that carries the blue-strike bit, so the player takes
        /// ordinary shake damage and eligible objects run their own shake handler.
        /// </summary>
        private void ResolveStrike(
            Entity monster,
            BlueMonsterBody body,
            List<SimulationEvent> events,
            Action<DamageRequest> applyDamage,
            Func<Entity, EntityId, bool> applyShake)
        {
            GridPosition targetCell = monster.Position.Offset(body.FacingDirection);
            body.State = BlueMonsterState.AttackRecovery;
            TimeSpan turnaroundDuration = body.FacingDirection == Direction.Left
                ? body.LeftAttackRecoveryDuration
                : body.AttackRecoveryDuration;
            body.Remaining = turnaroundDuration - body.TurnDuration;
            events.Add(new EnemyStrike(monster.Id, targetCell));

            bool hit = false;
            if (_world.Contains(targetCell))
            {
                foreach (Entity target in _world.GetOccupants(targetCell))
                {
                    if (!IsStrikeTarget(target))
                    {
                        continue;
                    }

                    if (target.PlayerBody is not null)
                    {
                        hit = true;
                        applyDamage(
                            new DamageRequest(
                                monster.Id,
                                target.Id,
                                DamageKind.Strike,
                                body.StrikeDamage));
                    }
                    else
                    {
                        hit |= applyShake(target, monster.Id);
                    }
                }
            }

            if (hit)
            {
                (double x, double y) = body.FacingDirection switch
                {
                    Direction.Left => (-48.0 / 80, 8.0 / 75),
                    Direction.Up => (-13.0 / 80, -51.0 / 75),
                    Direction.Right => (47.0 / 80, -9.0 / 75),
                    Direction.Down => (13.0 / 80, 50.0 / 75),
                    _ => throw new InvalidOperationException("Undefined strike direction.")
                };
                events.Add(new ShakeImpact(monster.Id, monster.Position.X + x, monster.Position.Y + y));
            }

            events.Add(
                new EnemyTurnStarted(
                    monster.Id,
                    body.FacingDirection,
                    body.FacingDirection.Opposite(),
                    turnaroundDuration));
        }

        private static void StartTurn(
            Entity monster,
            BlueMonsterBody body,
            bool turnLeft,
            List<SimulationEvent> events)
        {
            Direction nextDirection = turnLeft
                ? body.FacingDirection.TurnLeft()
                : body.FacingDirection.TurnRight();
            body.State = turnLeft
                ? BlueMonsterState.TurningLeft
                : BlueMonsterState.TurningRight;
            body.Remaining = body.TurnDuration;
            events.Add(
                new EnemyTurnStarted(
                    monster.Id,
                    body.FacingDirection,
                    nextDirection,
                    body.TurnDuration));
        }

        private static void StartAttack(
            Entity monster,
            BlueMonsterBody body,
            GridPosition targetCell,
            List<SimulationEvent> events)
        {
            body.State = BlueMonsterState.AttackWindup;
            body.Remaining = body.AttackWindupDuration;
            events.Add(
                new EnemyAttackStarted(
                    monster.Id,
                    targetCell,
                    body.AttackWindupDuration));
        }
    }
}
