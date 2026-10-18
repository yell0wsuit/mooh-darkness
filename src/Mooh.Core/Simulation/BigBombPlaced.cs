using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that the player's big bomb was placed, the recovered <c>_AEnd_BBomb</c>.</summary>
    /// <param name="EntityId">The player.</param>
    /// <param name="BombEntityId">The placed bomb.</param>
    /// <param name="Position">The player's own cell, where the bomb now lies.</param>
    public sealed record BigBombPlaced(
        EntityId EntityId,
        EntityId BombEntityId,
        GridPosition Position) : SimulationEvent(EntityId);
}
