using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that the level was completed after its recovered settle.</summary>
    /// <param name="EntityId">The player entity the outcome belongs to.</param>
    public sealed record LevelWon(EntityId EntityId) : SimulationEvent(EntityId);
}
