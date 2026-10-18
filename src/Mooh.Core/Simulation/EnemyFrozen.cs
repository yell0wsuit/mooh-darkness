using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that an ice bonus froze an enemy.</summary>
    /// <param name="EntityId">The frozen enemy.</param>
    public sealed record EnemyFrozen(EntityId EntityId) : SimulationEvent(EntityId);
}
