using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that an enemy started winding up an attack.</summary>
    /// <param name="EntityId">The attacking enemy.</param>
    /// <param name="TargetCell">The cell that triggered the attack.</param>
    /// <param name="Duration">The complete wind-up duration.</param>
    public sealed record EnemyAttackStarted(
        EntityId EntityId,
        GridPosition TargetCell,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
