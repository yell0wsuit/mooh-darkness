using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.SimulationRunner
{
    /// <summary>Renders a live simulation world as emoji rows using the archival board glyphs.</summary>
    public static class LiveBoardRenderer
    {
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
        /// Classifies one live entity. A beetle is spawned at runtime rather than placed, so it
        /// carries no archival identity and is recognized by its behavior component instead.
        /// </summary>
        private static LevelBoardRenderer.BoardGlyph Classify(Entity entity)
        {
            return entity.SmallBeetleBody is not null
                ? LevelBoardRenderer.BeetleGlyph
                : LevelBoardRenderer.ClassifyType(
                    entity.ArchivalIdentity?.ArchivalType ?? string.Empty,
                    null);
        }
    }
}
