using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player began a hand strike. The recovered <c>ST_StrIn*</c> windup applies
    /// nothing until it ends.
    /// </summary>
    /// <param name="EntityId">The striking player.</param>
    /// <param name="Direction">The struck direction.</param>
    /// <param name="Duration">The windup duration before the strike lands.</param>
    public sealed record PlayerStrikeStarted(
        EntityId EntityId,
        Direction Direction,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
