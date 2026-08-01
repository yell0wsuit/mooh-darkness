using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports an entity removed by an explosion.</summary>
    /// <param name="EntityId">The destroyed entity.</param>
    /// <param name="SourceEntityId">The exploding entity responsible for the destruction.</param>
    public sealed record EntityDestroyed(EntityId EntityId, EntityId SourceEntityId)
        : SimulationEvent(EntityId);
}
