using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that an entity began falling.
    /// </summary>
    /// <param name="EntityId">The falling entity.</param>
    /// <param name="Direction">The direction of gravity-driven travel.</param>
    public sealed record FallStarted(EntityId EntityId, Direction Direction)
        : SimulationEvent(EntityId);
}
