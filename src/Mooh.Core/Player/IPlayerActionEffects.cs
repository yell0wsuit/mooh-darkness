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

        /// <summary>
        /// Destroys a cell's crushable occupants, applying the
        /// <c>CellObjDump(n, CNT_CMaskCallCanPushTh)</c> loop that both <c>GamePlayer.CtrlMove</c>
        /// and <c>Game._CellPlKill</c> run over <c>Game.ObjKill</c>.
        /// </summary>
        /// <remarks>
        /// The engine has one kill state — a flag every cell query filters on — so a crushed
        /// object has to leave the world by the same path whatever crushed it, rather than being
        /// removed silently.
        /// </remarks>
        /// <param name="cell">The cell whose crushable occupants are destroyed.</param>
        /// <param name="events">The ordered event sink.</param>
        void CrushCell(GridPosition cell, List<SimulationEvent> events);

        /// <summary>Registers a thrown grenade in a cleared cell.</summary>
        /// <param name="cell">The cell that receives the grenade.</param>
        /// <param name="events">The ordered event sink.</param>
        void PlaceGrenade(GridPosition cell, List<SimulationEvent> events);
    }
}
