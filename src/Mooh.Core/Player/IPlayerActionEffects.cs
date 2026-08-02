using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Simulation;

namespace Mooh.Core.Player
{
    /// <summary>
    /// Exposes the world-level effects a fire-control action produces. The player controller owns
    /// the decision and the timings; destruction, shakes, and spawning stay with the simulation
    /// coordinator that already owns them for every other path.
    /// </summary>
    internal interface IPlayerActionEffects
    {
        /// <summary>
        /// Starts a struck object sliding, applying the recovered <c>CPlPushBase.OnPlPush</c>.
        /// </summary>
        /// <param name="target">The struck object.</param>
        /// <param name="direction">The strike direction.</param>
        /// <param name="events">The ordered event sink.</param>
        void PushStruck(Entity target, Direction direction, List<SimulationEvent> events);

        /// <summary>
        /// Applies the recovered <c>Game._CellPlKill</c> to a cell a weapon is being placed in:
        /// every crushable occupant is destroyed and the cell is locked.
        /// </summary>
        /// <param name="cell">The cell being cleared.</param>
        /// <param name="events">The ordered event sink.</param>
        void ClearWeaponCell(GridPosition cell, List<SimulationEvent> events);

        /// <summary>Registers a thrown grenade in a cleared cell.</summary>
        /// <param name="cell">The cell that receives the grenade.</param>
        /// <param name="events">The ordered event sink.</param>
        void PlaceGrenade(GridPosition cell, List<SimulationEvent> events);
    }
}
