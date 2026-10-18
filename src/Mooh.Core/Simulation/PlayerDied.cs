using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that player health reached zero.</summary>
    /// <param name="EntityId">The player entity.</param>
    public sealed record PlayerDied(EntityId EntityId) : SimulationEvent(EntityId);
}
