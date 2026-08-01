using Mooh.Core.Combat;
using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports a damage request ignored by target health state.</summary>
    /// <param name="EntityId">The intended target entity.</param>
    /// <param name="SourceEntityId">The entity responsible for the request.</param>
    /// <param name="Kind">The semantic damage category.</param>
    /// <param name="Reason">The reason the request was ignored.</param>
    public sealed record DamageIgnored(
        EntityId EntityId,
        EntityId SourceEntityId,
        DamageKind Kind,
        DamageResult Reason) : SimulationEvent(EntityId);
}
