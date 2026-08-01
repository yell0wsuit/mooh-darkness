using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that the player entered an uncontrollable rotation state.</summary>
    public sealed record PlayerRotationStarted(
        EntityId EntityId,
        Direction From,
        Direction To) : SimulationEvent(EntityId);
}
