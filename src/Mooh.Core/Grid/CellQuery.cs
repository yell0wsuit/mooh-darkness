using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Interactions;

namespace Mooh.Core.Grid
{
    /// <summary>
    /// Holds the recovered cell-occupancy queries every system shares.
    /// </summary>
    /// <remarks>
    /// Movement commits occupancy as soon as a segment begins. Ordinary occupancy queries
    /// therefore include moving entities; only explicit stationary-support checks exclude them.
    /// </remarks>
    internal static class CellQuery
    {
        /// <summary>
        /// Gets the occupants an ordinary query sees. A zero <c>ulMaskAny</c> makes the world list
        /// skip every <c>CNT_OrMaskShadow</c> carrier, so a message table, a teleporter, fire, a
        /// deployed mine and a spawned beetle are invisible here. A query that passes an explicit
        /// positive mask sees them again and must not use this.
        /// </summary>
        /// <param name="world">The authoritative world.</param>
        /// <param name="cell">The cell to inspect. It must be inside the world.</param>
        /// <returns>The visible occupants, in the world's newest-arrival-first order.</returns>
        internal static IEnumerable<Entity> VisibleOccupants(WorldGrid world, GridPosition cell)
        {
            return world.GetOccupants(cell)
                .Where(occupant => (occupant.Interactions & InteractionMask.OccupancyTransparent) == 0);
        }

        /// <summary>
        /// Reports whether a cell holds any visible occupant, modelling
        /// <c>CellObjCheck(c, 0, 0, True)</c>.
        /// </summary>
        /// <remarks>
        /// An entity part-way through its own move counts. Occupancy is committed when movement
        /// starts, so such an entity already owns the cell it is entering, and both the walking
        /// push and the strike-push chain have to stop against it.
        /// </remarks>
        /// <param name="world">The authoritative world.</param>
        /// <param name="cell">The cell to inspect. It must be inside the world.</param>
        /// <returns><see langword="true"/> when the cell holds a visible occupant.</returns>
        internal static bool IsOccupied(WorldGrid world, GridPosition cell)
        {
            return world.AnyOccupant(cell, 0, static (occupant, _) =>
                (occupant.Interactions & InteractionMask.OccupancyTransparent) == 0);
        }

        /// <summary>
        /// Gets the occupants a push or a placed weapon crushes, modelling
        /// <c>CellObjDump(c, CNT_CMaskCallCanPushTh)</c>.
        /// </summary>
        /// <remarks>
        /// The recovered call passes a positive <c>ulMaskAny</c>, so unlike
        /// <see cref="VisibleOccupants"/> it is not subject to the shadow filter — it simply never
        /// matches a shadow carrier, because none of them is crushable. Callers differ in what
        /// they do with the result: the weapon path destroys them as entities, the walking push
        /// removes them.
        /// </remarks>
        /// <param name="world">The authoritative world.</param>
        /// <param name="cell">The cell to inspect. It must be inside the world.</param>
        /// <returns>The crushable occupants of the cell.</returns>
        internal static IEnumerable<Entity> CrushableOccupants(WorldGrid world, GridPosition cell)
        {
            return world.GetOccupants(cell)
                .Where(occupant => (occupant.Interactions & InteractionMask.CanBePushedThrough) != 0);
        }

        /// <summary>
        /// Reports whether a monster may walk into a cell, modelling
        /// <c>CellObjCheck(f, ~CNT_CMaskMonShadow, CNT_CMaskMonShadow, True)</c>.
        /// </summary>
        /// <remarks>
        /// The recovered test passes every interaction bit except the monster-shadow bit, so any
        /// occupant that is not explicitly transparent to monsters blocks the walk — collectibles,
        /// fire and world-rotation objects included. The positive mask also means this query is
        /// not subject to the shadow filter <see cref="VisibleOccupants"/> applies.
        /// </remarks>
        /// <param name="world">The authoritative world.</param>
        /// <param name="monster">The monster attempting the walk.</param>
        /// <param name="destination">The cell it would enter.</param>
        /// <returns><see langword="true"/> when the monster may enter.</returns>
        internal static bool CanMonsterEnter(WorldGrid world, Entity monster, GridPosition destination)
        {
            return (monster.Interactions & InteractionMask.MovementLocked) == 0
                && world.Contains(destination)
                && !world.AnyOccupant(destination, 0, static (occupant, _) =>
                    (occupant.Interactions & InteractionMask.MonsterTransparent) == 0);
        }

        /// <summary>
        /// Reports whether a still, visible occupant survives in a cell a blast has just reached,
        /// modelling <c>CellObjCheck(c, 0, CNT_OrMaskShadow, True)</c>. The recovered
        /// <c>_ObjExplCross</c> stops an arm there, and <c>_ObjExplWrd</c> skips its blast graphic.
        /// </summary>
        /// <param name="world">The authoritative world.</param>
        /// <param name="cell">The reached cell.</param>
        /// <returns><see langword="true"/> when something still and non-shadow remains.</returns>
        internal static bool HoldsBlastSurvivor(WorldGrid world, GridPosition cell)
        {
            return world.Contains(cell)
                && world.AnyOccupant(cell, 0, static (occupant, _) => occupant.Motion is null
                        && (occupant.Interactions
                            & (InteractionMask.OccupancyTransparent | InteractionMask.MonsterTransparent)) == 0);
        }

        /// <summary>
        /// Reports whether a cell holds an entity moving against a direction, modelling the
        /// recovered <c>CellDirLocked</c> test that selects the <c>CNT_OPrm_AirSlow</c> multiplier.
        /// </summary>
        /// <param name="world">The authoritative world.</param>
        /// <param name="cell">The cell to inspect. A cell outside the world holds nothing.</param>
        /// <param name="direction">The direction the entering entity is travelling.</param>
        /// <returns><see langword="true"/> when the cell counter-moves.</returns>
        internal static bool HasCounterMotion(WorldGrid world, GridPosition cell, Direction direction)
        {
            return world.Contains(cell)
                && world.AnyOccupant(cell, direction, static (occupant, travelDirection) =>
                    occupant.Motion is not null && occupant.Motion.TravelDirection != travelDirection);
        }
    }
}
