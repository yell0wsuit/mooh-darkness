using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Motion;

namespace Mooh.SimulationRunner
{
    /// <summary>Renders a live simulation world as emoji using the archival board glyphs.</summary>
    public static class LiveBoardRenderer
    {
        /// <summary>
        /// Renders every entity at its interpolated position.
        /// </summary>
        /// <remarks>
        /// An entity commits its destination cell when movement starts, so its authoritative
        /// <see cref="Entity.Position"/> is already the cell it is heading for while
        /// <see cref="MotionState.Progress"/> runs from zero to one. The drawn position therefore
        /// trails the authoritative cell by the unfinished fraction of the segment. A reversal
        /// drives progress back toward zero without changing the committed cell, and the same
        /// expression carries the entity back to where it came from.
        /// </remarks>
        /// <param name="world">The authoritative runtime world.</param>
        /// <param name="blastCells">
        /// The cells a blast has reached and not yet released. Each is drawn over every entity,
        /// because an explosion is a timed cell effect rather than an entity of its own.
        /// </param>
        /// <returns>The sprites in ascending draw order.</returns>
        /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
        public static IReadOnlyList<PlaySprite> RenderSprites(
            WorldGrid world,
            IReadOnlyCollection<GridPosition> blastCells)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(blastCells);

            LevelBoardRenderer.BoardGlyph blast = LevelBoardRenderer.ExplosionGlyph;
            return
            [
                .. world.Entities
                    .Select(entity =>
                    {
                        LevelBoardRenderer.BoardGlyph glyph = Classify(entity);
                        (double x, double y) = InterpolatedPosition(entity);
                        return new PlaySprite(glyph.Emoji, x, y, glyph.Priority);
                    })
                    .Concat(
                        blastCells.Select(
                            cell => new PlaySprite(blast.Emoji, cell.X, cell.Y, blast.Priority)))
                    .OrderBy(sprite => sprite.Layer),
            ];
        }

        private static (double X, double Y) InterpolatedPosition(Entity entity)
        {
            if (entity.Motion is not MotionState motion)
            {
                return (entity.Position.X, entity.Position.Y);
            }

            double trailing = 1 - motion.Progress;
            (int dx, int dy) = motion.SegmentDirection switch
            {
                Direction.Left => (-1, 0),
                Direction.Up => (0, -1),
                Direction.Right => (1, 0),
                Direction.Down => (0, 1),
                _ => (0, 0),
            };
            return (entity.Position.X - (dx * trailing), entity.Position.Y - (dy * trailing));
        }

        /// <summary>Renders one emoji row per grid row.</summary>
        /// <param name="world">The authoritative runtime world.</param>
        /// <returns>One string per row, each holding one glyph per cell.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
        public static string[] Render(WorldGrid world)
        {
            ArgumentNullException.ThrowIfNull(world);

            string[] rows = new string[world.Height];
            StringBuilder row = new();
            for (int y = 0; y < world.Height; y++)
            {
                _ = row.Clear();
                for (int x = 0; x < world.Width; x++)
                {
                    IReadOnlyList<Entity> occupants = world.GetOccupants(new GridPosition(x, y));
                    _ = row.Append(SelectGlyph(occupants).Emoji);
                }

                rows[y] = row.ToString();
            }

            return rows;
        }

        private static LevelBoardRenderer.BoardGlyph SelectGlyph(IReadOnlyList<Entity> occupants)
        {
            return occupants.Count == 0
                ? LevelBoardRenderer.EmptyGlyph
                : occupants
                    .Select(Classify)
                    .OrderByDescending(glyph => glyph.Priority)
                    .ThenBy(glyph => glyph.LegendOrder)
                    .First();
        }

        /// <summary>
        /// Classifies one live entity. A beetle, a placed big bomb, a fired rocket and the start door
        /// are spawned at runtime rather than placed, so they carry no archival identity and are recognized by their behavior
        /// components instead.
        /// </summary>
        private static LevelBoardRenderer.BoardGlyph Classify(Entity entity)
        {
            return entity.SmallBeetleBody is not null
                ? LevelBoardRenderer.BeetleGlyph
                : entity.BigBombBody is not null
                ? LevelBoardRenderer.BigBombGlyph
                : entity.RocketBody is not null
                ? LevelBoardRenderer.RocketGlyph
                : entity.DoorBody is not null
                ? LevelBoardRenderer.DoorGlyph
                : LevelBoardRenderer.ClassifyType(
                    entity.ArchivalIdentity?.ArchivalType ?? string.Empty,
                    null);
        }
    }
}
