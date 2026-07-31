using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player stopped after completing a forward cell segment.
    /// </summary>
    /// <param name="EntityId">The player entity.</param>
    public sealed record PlayerStopped(EntityId EntityId)
        : SimulationEvent(EntityId);
}
