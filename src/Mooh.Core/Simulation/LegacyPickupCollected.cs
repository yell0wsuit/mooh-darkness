using Mooh.Core.Entities;
using Mooh.Core.Legacy;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports recovered state changes caused by collecting an archival pickup.</summary>
    /// <param name="EntityId">The collecting player.</param>
    /// <param name="PickupEntityId">The collected runtime entity.</param>
    /// <param name="Kind">The collected semantic family.</param>
    /// <param name="ScoreAwarded">The score awarded, including any all-treasure bonus.</param>
    public sealed record LegacyPickupCollected(
        EntityId EntityId,
        EntityId PickupEntityId,
        LegacyEntityKind Kind,
        int ScoreAwarded)
        : SimulationEvent(EntityId);
}
