using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player began walking toward an adjacent cell.
    /// </summary>
    /// <param name="EntityId">The player entity.</param>
    /// <param name="Direction">The direction of the walking segment.</param>
    public sealed record PlayerMoveStarted(EntityId EntityId, Direction Direction)
        : SimulationEvent(EntityId);
}
