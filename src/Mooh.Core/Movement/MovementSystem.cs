using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

namespace Mooh.Core.Movement
{
    /// <summary>
    /// Plans movement requests and commits successful transactions to a world.
    /// </summary>
    /// <remarks>
    /// Initializes a movement system for a world and interaction policy.
    /// </remarks>
    /// <param name="world">The authoritative world to mutate.</param>
    /// <param name="interactions">The policy used to classify destination occupants.</param>
    public sealed class MovementSystem(WorldGrid world, IInteractionPolicy interactions)
    {
        private readonly IInteractionPolicy _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        private readonly WorldGrid _world = world ?? throw new ArgumentNullException(nameof(world));

        /// <summary>
        /// Attempts to execute a movement request atomically.
        /// </summary>
        /// <param name="request">The entity and direction to move.</param>
        /// <returns>The movement outcome.</returns>
        public MoveResult Move(MoveRequest request)
        {
            Entity mover = _world.GetEntity(request.EntityId);
            List<PlannedMove> plannedMoves = [];
            HashSet<EntityId> plannedEntities = [];
            HashSet<EntityId> visiting = [];

            if (!TryPlanMove(mover, request.Direction, plannedMoves, plannedEntities, visiting))
            {
                return MoveResult.Blocked;
            }

            _world.CommitMoves(plannedMoves);
            return plannedMoves.Count > 1 ? MoveResult.Pushed : MoveResult.Moved;
        }

        private bool TryPlanMove(
            Entity entity,
            Direction direction,
            List<PlannedMove> plannedMoves,
            HashSet<EntityId> plannedEntities,
            HashSet<EntityId> visiting)
        {
            if (entity.Interactions.HasFlag(InteractionMask.MovementLocked)
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
            plannedMoves.Add(new PlannedMove(entity.Id, entity.Position, destination));
            return true;
        }
    }
}
