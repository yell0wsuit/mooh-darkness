using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Player;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player started using a rocket, a mine or the big bomb, the recovered
    /// <c>PlStart</c> entering that weapon's animation. The grenade reports
    /// <see cref="PlayerGrenadeThrowStarted"/> instead.
    /// </summary>
    /// <param name="EntityId">The player.</param>
    /// <param name="Weapon">The weapon being used.</param>
    /// <param name="Direction">The direction the player faces while using it.</param>
    /// <param name="Duration">The animation, during which the player cannot act.</param>
    public sealed record PlayerWeaponUseStarted(
        EntityId EntityId,
        WeaponKind Weapon,
        Direction Direction,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
