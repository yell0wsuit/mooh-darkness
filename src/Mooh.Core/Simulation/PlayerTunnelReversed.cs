using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a travelling player was turned back by a blocked exit, the recovered
    /// <c>bTonFlip</c>. Routing consults <c>DirMap</c> from here on, which is what lets a player
    /// out of a one-way tunnel they have run to the end of.
    /// </summary>
    /// <param name="EntityId">The travelling player.</param>
    /// <param name="Position">The tunnel cell the turnaround happened in.</param>
    /// <param name="Direction">The direction the player now travels.</param>
    public sealed record PlayerTunnelReversed(
        EntityId EntityId,
        GridPosition Position,
        Direction Direction) : SimulationEvent(EntityId);
}
