using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a reversing player returned to the authoritative origin cell.
    /// </summary>
    /// <param name="EntityId">The player entity.</param>
    /// <param name="Position">The unchanged authoritative cell.</param>
    public sealed record PlayerReturned(EntityId EntityId, GridPosition Position)
        : SimulationEvent(EntityId);
}
