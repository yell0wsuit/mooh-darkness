using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player began climbing into a tunnel, the recovered <c>ST_TonIn*</c>. The
    /// player stays in its own cell for the whole animation and only starts travelling when it
    /// ends.
    /// </summary>
    /// <param name="EntityId">The entering player.</param>
    /// <param name="Position">The tunnel cell the player is entering.</param>
    /// <param name="Direction">The direction the player entered from.</param>
    /// <param name="Duration">The entry animation duration.</param>
    public sealed record PlayerEnteredTunnel(
        EntityId EntityId,
        GridPosition Position,
        Direction Direction,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
