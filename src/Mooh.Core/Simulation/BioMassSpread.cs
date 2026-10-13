using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a bio mass spread into a neighbor, the recovered <c>_AEnd_SWork</c> making a
    /// new bio mass in its own cell and sending it into the chosen one.
    /// </summary>
    /// <param name="EntityId">The bio mass that spread.</param>
    /// <param name="ChildEntityId">The new bio mass.</param>
    /// <param name="From">The parent's cell, where the child starts.</param>
    /// <param name="To">The neighbor the child moves into.</param>
    public sealed record BioMassSpread(
        EntityId EntityId,
        EntityId ChildEntityId,
        GridPosition From,
        GridPosition To) : SimulationEvent(EntityId);
}
