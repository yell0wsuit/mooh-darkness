using System;
using System.Collections.Generic;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies the recovered world-list shadow filter. <c>GameBase.WorldListType</c> is built with
    /// <c>CNT_OrMaskShadow</c>, and every cell query that passes a zero <c>ulMaskAny</c> —
    /// <c>CellObjCheck</c>, <c>CellObjDump</c>, <c>CellObjCount</c>, <c>CellObjStill</c> — skips
    /// occupants carrying it. A message table therefore reads as an empty cell to the push gates,
    /// while a query with an explicit positive mask still sees it.
    /// </summary>
    public sealed class ShadowOccupantTests
    {
        /// <summary>
        /// Verifies a walking player pushes a rock into a message table's cell. The recovered gate
        /// is <c>CellObjCheck(n, 0, 0, True)</c> in <c>GamePlayer.CtrlMove</c>.
        /// </summary>
        [Fact]
        public void AWalkingPushCrossesAMessageTableCell()
        {
            MaterializedLevel level = Materialize(
                Floor(0, 8, 2),
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "ORock01", 2, 1),
                    Object(102, "OTable", 3, 1),
                    Object(103, "CellsSolid01", 5, 1),
                ]);
            Entity rock = level.GetEntity(101);
            Entity table = level.GetEntity(102);

            level.Simulation.SetPlayerInput(HeldRight());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(1500));

            // The rock crossed the table's cell and stopped against the wall instead.
            Assert.Equal(new GridPosition(4, 1), rock.Position);
            Assert.Contains(level.World.Entities, entity => entity.Id == table.Id);
            Assert.Equal(new GridPosition(3, 1), table.Position);
        }

        /// <summary>
        /// Verifies the strike-push chain crosses a message table cell instead of stopping against
        /// it. The recovered gate is <c>CellObjCheck(c, 0, 0, True)</c> in
        /// <c>CPlPushBase.PlPush_OnCell</c>.
        /// </summary>
        [Fact]
        public void AStrikePushChainCrossesAMessageTableCell()
        {
            MaterializedLevel level = Materialize(
                Floor(0, 8, 2),
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "ORock01", 3, 1),
                    Object(102, "OTable", 5, 1),
                    Object(103, "CellsSolid01", 7, 1),
                ]);
            Entity rock = level.GetEntity(101);
            Entity table = level.GetEntity(102);
            WalkRightTo(level, 2);

            level.Simulation.SetPlayerInput(Fire());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(1500));

            Assert.Equal(new GridPosition(6, 1), rock.Position);
            Assert.Null(rock.Motion);
            Assert.Contains(level.World.Entities, entity => entity.Id == table.Id);
        }

        /// <summary>
        /// Verifies a struck object that stops on a message table's cell leaves it alone. The
        /// recovered chain kills only <c>CNT_CMaskCallCanPushTh</c> carriers, which it selects with
        /// an explicit positive mask, so the shadow occupant is neither crushed nor shaken.
        /// </summary>
        [Fact]
        public void AStrikePushDoesNotDestroyAMessageTableItPassesOver()
        {
            MaterializedLevel level = Materialize(
                Floor(0, 8, 2),
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "ORock01", 3, 1),
                    Object(102, "OTable", 6, 1),
                    Object(103, "CellsSolid01", 7, 1),
                ]);
            Entity table = level.GetEntity(102);
            WalkRightTo(level, 2);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(1500));

            Assert.Contains(level.World.Entities, entity => entity.Id == table.Id);
            Assert.DoesNotContain(
                events,
                value => value is EntityDestroyed destroyed && destroyed.EntityId == table.Id);
        }

        private static void WalkRightTo(MaterializedLevel level, int x)
        {
            // The recovered quarter turn takes 105 ms and the standard walking speed crosses one
            // cell in 400 ms.
            double milliseconds = 105 + (400 * x);
            level.Simulation.SetPlayerInput(HeldRight());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(milliseconds - 20));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(60));

            Assert.Equal(new GridPosition(x, 1), level.World.Player!.Position);
            Assert.Equal(Direction.Right, level.World.Player.PlayerBody!.FacingDirection);
        }

        private static PlayerInputState HeldRight()
        {
            return new PlayerInputState(
                Left: false,
                Up: false,
                Right: true,
                Down: false);
        }

        private static PlayerInputState Fire()
        {
            return new PlayerInputState(
                Left: false,
                Up: false,
                Right: false,
                Down: false,
                Fire: true);
        }

        private static List<LevelObject> Floor(int fromX, int toX, int y)
        {
            List<LevelObject> result = [];
            for (int x = fromX; x <= toX; x++)
            {
                result.Add(Object(x, "CellsMetal01", x, y));
            }

            return result;
        }

        private static MaterializedLevel Materialize(
            List<LevelObject> floor,
            IEnumerable<LevelObject> objects)
        {
            floor.AddRange(objects);
            return LevelMaterializer.Materialize(
                new LevelDocument(
                    1,
                    null,
                    new LevelSize(10, 6),
                    new EditorPreview(0, 0, 10, 6),
                    floor));
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            Dictionary<string, System.Text.Json.JsonElement> properties = [];
            return new LevelObject(
                id,
                type,
                0,
                new LevelPosition(x, y),
                [],
                properties);
        }
    }
}
