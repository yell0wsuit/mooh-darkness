using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that a collected colored key opened its matching door.</summary>
    /// <param name="EntityId">The player opening the door.</param>
    /// <param name="DoorEntityId">The removed door entity.</param>
    /// <param name="KeyNumber">The matching key number.</param>
    /// <param name="ScoreAwarded">The recovered door score.</param>
    /// <param name="Position">The door cell retained after its removal.</param>
    public sealed record KeyDoorOpened(
        EntityId EntityId,
        EntityId DoorEntityId,
        int KeyNumber,
        int ScoreAwarded,
        GridPosition? Position = null)
        : SimulationEvent(EntityId);
}
