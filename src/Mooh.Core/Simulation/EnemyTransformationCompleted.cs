using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that a glasses form transition completed.</summary>
    /// <param name="EntityId">The transformed enemy.</param>
    /// <param name="IsSmall">Whether the resulting form is small.</param>
    public sealed record EnemyTransformationCompleted(
        EntityId EntityId,
        bool IsSmall) : SimulationEvent(EntityId);
}
