using System;
using System.Collections.Generic;
using System.Linq;

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
            if (mover.Motion is not null
                || mover.Interactions.HasFlag(InteractionMask.MovementLocked))
            {
                return new MovementPlan(MoveResult.Blocked, []);
            }

            GridPosition destination = mover.Position.Offset(request.Direction);
            if (!_world.Contains(destination))
            {
                return new MovementPlan(MoveResult.Blocked, []);
            }

            List<Entity> pushed = [];
            foreach (Entity occupant in _world.GetOccupants(destination))
            {
                switch (_interactions.Classify(mover, occupant))
                {
                    case OccupantInteraction.Allow:
                        break;
                    case OccupantInteraction.Block:
                        return new MovementPlan(MoveResult.Blocked, []);
                    case OccupantInteraction.Push:
                        pushed.Add(occupant);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown occupant interaction.");
                }
            }

            if (pushed.Count == 0)
            {
                return new MovementPlan(
                    MoveResult.Moved,
                    [new MovementStep(mover.Id, mover.Position, destination)]);
            }

            GridPosition pushedDestination = destination.Offset(request.Direction);
            if (!_world.Contains(pushedDestination)
                || _world.GetOccupants(pushedDestination).Any(
                    entity => !entity.Interactions.HasFlag(InteractionMask.CanBePushedThrough))
                || pushed.Exists(
                    entity => entity.Motion is not null
                        || entity.Interactions.HasFlag(InteractionMask.MovementLocked)))
            {
                return new MovementPlan(MoveResult.Blocked, []);
            }

            List<MovementStep> steps = [];
            steps.AddRange(
                pushed.Select(
                    entity => new MovementStep(entity.Id, entity.Position, pushedDestination)));
            steps.Add(new MovementStep(mover.Id, mover.Position, destination));
            return new MovementPlan(MoveResult.Pushed, steps);
        }
    }
}
