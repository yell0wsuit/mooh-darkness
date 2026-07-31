using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Represents an ordered observable change produced by simulation advancement.
    /// </summary>
    /// <param name="EntityId">The entity associated with the change.</param>
    public abstract record SimulationEvent(EntityId EntityId);
}
