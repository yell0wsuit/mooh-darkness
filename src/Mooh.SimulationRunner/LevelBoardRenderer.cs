using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mooh.Core.Content;

namespace Mooh.SimulationRunner
{
    /// <summary>Formats an archival level as an observational emoji board.</summary>
    public static class LevelBoardRenderer
    {
        private static readonly BoardGlyph Player = new("🐔", "player", 100, 0);
        private static readonly BoardGlyph GreenMonster = new("🟢", "green monster", 90, 10);
        private static readonly BoardGlyph BlueMonster = new("🔵", "blue monster", 90, 11);
        private static readonly BoardGlyph BioMonster = new("🦠", "bio monster", 90, 12);
        private static readonly BoardGlyph Fire = new("🔥", "fire", 85, 20);
        private static readonly BoardGlyph Grenade = new("💣", "grenade", 80, 21);
        private static readonly BoardGlyph Rocket = new("🚀", "rocket", 80, 22);
        private static readonly BoardGlyph Mine = new("🧨", "mine", 80, 23);
        private static readonly BoardGlyph BigBomb = new("☢️", "big bomb", 80, 24);
        private static readonly BoardGlyph Diamond = new("💎", "diamond", 75, 30);
        private static readonly BoardGlyph Coin = new("🪙", "coin", 75, 31);
        private static readonly BoardGlyph Food = new("🍗", "food", 75, 32);
        private static readonly BoardGlyph Bonus = new("🎁", "bonus", 75, 33);
        private static readonly BoardGlyph Key = new("🔑", "key", 75, 34);
        private static readonly BoardGlyph Bottle = new("🧪", "bottle", 70, 40);
        private static readonly BoardGlyph Barrel = new("🛢️", "barrel", 70, 41);
        private static readonly BoardGlyph Balloon = new("🎈", "balloon", 70, 42);
        private static readonly BoardGlyph Bubble = new("🫧", "bubble", 70, 43);
        private static readonly BoardGlyph Rock = new("🪨", "rock", 70, 44);
        private static readonly BoardGlyph Magic = new("✨", "magic object", 70, 45);
        private static readonly BoardGlyph Arrow = new("➡️", "gravity arrow", 65, 46);
        private static readonly BoardGlyph WorldRotation = new("🔄", "world rotation", 65, 47);
        private static readonly BoardGlyph Portal = new("🌀", "teleporter", 65, 48);
        private static readonly BoardGlyph Door = new("🚪", "door", 65, 49);
        private static readonly BoardGlyph Table = new("📜", "message table", 60, 50);
        private static readonly BoardGlyph Pipe = new("🚰", "pipe or tunnel", 50, 60);
        private static readonly BoardGlyph Metal = new("⬛", "metal", 10, 70);
        private static readonly BoardGlyph Solid = new("🧱", "solid", 10, 71);
        private static readonly BoardGlyph Wood = new("🪵", "wood", 10, 72);
        private static readonly BoardGlyph Diggable = new("🟫", "diggable", 10, 73);
        private static readonly BoardGlyph Beetle = new("🐛", "small beetle", 90, 13);

        /// <summary>
        /// A reached blast cell. No archival object produces it, so it never enters the static
        /// board legend; it is drawn over every entity for as long as the cell's blast lock lasts.
        /// </summary>
        private static readonly BoardGlyph Explosion = new("💥", "explosion", 200, 1);
        private static readonly BoardGlyph Unknown = new("❓", "unknown", 80, 90);
        private static readonly BoardGlyph Empty = new("⬜", "empty", 0, 100);

        /// <summary>Gets the glyph used for a cell with no occupants.</summary>
        internal static BoardGlyph EmptyGlyph { get; } = Empty;

        /// <summary>Gets the glyph used for a beetle spawned at runtime rather than placed.</summary>
        internal static BoardGlyph BeetleGlyph { get; } = Beetle;

        /// <summary>Gets the glyph drawn over a cell a blast has reached.</summary>
        internal static BoardGlyph ExplosionGlyph { get; } = Explosion;

        /// <summary>Renders one validated archival level document.</summary>
        /// <param name="level">The archival level to inspect.</param>
        /// <param name="sourceName">The display name written in the board header.</param>
        /// <returns>A deterministic, newline-terminated board report.</returns>
        public static string Render(LevelDocument level, string sourceName)
        {
            ArgumentNullException.ThrowIfNull(level);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

            IReadOnlyList<LevelObject> objects = level.Objects
                ?? throw new ArgumentException("The level object collection cannot be null.", nameof(level));
            Dictionary<LevelPosition, List<LevelObject>> objectsByPosition = objects
                .GroupBy(levelObject => levelObject.Position)
                .ToDictionary(group => group.Key, group => group.ToList());
            HashSet<BoardGlyph> usedGlyphs = [];
            HashSet<string> unknownTypes = [];
            StringBuilder result = new();
            _ = result.Append("Emoji board: ")
                .Append(sourceName)
                .Append(" (")
                .Append(level.Size.Width)
                .Append('×')
                .Append(level.Size.Height)
                .Append(", ")
                .Append(objects.Count)
                .AppendLine(" objects)");

            for (int y = 0; y < level.Size.Height; y++)
            {
                for (int x = 0; x < level.Size.Width; x++)
                {
                    LevelPosition position = new(x, y);
                    BoardGlyph glyph = objectsByPosition.TryGetValue(position, out List<LevelObject>? occupants)
                        ? SelectGlyph(occupants, unknownTypes)
                        : Empty;
                    _ = usedGlyphs.Add(glyph);
                    _ = result.Append(glyph.Emoji);
                }

                _ = result.AppendLine();
            }

            _ = result.Append("Legend: ")
                .AppendJoin(
                    " | ",
                    usedGlyphs
                        .OrderBy(glyph => glyph.LegendOrder)
                        .Select(glyph => $"{glyph.Emoji} {glyph.Label}"))
                .AppendLine();
            if (unknownTypes.Count != 0)
            {
                _ = result.Append("Unknown legacy types: ")
                    .AppendJoin(", ", unknownTypes.Order(StringComparer.Ordinal))
                    .AppendLine();
            }

            return result.ToString();
        }

        private static BoardGlyph SelectGlyph(
            IReadOnlyList<LevelObject> occupants,
            ISet<string> unknownTypes)
        {
            return occupants
                .Select(levelObject => Classify(levelObject, unknownTypes))
                .OrderByDescending(glyph => glyph.Priority)
                .ThenBy(glyph => glyph.LegendOrder)
                .First();
        }

        private static BoardGlyph Classify(LevelObject levelObject, ISet<string> unknownTypes)
        {
            return ClassifyType(levelObject.Type ?? string.Empty, unknownTypes);
        }

        /// <summary>Selects the glyph for one exact archival type name.</summary>
        /// <param name="type">The exact archival type name.</param>
        /// <param name="unknownTypes">An optional sink recording unrecognized type names.</param>
        /// <returns>The matching glyph, or the unknown glyph.</returns>
        internal static BoardGlyph ClassifyType(string type, ISet<string>? unknownTypes)
        {
            BoardGlyph? glyph = type switch
            {
                "OPlayer" => Player,
                "MGrL" or "MGrT" or "MGrR" or "MGrB" => GreenMonster,
                "MBlueL" or "MBlueT" or "MBlueR" or "MBlueB" => BlueMonster,
                "MMBio" => BioMonster,
                "OFire" => Fire,
                "WBomb" or "WGrBox" => Grenade,
                "WROne" or "WRBox" => Rocket,
                "WMine" or "WMinesBox" => Mine,
                "WBBomb" => BigBomb,
                "ODiam01" or "ODiam02" or "ODiam03" or "ODiam04" or "ODiam05" => Diamond,
                "OCoin01" or "OCoin02" or "OCoin03" => Coin,
                "OEat01" or "OEat02" or "OEat03" => Food,
                "BCola" or "BIce" or "BBag" => Bonus,
                "OKDKey1" or "OKDKey2" or "OKDKey3" => Key,
                "OBottle" => Bottle,
                "OBarel" => Barrel,
                "OBaloon" => Balloon,
                "OBubble" => Bubble,
                "ORock01" or "OWeight" => Rock,
                "OMagic" => Magic,
                "OArrowL" or "OArrowT" or "OArrowR" or "OArrowB" => Arrow,
                "OWRotL" or "OWRotR" => WorldRotation,
                "OTPort" => Portal,
                "OKDDoor1" or "OKDDoor2" or "OKDDoor3" => Door,
                "OTable" => Table,
                "PipeHor" or "PipeVert" or "PipeLeftTop" or "PipeRightBottom"
                    or "PipeTopRight" or "PipeBottomLeft"
                    or "TonnelHor" or "TonnelVert" or "TonnelLeftTop"
                    or "TonnelRightBottom" or "TonnelTopRight" or "TonnelBottomLeft"
                    or "TonnelDL2R" or "TonnelDR2L" or "TonnelDT2B" or "TonnelDB2T" => Pipe,
                "CellsMetal01" or "CellsMetalLeft" or "CellsMetalTop"
                    or "CellsMetalRight" or "CellsMetalBottom" => Metal,
                "CellsSolid01" or "CellsSolidPl" or "CellsSolidWd"
                    or "DCellsSolid01" or "JCellsSolid01" => Solid,
                "CellsWdLeft" or "CellsWdTop" or "CellsWdRight" or "CellsWdBottom" => Wood,
                "CellsEat01" or "DCellsEat01" or "JCellsEat01" => Diggable,
                _ => null,
            };

            if (glyph is not null)
            {
                return glyph;
            }

            _ = unknownTypes?.Add(type);
            return Unknown;
        }

        internal sealed record BoardGlyph(
            string Emoji,
            string Label,
            int Priority,
            int LegendOrder);
    }
}
