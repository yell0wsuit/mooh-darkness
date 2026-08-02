using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that a defeated Green Monster spawned a small green beetle.</summary>
    /// <param name="EntityId">The defeated Green Monster.</param>
    /// <param name="SpawnedEntityId">The spawned beetle.</param>
    /// <param name="Position">The beetle's authoritative spawn cell.</param>
    public sealed record SmallGreenBeetleSpawned(
        EntityId EntityId,
        EntityId SpawnedEntityId,
        GridPosition Position) : SimulationEvent(EntityId);
}
