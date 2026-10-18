using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that an enemy resumed behavior after an ice bonus expired.</summary>
    /// <param name="EntityId">The thawed enemy.</param>
    public sealed record EnemyThawed(EntityId EntityId) : SimulationEvent(EntityId);
}
