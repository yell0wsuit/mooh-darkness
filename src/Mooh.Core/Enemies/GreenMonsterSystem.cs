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
    internal sealed class GreenMonsterSystem
    {
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
                .Where(entity => entity.GreenMonsterBody?.State == GreenMonsterState.Turning)
                .Min(entity => entity.GreenMonsterBody?.Remaining);
            return remaining?.TotalSeconds;
        }

        internal void DecideAll(List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);
            IReadOnlyList<Entity> entities = _world.Entities;
            for (int index = 0; index < entities.Count; index++)
            {
                Entity monster = entities[index];
                if (monster.GreenMonsterBody?.State == GreenMonsterState.Deciding)
                {
                    Decide(monster, index, events);
                }
            }
        }

        internal void AdvanceTimers(double elapsedSeconds, List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);
            TimeSpan elapsed = TimeSpan.FromSeconds(elapsedSeconds);
            foreach (Entity monster in _world.Entities)
            {
                GreenMonsterBody? body = monster.GreenMonsterBody;
                if (body?.State != GreenMonsterState.Turning)
                {
                    continue;
                }

                if (body.Remaining - elapsed > TimerTolerance)
                {
                    body.Remaining -= elapsed;
                    continue;
                }

                body.FacingDirection = body.FacingDirection.TurnRight();
                body.State = GreenMonsterState.Deciding;
                body.Remaining = TimeSpan.Zero;
                events.Add(new EnemyTurnCompleted(monster.Id, body.FacingDirection));
            }

            DecideAll(events);
        }

        internal static void CompleteWalking(Entity monster)
        {
            ArgumentNullException.ThrowIfNull(monster);
            GreenMonsterBody body = monster.GreenMonsterBody
                ?? throw new InvalidOperationException("Monster walking requires a green-monster body.");
            body.State = GreenMonsterState.Deciding;
        }

        private void Decide(Entity monster, int resolutionOrder, List<SimulationEvent> events)
        {
            GreenMonsterBody body = monster.GreenMonsterBody!;
            GridPosition destination = monster.Position.Offset(body.FacingDirection);
            if (CanEnter(monster, destination))
            {
                GridPosition source = monster.Position;
                _world.CommitMoves([new PlannedMove(monster.Id, source, destination)]);
                KinematicsSystem.Start(
                    monster,
                    MotionCause.MonsterWalking,
                    body.FacingDirection,
                    body.MoveSpeed,
                    resolutionOrder);
                body.State = GreenMonsterState.Walking;
                events.Add(new EnemyMoveStarted(monster.Id, source, destination));
                return;
            }

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

        private bool CanEnter(Entity monster, GridPosition destination)
        {
            return !monster.Interactions.HasFlag(InteractionMask.MovementLocked)
                && _world.Contains(destination)
                && _world.GetOccupants(destination)
                    .All(occupant => _interactions.Classify(monster, occupant) == OccupantInteraction.Allow);
        }
    }
}
