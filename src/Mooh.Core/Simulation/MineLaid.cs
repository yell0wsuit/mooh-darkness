using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that the player laid a mine, the recovered <c>_AEnd_Mine</c>.</summary>
    /// <param name="EntityId">The player.</param>
    /// <param name="MineEntityId">The laid mine.</param>
    /// <param name="Position">The faced cell the mine now lies in.</param>
    public sealed record MineLaid(
        EntityId EntityId,
        EntityId MineEntityId,
        GridPosition Position) : SimulationEvent(EntityId);
}
