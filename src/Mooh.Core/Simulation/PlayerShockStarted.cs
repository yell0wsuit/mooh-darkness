using System;

using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that nonlethal damage made the player temporarily uncontrollable.</summary>
    /// <param name="EntityId">The player entity.</param>
    /// <param name="Duration">The complete shock duration.</param>
    public sealed record PlayerShockStarted(EntityId EntityId, TimeSpan Duration)
        : SimulationEvent(EntityId);
}
