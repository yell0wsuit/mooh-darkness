using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a magic gem's recovered <c>MagicWork</c> filled an emptied cell with a random
    /// diamond. The original also draws its one-cell blast graphic there.
    /// </summary>
    /// <param name="EntityId">The magic gem that worked the cell.</param>
    /// <param name="DiamondEntityId">The spawned diamond.</param>
    /// <param name="Position">The filled cell.</param>
    public sealed record MagicDiamondSpawned(
        EntityId EntityId,
        EntityId DiamondEntityId,
        GridPosition Position) : SimulationEvent(EntityId);
}
