using System;
using System.Collections.Generic;
using System.Linq;

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

        private readonly IInteractionPolicy _interactions;
        private readonly WorldGrid _world;

        internal BlueMonsterSystem(WorldGrid world, IInteractionPolicy interactions)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        }

        internal double? SecondsUntilNextTimer()
        {
            TimeSpan? remaining = _world.Entities
                .Where(
                    entity => entity.BlueMonsterBody?.State is BlueMonsterState.TurningLeft
                        or BlueMonsterState.TurningRight)
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

        internal static void AdvanceTimer(
            Entity monster,
            double elapsedSeconds,
            List<SimulationEvent> events)
        {
            BlueMonsterBody? body = monster.BlueMonsterBody;
            if (body?.State is not (BlueMonsterState.TurningLeft or BlueMonsterState.TurningRight))
            {
                return;
            }

            TimeSpan elapsed = TimeSpan.FromSeconds(elapsedSeconds);
            if (body.Remaining - elapsed > TimerTolerance)
            {
                body.Remaining -= elapsed;
                return;
            }

            bool turnedLeft = body.State == BlueMonsterState.TurningLeft;
            body.FacingDirection = turnedLeft
                ? body.FacingDirection.TurnLeft()
                : body.FacingDirection.TurnRight();
            body.State = turnedLeft
                ? BlueMonsterState.DecidingAfterLeftTurn
                : BlueMonsterState.Deciding;
            body.Remaining = TimeSpan.Zero;
            events.Add(new EnemyTurnCompleted(monster.Id, body.FacingDirection));
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

        private bool CanEnter(Entity monster, GridPosition destination)
        {
            return !monster.Interactions.HasFlag(InteractionMask.MovementLocked)
                && _world.Contains(destination)
                && _world.GetOccupants(destination)
                    .All(occupant => _interactions.Classify(monster, occupant) == OccupantInteraction.Allow);
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
    }
}
