using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a thrown grenade was registered in the world and one grenade was spent. The
    /// recovered <c>GameObjGrenade</c> creates the internal <c>WBombInt</c> type, which is the
    /// placed <c>WBomb</c> pickup's behavior exactly, so it falls, is collectible again, and
    /// detonates on a landing, a shake, or a blast.
    /// </summary>
    /// <param name="EntityId">The throwing player.</param>
    /// <param name="GrenadeEntityId">The created grenade.</param>
    /// <param name="Position">The cell the grenade was placed in.</param>
    public sealed record PlayerGrenadePlaced(
        EntityId EntityId,
        EntityId GrenadeEntityId,
        GridPosition Position) : SimulationEvent(EntityId);
}
