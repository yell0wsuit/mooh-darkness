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
    internal sealed class GreenMonsterSystem
    {
        private const double CounterMotionMultiplier = 0.75;

        private static readonly TimeSpan TimerTolerance = TimeSpan.FromTicks(1);

        private readonly IInteractionPolicy _interactions;
        private readonly WorldGrid _world;

        internal GreenMonsterSystem(WorldGrid world, IInteractionPolicy interactions)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        }

        internal double? SecondsUntilNextTimer()
        {
            TimeSpan? remaining = _world.Entities
                .Where(
                    entity => entity.GreenMonsterBody?.State is GreenMonsterState.Turning
                        or GreenMonsterState.AttackWindup
                        or GreenMonsterState.AttackRecovery)
                .Min(entity => entity.GreenMonsterBody?.Remaining);
            return remaining?.TotalSeconds;
        }

        internal void AdvanceTimer(
            Entity monster,
            double elapsedSeconds,
            List<SimulationEvent> events,
            Action<DamageRequest> applyDamage)
        {
            ArgumentNullException.ThrowIfNull(events);
            ArgumentNullException.ThrowIfNull(applyDamage);
            GreenMonsterBody? body = monster.GreenMonsterBody;
            if (body?.State is not (GreenMonsterState.Turning
                or GreenMonsterState.AttackWindup
                or GreenMonsterState.AttackRecovery))
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
            GreenMonsterBody body = monster.GreenMonsterBody
                ?? throw new InvalidOperationException("Monster walking requires a green-monster body.");
            body.State = GreenMonsterState.Deciding;
        }

        internal void Decide(Entity monster, int resolutionOrder, List<SimulationEvent> events)
        {
            GreenMonsterBody body = monster.GreenMonsterBody!;
            GridPosition destination = monster.Position.Offset(body.FacingDirection);
            if (FindAttackablePlayer(destination) is not null)
            {
                body.State = GreenMonsterState.AttackWindup;
                body.Remaining = body.AttackWindupDuration;
                events.Add(
                    new EnemyAttackStarted(
                        monster.Id,
                        destination,
                        body.AttackWindupDuration));
                return;
            }

            if (CanEnter(monster, destination))
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
                body.State = GreenMonsterState.Walking;
                events.Add(new EnemyMoveStarted(monster.Id, source, destination));
                return;
            }

            StartTurn(monster, body, events);
        }

        private bool CanEnter(Entity monster, GridPosition destination)
        {
            return !monster.Interactions.HasFlag(InteractionMask.MovementLocked)
                && _world.Contains(destination)
                && _world.GetOccupants(destination)
                    .All(occupant => _interactions.Classify(monster, occupant) == OccupantInteraction.Allow);
        }

        private bool HasCounterMotion(GridPosition position, Direction direction)
        {
            return _world.GetOccupants(position).Any(
                entity => entity.Motion is not null
                    && entity.Motion.TravelDirection != direction);
        }

        private void CompleteTimedState(
            Entity monster,
            GreenMonsterBody body,
            List<SimulationEvent> events,
            Action<DamageRequest> applyDamage)
        {
            switch (body.State)
            {
                case GreenMonsterState.Turning:
                    body.FacingDirection = body.FacingDirection.TurnRight();
                    body.State = GreenMonsterState.Deciding;
                    body.Remaining = TimeSpan.Zero;
                    events.Add(new EnemyTurnCompleted(monster.Id, body.FacingDirection));
                    break;
                case GreenMonsterState.AttackWindup:
                    ResolveStrike(monster, body, events, applyDamage);
                    break;
                case GreenMonsterState.AttackRecovery:
                    StartTurn(monster, body, events);
                    break;
                case GreenMonsterState.Deciding:
                case GreenMonsterState.Walking:
                    throw new InvalidOperationException("A non-timed green-monster state cannot expire.");
                default:
                    throw new InvalidOperationException("Undefined green-monster state.");
            }
        }

        private void ResolveStrike(
            Entity monster,
            GreenMonsterBody body,
            List<SimulationEvent> events,
            Action<DamageRequest> applyDamage)
        {
            GridPosition targetCell = monster.Position.Offset(body.FacingDirection);
            body.State = GreenMonsterState.AttackRecovery;
            body.Remaining = body.AttackRecoveryDuration;
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

        private static void StartTurn(
            Entity monster,
            GreenMonsterBody body,
            List<SimulationEvent> events)
        {
            Direction nextDirection = body.FacingDirection.TurnRight();
            body.State = GreenMonsterState.Turning;
            body.Remaining = body.TurnDuration;
            events.Add(
                new EnemyTurnStarted(
                    monster.Id,
                    body.FacingDirection,
                    nextDirection,
                    body.TurnDuration));
        }
    }
}
