using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that the player's timed rotation completed.</summary>
    public sealed record PlayerRotationCompleted(
        EntityId EntityId,
        Direction Direction) : SimulationEvent(EntityId);
}
