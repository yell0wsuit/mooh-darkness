using System;
using System.Collections.Generic;

using Mooh.Core.Content;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies observational emoji rendering of archival level documents.</summary>
    public sealed class LevelBoardRendererTests
    {
        /// <summary>The dormant bag registration is reported as unsupported rather than a playable bonus.</summary>
        [Fact]
        public void ReportsUnusedBagAsUnknown()
        {
            LevelDocument level = CreateLevel(1, 1, NewObject(1, "BBag", 0, 0));

            string result = LevelBoardRenderer.Render(level, "unused.json");

            Assert.Contains("Unknown legacy types: BBag", result, StringComparison.Ordinal);
        }

        /// <summary>Verifies known objects are rendered as stable emoji cells with a used-glyph legend.</summary>
        [Fact]
        public void RendersKnownObjectsAndEmptyCells()
        {
            LevelDocument level = CreateLevel(
                3,
                2,
                NewObject(1, "CellsMetal01", 0, 0),
                NewObject(2, "OPlayer", 1, 0),
                NewObject(3, "CellsEat01", 2, 0),
                NewObject(4, "ORock01", 0, 1));

            string result = LevelBoardRenderer.Render(level, "tiny.json");

            Assert.Equal(
                $"Emoji board: tiny.json (3×2, 4 objects){Environment.NewLine}"
                + $"⬛🐔🟫{Environment.NewLine}"
                + $"🪨⬜⬜{Environment.NewLine}"
                + $"Legend: 🐔 player | 🪨 rock | ⬛ metal | 🟫 diggable | ⬜ empty{Environment.NewLine}",
                result);
        }

        /// <summary>Verifies gameplay objects win over terrain and unclassified types are visible.</summary>
        [Fact]
        public void PrioritizesGameplayObjectsAndReportsUnknownTypes()
        {
            LevelDocument level = CreateLevel(
                2,
                1,
                NewObject(1, "CellsEat01", 0, 0),
                NewObject(2, "MGrUnknown", 1, 0),
                NewObject(3, "OPlayer", 0, 0));

            string result = LevelBoardRenderer.Render(level, "stacked.json");

            Assert.Equal(
                $"Emoji board: stacked.json (2×1, 3 objects){Environment.NewLine}"
                + $"🐔❓{Environment.NewLine}"
                + $"Legend: 🐔 player | ❓ unknown{Environment.NewLine}"
                + $"Unknown legacy types: MGrUnknown{Environment.NewLine}",
                result);
        }

        /// <summary>Verifies every directional legacy blue monster renders with the blue glyph.</summary>
        [Fact]
        public void RendersDirectionalBlueMonsters()
        {
            LevelDocument level = CreateLevel(
                4,
                1,
                NewObject(1, "MBlueL", 0, 0),
                NewObject(2, "MBlueT", 1, 0),
                NewObject(3, "MBlueR", 2, 0),
                NewObject(4, "MBlueB", 3, 0));

            string result = LevelBoardRenderer.Render(level, "blue.json");

            Assert.Equal(
                $"Emoji board: blue.json (4×1, 4 objects){Environment.NewLine}"
                + $"🔵🔵🔵🔵{Environment.NewLine}"
                + $"Legend: 🔵 blue monster{Environment.NewLine}",
                result);
        }

        private static LevelDocument CreateLevel(
            int width,
            int height,
            params LevelObject[] objects)
        {
            return new LevelDocument(
                1,
                null,
                new LevelSize(width, height),
                new EditorPreview(0, 0, width, height),
                objects);
        }

        private static LevelObject NewObject(int id, string type, int x, int y)
        {
            return new LevelObject(
                id,
                type,
                0,
                new LevelPosition(x, y),
                [],
                new Dictionary<string, System.Text.Json.JsonElement>());
        }
    }
}
