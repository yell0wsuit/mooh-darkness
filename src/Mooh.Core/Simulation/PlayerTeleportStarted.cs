using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player used a teleporter and began the recovered <c>ST_TPortIn</c>
    /// animation. The destination is already decided at this point, because
    /// <c>CGameObjTPort.PlCtrl</c> takes it and rotates the link list before the player moves.
    /// </summary>
    /// <param name="EntityId">The teleporting player.</param>
    /// <param name="Origin">The cell the player left from.</param>
    /// <param name="Destination">The linked cell the player is being sent to.</param>
    /// <param name="Duration">The entry animation duration.</param>
    public sealed record PlayerTeleportStarted(
        EntityId EntityId,
        GridPosition Origin,
        GridPosition Destination,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
