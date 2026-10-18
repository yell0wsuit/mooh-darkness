using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>A small beetle was killed by a blast or reached the end of its lifetime.</summary>
    /// <param name="EntityId">The affected entity.</param>
    /// <param name="Position">The interaction cell.</param>
    public sealed record SmallBeetleKilled(EntityId EntityId, GridPosition Position) : SimulationEvent(EntityId);
}
