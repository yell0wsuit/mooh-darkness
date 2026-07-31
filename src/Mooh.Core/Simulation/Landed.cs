using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that an entity stopped falling because further travel was blocked.
    /// </summary>
    /// <param name="EntityId">The entity that landed.</param>
    /// <param name="Direction">The gravity direction in which it was falling.</param>
    public sealed record Landed(EntityId EntityId, Direction Direction)
        : SimulationEvent(EntityId);
}
