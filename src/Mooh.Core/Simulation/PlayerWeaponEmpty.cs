using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the fire control reached the weapon branch with no ammunition. The recovered
    /// path plays <c>SndWeapNo</c>, shows the <c>HlpSitNoAmmo</c> hint, and still consumes the
    /// shared repeat timestamp.
    /// </summary>
    /// <param name="EntityId">The player that tried to fire.</param>
    public sealed record PlayerWeaponEmpty(EntityId EntityId) : SimulationEvent(EntityId);
}
