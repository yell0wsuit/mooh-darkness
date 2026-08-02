using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a defeated monster spawned a small beetle. Both the Green Monster and the Blue
    /// Monster do this in <c>_OnKillInt</c>, each spawning its own colored beetle.
    /// </summary>
    /// <param name="EntityId">The defeated monster.</param>
    /// <param name="SpawnedEntityId">The spawned beetle.</param>
    /// <param name="Position">The beetle's authoritative spawn cell.</param>
    /// <param name="Color">The spawning monster's family.</param>
    public sealed record SmallBeetleSpawned(
        EntityId EntityId,
        EntityId SpawnedEntityId,
        GridPosition Position,
        SmallBeetleColor Color) : SimulationEvent(EntityId);
}
