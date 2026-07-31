using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that an entity began continuous motion after a player push.
    /// </summary>
    /// <param name="EntityId">The pushed entity.</param>
    /// <param name="Direction">The push direction.</param>
    public sealed record PushStarted(EntityId EntityId, Direction Direction)
        : SimulationEvent(EntityId);
}
