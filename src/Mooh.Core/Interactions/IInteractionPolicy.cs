using Mooh.Core.Entities;

namespace Mooh.Core.Interactions
{
    /// <summary>
    /// Classifies how one entity interacts with an occupant of its destination cell.
    /// </summary>
    public interface IInteractionPolicy
    {
        /// <summary>
        /// Classifies the interaction between a mover and a destination occupant.
        /// </summary>
        /// <param name="mover">The entity attempting to move.</param>
        /// <param name="occupant">An entity already occupying the destination.</param>
        /// <returns>The required interaction.</returns>
        OccupantInteraction Classify(Entity mover, Entity occupant);
    }
}
