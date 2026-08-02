using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that the level was failed after its recovered settle.</summary>
    /// <param name="EntityId">The player entity the outcome belongs to.</param>
    public sealed record LevelLost(EntityId EntityId) : SimulationEvent(EntityId);
}
