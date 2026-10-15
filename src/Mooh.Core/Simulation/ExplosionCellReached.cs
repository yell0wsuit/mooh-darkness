using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Identifies how a blast reached its cell.</summary>
    public enum ExplosionPattern
    {
        /// <summary>An ordinary area blast or the center of a barrel blast.</summary>
        Area,
        /// <summary>An outward arm of a barrel blast.</summary>
        CrossArm,
        /// <summary>A spreading big-bomb wave.</summary>
        Flood,
    }

    /// <summary>Reports an explosion wave reaching one cell.</summary>
    /// <param name="EntityId">The source entity responsible for this explosion wave.</param>
    /// <param name="Position">The reached cell.</param>
    /// <param name="Pattern">The propagation pattern.</param>
    /// <param name="Direction">The outward direction of a cross arm.</param>
    public sealed record ExplosionCellReached(EntityId EntityId, GridPosition Position,
        ExplosionPattern Pattern = ExplosionPattern.Area, Direction? Direction = null)
        : SimulationEvent(EntityId);
}
