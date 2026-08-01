using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports a new delayed 3x3 explosion.</summary>
    /// <param name="EntityId">The exploding source entity.</param>
    /// <param name="Position">The explosion center.</param>
    public sealed record ExplosionStarted(EntityId EntityId, GridPosition Position)
        : SimulationEvent(EntityId);
}
