using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that an enemy started walking into an adjacent cell.</summary>
    /// <param name="EntityId">The enemy entity.</param>
    /// <param name="From">The source cell.</param>
    /// <param name="To">The reserved destination cell.</param>
    public sealed record EnemyMoveStarted(EntityId EntityId, GridPosition From, GridPosition To)
        : SimulationEvent(EntityId);
}
