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
        /// Offers an occupant the recovered <c>PlPush</c> handler a walking push dispatches before
        /// it moves anything. The balloon is the only placed type that defines one.
        /// </summary>
        /// <param name="target">The occupant being pushed.</param>
        /// <param name="events">The ordered event sink.</param>
        void OfferPush(Entity target, List<SimulationEvent> events);

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

        /// <summary>
        /// Launches a directional arrow, applying the recovered <c>_DoFly_&lt;dir&gt;</c>. A hand
        /// strike reaches it through the <c>PlCtrl</c> dispatch <c>_AEnd_Str</c> runs before it
        /// offers the occupant a push.
        /// </summary>
        /// <param name="arrow">The struck arrow.</param>
        /// <param name="events">The ordered event sink.</param>
        void LaunchArrow(Entity arrow, List<SimulationEvent> events);

        /// <summary>
        /// Starts a world rotator's <c>ST_Work</c> windup, applying the recovered
        /// <c>GoTo_Work</c>. It shares the arrow's route in: <c>_AEnd_Str</c> offers the struck
        /// cell <c>PlCtrl</c> before it offers a push, and a rotator answers.
        /// </summary>
        /// <param name="rotator">The struck rotator.</param>
        /// <param name="events">The ordered event sink.</param>
        void TriggerWorldRotation(Entity rotator, List<SimulationEvent> events);

        /// <summary>
        /// Shatters a frozen monster, applying the recovered <c>_Ice_Ctrl</c> that <c>IceOn</c>
        /// installs as the monster's <c>PlCtrl</c>. A hand strike reaches it through the same
        /// <c>PlCtrl</c> dispatch that launches an arrow.
        /// </summary>
        /// <param name="monster">The struck frozen monster.</param>
        /// <param name="events">The ordered event sink.</param>
        void ShatterFrozenMonster(Entity monster, List<SimulationEvent> events);

        /// <summary>
        /// Applies the recovered <c>Game._ObjExplWrd</c> to one cell: its explodable occupants
        /// are destroyed, the player in it is hurt and the cell is locked. A teleport arrival
        /// runs it when the destination cell is still occupied.
        /// </summary>
        /// <param name="cell">The cell the blast reaches.</param>
        /// <param name="sourceEntityId">The entity the blast is attributed to.</param>
        /// <param name="events">The ordered event sink.</param>
        void ExplodeCell(GridPosition cell, EntityId sourceEntityId, List<SimulationEvent> events);

        /// <summary>Registers a placed big bomb in the player's own cell, the recovered <c>_AEnd_BBomb</c>.</summary>
        /// <param name="cell">The player's cell.</param>
        /// <param name="events">The ordered event sink.</param>
        void PlaceBigBomb(GridPosition cell, List<SimulationEvent> events);

        /// <summary>
        /// Lays a mine in the faced cell, the recovered <c>_AEnd_Mine</c>, which clears the cell
        /// again before the mine is registered.
        /// </summary>
        /// <param name="cell">The faced cell.</param>
        /// <param name="events">The ordered event sink.</param>
        void LayMine(GridPosition cell, List<SimulationEvent> events);

        /// <summary>Registers a thrown grenade in a cleared cell.</summary>
        /// <param name="cell">The cell that receives the grenade.</param>
        /// <param name="events">The ordered event sink.</param>
        void PlaceGrenade(GridPosition cell, List<SimulationEvent> events);
    }
}
