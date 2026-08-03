using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a teleporting player landed in the destination cell and began the recovered
    /// <c>ST_TPortOut</c> animation, which is where the player becomes solid again.
    /// </summary>
    /// <param name="EntityId">The arriving player.</param>
    /// <param name="Position">The cell the player landed in.</param>
    public sealed record PlayerTeleportArrived(EntityId EntityId, GridPosition Position)
        : SimulationEvent(EntityId);
}
