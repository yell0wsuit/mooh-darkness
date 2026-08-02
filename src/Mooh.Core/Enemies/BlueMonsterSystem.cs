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
                    entity => entity.BlueMonsterBody?.State is BlueMonsterState.TurningLeft
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

            if (FindAttackablePlayer(forward) is not null)
            {
                StartAttack(monster, body, forward, events);
                return;
            }

            if (FindAttackablePlayer(left) is not null || CanEnter(monster, left))
            {
                StartTurn(monster, body, turnLeft: true, events);
                return;
            }

            if (CanEnter(monster, forward))
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
            Action<DamageRequest> applyDamage)
        {
            ArgumentNullException.ThrowIfNull(applyDamage);
            BlueMonsterBody? body = monster.BlueMonsterBody;
            if (body?.State is not (BlueMonsterState.TurningLeft
                or BlueMonsterState.TurningRight
                or BlueMonsterState.AttackWindup
                or BlueMonsterState.AttackRecovery
                or BlueMonsterState.AttackTurnaround))
            {
                return;
            }

            TimeSpan elapsed = TimeSpan.FromSeconds(elapsedSeconds);
            if (body.Remaining - elapsed > TimerTolerance)
            {
                body.Remaining -= elapsed;
                return;
            }

            CompleteTimedState(monster, body, events, applyDamage);
        }

        internal static void CompleteWalking(Entity monster)
        {
            ArgumentNullException.ThrowIfNull(monster);
            BlueMonsterBody body = monster.BlueMonsterBody
                ?? throw new InvalidOperationException("Blue-monster walking requires a blue-monster body.");
            body.State = BlueMonsterState.Deciding;
        }

        internal void DecideAfterLeftTurn(
            Entity monster,
            int resolutionOrder,
            List<SimulationEvent> events)
        {
            BlueMonsterBody body = monster.BlueMonsterBody!;
            GridPosition forward = monster.Position.Offset(body.FacingDirection);
            if (FindAttackablePlayer(forward) is not null)
            {
                StartAttack(monster, body, forward, events);
                return;
            }

            if (CanEnter(monster, forward))
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
                HasCounterMotion(destination, body.FacingDirection)
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
        private bool CanEnter(Entity monster, GridPosition destination)
        {
            return !monster.Interactions.HasFlag(InteractionMask.MovementLocked)
                && _world.Contains(destination)
                && _world.GetOccupants(destination)
                    .All(occupant => occupant.Interactions.HasFlag(InteractionMask.MonsterTransparent));
        }

        private Entity? FindAttackablePlayer(GridPosition position)
        {
            return _world.Contains(position)
                ? _world.GetOccupants(position)
                    .FirstOrDefault(
                        entity => entity.PlayerBody is not null
                            && entity.HealthBody is { IsDead: false })
                : null;
        }

        private void CompleteTimedState(
            Entity monster,
            BlueMonsterBody body,
            List<SimulationEvent> events,
            Action<DamageRequest> applyDamage)
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
                    ResolveStrike(monster, body, events, applyDamage);
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

        private void ResolveStrike(
            Entity monster,
            BlueMonsterBody body,
            List<SimulationEvent> events,
            Action<DamageRequest> applyDamage)
        {
            GridPosition targetCell = monster.Position.Offset(body.FacingDirection);
            body.State = BlueMonsterState.AttackRecovery;
            TimeSpan turnaroundDuration = body.FacingDirection == Direction.Left
                ? body.LeftAttackRecoveryDuration
                : body.AttackRecoveryDuration;
            body.Remaining = turnaroundDuration - body.TurnDuration;
            events.Add(new EnemyStrike(monster.Id, targetCell));

            Entity? target = FindAttackablePlayer(targetCell);
            if (target is not null)
            {
                applyDamage(
                    new DamageRequest(
                        monster.Id,
                        target.Id,
                        DamageKind.Strike,
                        body.StrikeDamage));
            }

            events.Add(
                new EnemyTurnStarted(
                    monster.Id,
                    body.FacingDirection,
                    body.FacingDirection.Opposite(),
                    turnaroundDuration));
        }

        private bool HasCounterMotion(GridPosition position, Direction direction)
        {
            return _world.GetOccupants(position).Any(
                entity => entity.Motion is not null
                    && entity.Motion.TravelDirection != direction);
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
