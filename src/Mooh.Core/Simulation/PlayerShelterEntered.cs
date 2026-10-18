using System;

using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player went into the start door to heal, the recovered
    /// <c>ST_DoorOutStart</c> sequence that <c>DoCtrl</c> enters when the fire control is used on
    /// the door.
    /// </summary>
    /// <param name="EntityId">The player.</param>
    /// <param name="Duration">The whole visit, during which the player is immortal.</param>
    public sealed record PlayerShelterEntered(EntityId EntityId, TimeSpan Duration) : SimulationEvent(EntityId);
}
