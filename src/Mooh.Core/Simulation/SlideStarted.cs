using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a supported round entity began sliding laterally.
    /// </summary>
    /// <param name="EntityId">The sliding entity.</param>
    /// <param name="Direction">The lateral direction of travel.</param>
    public sealed record SlideStarted(EntityId EntityId, Direction Direction)
        : SimulationEvent(EntityId);
}
