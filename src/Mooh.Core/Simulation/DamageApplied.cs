using Mooh.Core.Combat;
using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports health damage applied to an entity.</summary>
    /// <param name="EntityId">The damaged entity.</param>
    /// <param name="SourceEntityId">The entity responsible for the damage.</param>
    /// <param name="Kind">The semantic damage category.</param>
    /// <param name="Amount">The applied damage amount.</param>
    /// <param name="RemainingHealth">The target health after damage.</param>
    public sealed record DamageApplied(
        EntityId EntityId,
        EntityId SourceEntityId,
        DamageKind Kind,
        double Amount,
        double RemainingHealth) : SimulationEvent(EntityId);
}
