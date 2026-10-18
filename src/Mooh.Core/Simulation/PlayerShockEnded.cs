using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that the player recovered from shock.</summary>
    /// <param name="EntityId">The player entity.</param>
    public sealed record PlayerShockEnded(EntityId EntityId) : SimulationEvent(EntityId);
}
