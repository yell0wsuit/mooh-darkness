using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>A frozen monster shattered through its ice interaction.</summary>
    /// <param name="EntityId">The affected entity.</param>
    /// <param name="Position">The interaction cell.</param>
    public sealed record FrozenMonsterShattered(EntityId EntityId, GridPosition Position) : SimulationEvent(EntityId);
}
