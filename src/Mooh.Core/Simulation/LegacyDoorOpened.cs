using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that a collected colored key opened its matching door.</summary>
    /// <param name="EntityId">The player opening the door.</param>
    /// <param name="DoorEntityId">The removed door entity.</param>
    /// <param name="KeyNumber">The matching key number.</param>
    /// <param name="ScoreAwarded">The recovered door score.</param>
    public sealed record LegacyDoorOpened(
        EntityId EntityId,
        EntityId DoorEntityId,
        int KeyNumber,
        int ScoreAwarded)
        : SimulationEvent(EntityId);
}
