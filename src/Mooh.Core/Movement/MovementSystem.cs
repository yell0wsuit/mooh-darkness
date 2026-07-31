using System;

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
            if (mover.Interactions.HasFlag(InteractionMask.MovementLocked))
            {
                return MoveResult.Blocked;
            }

            GridPosition destination = mover.Position.Offset(request.Direction);
            if (!_world.Contains(destination))
            {
                return MoveResult.Blocked;
            }

            foreach (Entity occupant in _world.GetOccupants(destination))
            {
                if (_interactions.Classify(mover, occupant) != OccupantInteraction.Allow)
                {
                    return MoveResult.Blocked;
                }
            }

            _world.CommitMoves([new PlannedMove(mover.Id, mover.Position, destination)]);
            return MoveResult.Moved;
        }
    }
}
