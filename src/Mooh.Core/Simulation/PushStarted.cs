using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that an entity began continuous motion after a player push.
    /// </summary>
    /// <param name="EntityId">The pushed entity.</param>
    /// <param name="Direction">The push direction.</param>
    /// <param name="IsStrike">Whether a hand strike started the push rather than walking.</param>
    public sealed record PushStarted(EntityId EntityId, Direction Direction, bool IsStrike = false)
        : SimulationEvent(EntityId);
}
