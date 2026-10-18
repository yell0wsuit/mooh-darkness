using System;

using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player started drinking a collected cola, the recovered
    /// <c>ST_BonCola</c> chain. The protection itself starts only when the drink ends.
    /// </summary>
    /// <param name="EntityId">The drinking player.</param>
    /// <param name="Duration">The whole drink, during which the player cannot act.</param>
    public sealed record PlayerColaDrinkStarted(EntityId EntityId, TimeSpan Duration) : SimulationEvent(EntityId);
}
