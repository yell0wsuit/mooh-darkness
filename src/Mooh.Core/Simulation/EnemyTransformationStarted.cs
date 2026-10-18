using System;

using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that a glasses bonus started changing an enemy's form.</summary>
    /// <param name="EntityId">The transforming enemy.</param>
    /// <param name="IsSmall">Whether the destination form is small.</param>
    /// <param name="Duration">The recovered transition duration.</param>
    public sealed record EnemyTransformationStarted(
        EntityId EntityId,
        bool IsSmall,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
