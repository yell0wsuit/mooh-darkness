using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a directional arrow was triggered and began its recovered <c>ST_Work</c>
    /// windup. The arrow always flies in its own fixed direction, whatever triggered it, and it
    /// stops falling the moment it is launched.
    /// </summary>
    /// <param name="EntityId">The launched arrow.</param>
    /// <param name="Position">The cell the arrow was launched from.</param>
    /// <param name="Direction">The fixed direction the arrow will fly in.</param>
    /// <param name="Duration">The windup duration before flight starts.</param>
    public sealed record ArrowLaunched(
        EntityId EntityId,
        GridPosition Position,
        Direction Direction,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
