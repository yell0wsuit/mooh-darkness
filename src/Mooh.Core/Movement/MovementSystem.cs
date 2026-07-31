using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

namespace Mooh.Core.Movement
{
    /// <summary>
    /// Plans movement requests without mutating the authoritative world.
    /// </summary>
    /// <remarks>
    /// Initializes a movement system for a world and interaction policy.
    /// </remarks>
    /// <param name="world">The authoritative world to inspect.</param>
    /// <param name="interactions">The policy used to classify destination occupants.</param>
    public sealed class MovementSystem(WorldGrid world, IInteractionPolicy interactions)
    {
        private readonly IInteractionPolicy _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        private readonly WorldGrid _world = world ?? throw new ArgumentNullException(nameof(world));

        /// <summary>
        /// Plans a movement request without mutating the world.
        /// </summary>
        /// <param name="request">The entity and direction to plan.</param>
        /// <returns>The movement outcome and ordered entity transitions.</returns>
        public MovementPlan Plan(MoveRequest request)
        {
            Entity mover = _world.GetEntity(request.EntityId);
            List<MovementStep> plannedMoves = [];
            HashSet<EntityId> plannedEntities = [];
            HashSet<EntityId> visiting = [];

            if (!TryPlanMove(mover, request.Direction, plannedMoves, plannedEntities, visiting))
            {
                return new MovementPlan(MoveResult.Blocked, []);
            }

            MoveResult result = plannedMoves.Count > 1 ? MoveResult.Pushed : MoveResult.Moved;
            return new MovementPlan(result, plannedMoves);
        }

        private bool TryPlanMove(
            Entity entity,
            Direction direction,
            List<MovementStep> plannedMoves,
            HashSet<EntityId> plannedEntities,
            HashSet<EntityId> visiting)
        {
            if (entity.Motion is not null
                || entity.Interactions.HasFlag(InteractionMask.MovementLocked)
                || !visiting.Add(entity.Id))
            {
                return false;
            }

            GridPosition destination = entity.Position.Offset(direction);
            if (!_world.Contains(destination))
            {
                _ = visiting.Remove(entity.Id);
                return false;
            }

            foreach (Entity occupant in _world.GetOccupants(destination))
            {
                if (plannedEntities.Contains(occupant.Id))
                {
                    continue;
                }

                switch (_interactions.Classify(entity, occupant))
                {
                    case OccupantInteraction.Allow:
                        break;
                    case OccupantInteraction.Block:
                        _ = visiting.Remove(entity.Id);
                        return false;
                    case OccupantInteraction.Push:
                        if (!TryPlanMove(occupant, direction, plannedMoves, plannedEntities, visiting))
                        {
                            _ = visiting.Remove(entity.Id);
                            return false;
                        }

                        break;
                    default:
                        throw new InvalidOperationException("Unknown occupant interaction.");
                }
            }

            _ = visiting.Remove(entity.Id);
            _ = plannedEntities.Add(entity.Id);
            plannedMoves.Add(new MovementStep(entity.Id, entity.Position, destination));
            return true;
        }
    }
}
