using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that held player input could not start a movement segment.
    /// </summary>
    /// <param name="EntityId">The player entity.</param>
    /// <param name="Direction">The blocked direction.</param>
    public sealed record PlayerBlocked(EntityId EntityId, Direction Direction)
        : SimulationEvent(EntityId);
}
