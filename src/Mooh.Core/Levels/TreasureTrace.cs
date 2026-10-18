using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;

namespace Mooh.Core.Levels
{
    /// <summary>
    /// Reproduces the recovered treasure trace, <c>Game._TraceCount</c> and <c>Game._Trace</c>: a
    /// breadth-first walk from the player that decides which treasure the all-treasure bonus
    /// needs.
    /// </summary>
    /// <remarks>
    /// The walk visits every cell once. It enters any neighbor unless that neighbor holds a
    /// <c>CNT_CMaskTracePlLock</c> carrier — only the five metal cell types — and a tunnel is
    /// entered only where its <c>EnDirMap</c> accepts the direction and left only through the
    /// directions its <c>DirMap</c> lists. Every teleporter in a visited cell adds its link
    /// destinations without the metal test. Nothing else, earth and rock included, stops it.
    /// </remarks>
    internal static class TreasureTrace
    {
        private static readonly Direction[] AllDirections =
            [Direction.Left, Direction.Up, Direction.Right, Direction.Down];

        /// <summary>Counts the treasure the player can reach, the recovered <c>_TraceCount</c>.</summary>
        internal static int CountReachable(WorldGrid world, GridPosition from)
        {
            return ReachableCells(world, from)
                .Sum(cell => world.GetOccupants(cell).Count(occupant => occupant.IsTreasure));
        }

        /// <summary>
        /// Reports whether any treasure other than <paramref name="excluded"/> is still reachable,
        /// the recovered <c>_Trace</c>. The eaten treasure is excluded because the original has
        /// already killed it, which removes it from every cell query.
        /// </summary>
        internal static bool AnyReachable(WorldGrid world, GridPosition from, EntityId excluded)
        {
            return ReachableCells(world, from).Any(
                cell => world.GetOccupants(cell).Any(
                    occupant => occupant.IsTreasure && occupant.Id != excluded));
        }

        private static IEnumerable<GridPosition> ReachableCells(WorldGrid world, GridPosition from)
        {
            if (!world.Contains(from) || !world.Entities.Any(entity => entity.IsTreasure))
            {
                yield break;
            }

            HashSet<GridPosition> visited = [from];
            Queue<GridPosition> queue = new([from]);
            while (queue.Count != 0)
            {
                GridPosition cell = queue.Dequeue();
                yield return cell;

                IReadOnlyList<Entity> occupants = world.GetOccupants(cell);
                foreach (TeleporterBody teleporter in occupants.Select(occupant => occupant.TeleporterBody).OfType<TeleporterBody>())
                {
                    foreach (GridPosition link in teleporter.Links)
                    {
                        if (world.Contains(link) && visited.Add(link))
                        {
                            queue.Enqueue(link);
                        }
                    }
                }

                TunnelShape? tunnel = TunnelAt(world, cell);
                IEnumerable<Direction> exits = tunnel is null
                    ? AllDirections
                    : AllDirections
                        .Select(direction => tunnel.Route(direction, ignoreOneWay: true))
                        .OfType<Direction>();
                foreach (Direction exit in exits)
                {
                    GridPosition next = cell.Offset(exit);
                    if (!world.Contains(next) || visited.Contains(next))
                    {
                        continue;
                    }

                    TunnelShape? entered = TunnelAt(world, next);
                    if (entered is not null && entered.Route(exit) is null)
                    {
                        continue;
                    }

                    _ = visited.Add(next);
                    if (!world.GetOccupants(next).Any(IsTraceLock))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        private static TunnelShape? TunnelAt(WorldGrid world, GridPosition cell)
        {
            return world.GetOccupants(cell)
                .Select(occupant => occupant.TunnelBody?.Shape)
                .FirstOrDefault(shape => shape is not null);
        }

        private static bool IsTraceLock(Entity entity)
        {
            return entity.ArchivalIdentity?.Kind == EntityKind.MetalTerrain;
        }
    }
}
