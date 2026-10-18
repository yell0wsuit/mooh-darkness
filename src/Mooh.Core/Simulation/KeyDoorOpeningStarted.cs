using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that a collected colored key started its matching door's opening.</summary>
    /// <param name="EntityId">The player at the door.</param>
    /// <param name="DoorEntityId">The opening door entity.</param>
    /// <param name="KeyNumber">The matching key number.</param>
    public sealed record KeyDoorOpeningStarted(
        EntityId EntityId,
        EntityId DoorEntityId,
        int KeyNumber)
        : SimulationEvent(EntityId);
}
