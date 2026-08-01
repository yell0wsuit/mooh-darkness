using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that the player consumed an entity before entering its cell.</summary>
    /// <param name="EntityId">The player entity.</param>
    /// <param name="EatenEntityId">The consumed entity.</param>
    public sealed record PlayerAte(EntityId EntityId, EntityId EatenEntityId)
        : SimulationEvent(EntityId);
}
