using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player reversed travel along the active cell segment.
    /// </summary>
    /// <param name="EntityId">The player entity.</param>
    /// <param name="Direction">The new travel direction.</param>
    public sealed record PlayerReversed(EntityId EntityId, Direction Direction)
        : SimulationEvent(EntityId);
}
