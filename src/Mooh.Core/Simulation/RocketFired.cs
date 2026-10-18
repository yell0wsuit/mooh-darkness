using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that the player fired a rocket, the recovered <c>_AEnd_Rocket</c>.</summary>
    /// <param name="EntityId">The player.</param>
    /// <param name="RocketEntityId">The fired rocket.</param>
    /// <param name="Position">The faced cell the rocket starts from.</param>
    /// <param name="Direction">The rocket's fixed flight direction, left or right.</param>
    public sealed record RocketFired(
        EntityId EntityId,
        EntityId RocketEntityId,
        GridPosition Position,
        Direction Direction) : SimulationEvent(EntityId);
}
