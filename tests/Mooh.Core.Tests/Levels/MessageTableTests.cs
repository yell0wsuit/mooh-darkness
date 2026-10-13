using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Levels
{
    /// <summary>
    /// Verifies the recovered message table: <c>_OnCell_Wlk</c> dispatches <c>PlHere</c> to the
    /// player's own cell on every walking arrival, and <c>_PlHere_Table</c> asks the help panel
    /// for the table's <c>_TableTxt</c>.
    /// </summary>
    public sealed class MessageTableTests
    {
        /// <summary>Verifies the archival help-text id is materialized onto the table.</summary>
        [Fact]
        public void MaterializationReadsTheHelpTextId()
        {
            MaterializedLevel level = Materialize("HlpTutAllObjsFire");

            Entity table = level.GetEntity(2);

            Assert.Equal("HlpTutAllObjsFire", table.MessageTableBody?.HelpTextId);
            Assert.False(table.MessageTableBody!.HasBeenRead);
        }

        /// <summary>Verifies walking onto a table requests its text once, and again on a return.</summary>
        [Fact]
        public void EachWalkingArrivalRequestsTheText()
        {
            MaterializedLevel level = Materialize("HlpTutAllObjsFire");
            Entity table = level.GetEntity(2);
            level.Simulation.SetPlayerInput(Right());

            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(490));
            level.Simulation.SetPlayerInput(default);
            IReadOnlyList<SimulationEvent> onto = level.Simulation.Advance(TimeSpan.FromMilliseconds(30));
            IReadOnlyList<SimulationEvent> standing = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(
                [new HelpTextRequested(table.Id, "HlpTutAllObjsFire")],
                onto.OfType<HelpTextRequested>());
            Assert.True(table.MessageTableBody!.HasBeenRead);
            Assert.DoesNotContain(standing, item => item is HelpTextRequested);

            level.Simulation.SetPlayerInput(Right());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(390));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(100));
            level.Simulation.SetPlayerInput(Left());
            IReadOnlyList<SimulationEvent> back = level.Simulation.Advance(TimeSpan.FromMilliseconds(800));

            _ = Assert.Single(back.OfType<HelpTextRequested>());
        }

        /// <summary>Verifies a table without the archival property loads and stays silent.</summary>
        [Fact]
        public void TableWithoutTextLoadsAndEmitsNothing()
        {
            MaterializedLevel level = Materialize(helpTextId: null);
            Entity table = level.GetEntity(2);
            level.Simulation.SetPlayerInput(Right());

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(520));

            Assert.Null(table.MessageTableBody?.HelpTextId);
            Assert.Contains(events, item => item is CellEntered entered && entered.EntityId == level.GetEntity(1).Id);
            Assert.DoesNotContain(events, item => item is HelpTextRequested);
        }

        /// <summary>
        /// Verifies a pending cola drink replaces the arrival handler, so the table on that cell is
        /// not read: <c>_OnCell</c> applies <c>StOnCell</c> instead of running <c>_OnCell_Wlk</c>.
        /// </summary>
        [Fact]
        public void ColaDrinkArrivalSkipsTheTable()
        {
            MaterializedLevel level = Materialize("HlpTutAllObjsFire", colaWithTable: true);
            level.Simulation.SetPlayerInput(Right());

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(520));

            Assert.Contains(events, item => item is PlayerColaDrinkStarted);
            Assert.DoesNotContain(events, item => item is HelpTextRequested);
        }

        private static PlayerInputState Right()
        {
            return new PlayerInputState(Left: false, Up: false, Right: true, Down: false);
        }

        private static PlayerInputState Left()
        {
            return new PlayerInputState(Left: true, Up: false, Right: false, Down: false);
        }

        private static MaterializedLevel Materialize(string? helpTextId, bool colaWithTable = false)
        {
            Dictionary<string, JsonElement> properties = [];
            if (helpTextId is not null)
            {
                properties["_TableTxt"] = JsonSerializer.SerializeToElement(helpTextId);
            }

            List<LevelObject> objects =
            [
                Object(1, "OPlayer", 1, 1),
                new LevelObject(2, "OTable", 0, new LevelPosition(2, 1), [], properties),
            ];
            if (colaWithTable)
            {
                objects.Add(Object(3, "BCola", 2, 1));
            }

            for (int x = 0; x <= 5; x++)
            {
                objects.Add(Object(100 + x, "CellsMetal01", x, 2));
            }

            return LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(6, 4), new EditorPreview(0, 0, 6, 4), objects));
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], new Dictionary<string, JsonElement>());
        }
    }
}
