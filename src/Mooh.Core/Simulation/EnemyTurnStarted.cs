using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that an enemy started a turn.</summary>
    /// <param name="EntityId">The enemy entity.</param>
    /// <param name="FromDirection">The direction faced before the turn.</param>
    /// <param name="ToDirection">The direction that will be faced after the turn.</param>
    /// <param name="Duration">The complete turn duration.</param>
    public sealed record EnemyTurnStarted(
        EntityId EntityId,
        Direction FromDirection,
        Direction ToDirection,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
