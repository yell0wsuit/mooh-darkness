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
        /// <summary>
        /// Matches the recovered <c>CNT_OrMaskNotEmpty</c>, the support test an ordinary falling
        /// object uses.
        /// </summary>
        private const InteractionMask BlocksFalling =
            InteractionMask.BlocksMovement
            | InteractionMask.MovementLocked
            | InteractionMask.PlayerEdible
            | InteractionMask.CanBePushedThrough;

        /// <summary>
        /// Matches the recovered <c>CNT_OrMaskNotEmpty2</c>, which adds
        /// <c>CNT_CMaskMonShadow | CNT_CMaskShadowObj</c> to the ordinary support test. Only a
        /// body declaring <see cref="GravityBody.TreatsShadowAsSupport"/> uses it.
        /// </summary>
        private const InteractionMask BlocksFallingWithShadow = BlocksFalling
            | InteractionMask.MonsterTransparent
            | InteractionMask.OccupancyTransparent;

        /// <inheritdoc />
        public bool CanEnter(WorldGrid world, Entity mover, GridPosition destination)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(mover);
            InteractionMask blocking = mover.GravityBody?.TreatsShadowAsSupport == true
                ? BlocksFallingWithShadow
                : BlocksFalling;
            return world.Contains(destination)
                && world.GetOccupants(destination).All(
                    occupant => occupant.Id == mover.Id || (occupant.Interactions & blocking) == 0);
        }
    }
}
