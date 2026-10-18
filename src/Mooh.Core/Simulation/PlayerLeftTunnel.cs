using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player arrived in a cell holding no tunnel and began climbing back out,
    /// the recovered <c>ST_TonOut*</c>.
    /// </summary>
    /// <param name="EntityId">The leaving player.</param>
    /// <param name="Position">The cell the player left the tunnel network in.</param>
    /// <param name="Direction">The direction the player was travelling.</param>
    /// <param name="Duration">The exit animation duration.</param>
    public sealed record PlayerLeftTunnel(
        EntityId EntityId,
        GridPosition Position,
        Direction Direction,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
