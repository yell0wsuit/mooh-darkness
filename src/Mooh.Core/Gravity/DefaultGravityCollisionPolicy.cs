using System;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

namespace Mooh.Core.Gravity
{
    /// <summary>
    /// Applies the recovered default obstruction semantics to gravity-driven movement.
    /// </summary>
    public sealed class DefaultGravityCollisionPolicy : IGravityCollisionPolicy
    {
        private const InteractionMask BlocksFalling =
            InteractionMask.BlocksMovement
            | InteractionMask.MovementLocked
            | InteractionMask.PlayerEdible
            | InteractionMask.CanBePushedThrough;

        /// <inheritdoc />
        public bool CanEnter(WorldGrid world, Entity mover, GridPosition destination)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(mover);
            return world.Contains(destination)
                && world.GetOccupants(destination).All(
                    occupant => occupant.Id == mover.Id || (occupant.Interactions & BlocksFalling) == 0);
        }
    }
}
