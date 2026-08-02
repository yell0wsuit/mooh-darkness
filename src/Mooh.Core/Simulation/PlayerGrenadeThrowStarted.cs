using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player began placing a grenade. The recovered <c>Weap_Gas</c> clears the
    /// target cell immediately and the grenade itself appears when <c>ST_Bomb*</c> ends.
    /// </summary>
    /// <param name="EntityId">The throwing player.</param>
    /// <param name="Direction">The direction of the target cell.</param>
    /// <param name="Duration">The placement duration before the grenade appears.</param>
    public sealed record PlayerGrenadeThrowStarted(
        EntityId EntityId,
        Direction Direction,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
