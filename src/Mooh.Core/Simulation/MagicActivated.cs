using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>A magic gem activated its surrounding cells.</summary>
    /// <param name="EntityId">The affected entity.</param>
    /// <param name="Position">The interaction cell.</param>
    public sealed record MagicActivated(EntityId EntityId, GridPosition Position) : SimulationEvent(EntityId);
}
