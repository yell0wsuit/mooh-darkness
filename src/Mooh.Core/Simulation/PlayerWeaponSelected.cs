using Mooh.Core.Entities;
using Mooh.Core.Player;

namespace Mooh.Core.Simulation
{
    /// <summary>Reports that the player's current weapon changed, the recovered <c>WeapCurrSet</c>.</summary>
    /// <param name="EntityId">The player.</param>
    /// <param name="Weapon">The newly current weapon.</param>
    public sealed record PlayerWeaponSelected(EntityId EntityId, WeaponKind Weapon) : SimulationEvent(EntityId);
}
