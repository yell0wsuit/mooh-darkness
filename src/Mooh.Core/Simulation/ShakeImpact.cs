using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports the contact point of an impact that dispatched a shake handler.</summary>
    /// <param name="EntityId">The entity producing the impact.</param>
    /// <param name="X">The contact column in world cells.</param>
    /// <param name="Y">The contact row in world cells.</param>
    public sealed record ShakeImpact(EntityId EntityId, double X, double Y) : SimulationEvent(EntityId);
}
