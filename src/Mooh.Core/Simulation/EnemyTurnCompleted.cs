using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that an enemy completed a turn.</summary>
    /// <param name="EntityId">The enemy entity.</param>
    /// <param name="FacingDirection">The direction faced after the turn.</param>
    public sealed record EnemyTurnCompleted(EntityId EntityId, Direction FacingDirection)
        : SimulationEvent(EntityId);
}
