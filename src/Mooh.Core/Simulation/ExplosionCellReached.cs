using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports an explosion wave reaching one cell.</summary>
    /// <param name="EntityId">The source entity responsible for this explosion wave.</param>
    /// <param name="Position">The reached cell.</param>
    public sealed record ExplosionCellReached(EntityId EntityId, GridPosition Position)
        : SimulationEvent(EntityId);
}
