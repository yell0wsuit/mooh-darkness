using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that an enemy reached the strike point of its attack.</summary>
    /// <param name="EntityId">The attacking enemy.</param>
    /// <param name="TargetCell">The cell rechecked by the strike.</param>
    public sealed record EnemyStrike(EntityId EntityId, GridPosition TargetCell)
        : SimulationEvent(EntityId);
}
