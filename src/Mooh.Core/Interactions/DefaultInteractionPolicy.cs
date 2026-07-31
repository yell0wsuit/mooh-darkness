using System;

using Mooh.Core.Entities;

namespace Mooh.Core.Interactions
{
    /// <summary>
    /// Applies the simulation core's default movement interaction rules.
    /// </summary>
    public sealed class DefaultInteractionPolicy : IInteractionPolicy
    {
        /// <inheritdoc/>
        public OccupantInteraction Classify(Entity mover, Entity occupant)
        {
            ArgumentNullException.ThrowIfNull(mover);
            ArgumentNullException.ThrowIfNull(occupant);

            return occupant.Interactions.HasFlag(InteractionMask.MovementLocked)
                ? OccupantInteraction.Block
                : occupant.Interactions.HasFlag(InteractionMask.PlayerPushable)
                ? OccupantInteraction.Push
                : occupant.Interactions.HasFlag(InteractionMask.BlocksMovement)
                ? OccupantInteraction.Block
                : OccupantInteraction.Allow;
        }
    }
}
