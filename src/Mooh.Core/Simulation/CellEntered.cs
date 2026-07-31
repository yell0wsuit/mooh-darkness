using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that an entity committed entry into an adjacent cell.
    /// </summary>
    /// <param name="EntityId">The entity that moved.</param>
    /// <param name="Position">The newly occupied cell.</param>
    public sealed record CellEntered(EntityId EntityId, GridPosition Position)
        : SimulationEvent(EntityId);
}
