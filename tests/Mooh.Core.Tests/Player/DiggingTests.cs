using System;
using System.Collections.Generic;
using System.Linq;

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
    /// Verifies the recovered digging path, in which the player consumes diggable terrain in the
    /// faced cell and then walks into it, while solid terrain only blocks.
    /// </summary>
    public sealed class DiggingTests
    {
        /// <summary>Verifies diggable terrain is consumed and the freed cell is entered.</summary>
        /// <param name="archivalType">A recovered diggable terrain type.</param>
        [Theory]
        [InlineData("CellsEat01")]
        [InlineData("DCellsEat01")]
        [InlineData("JCellsEat01")]
        public void PlayerDigsDiggableTerrainAndEntersTheCell(string archivalType)
        {
            MaterializedLevel level = Materialize(
                Object(1, "CellsMetal01", 1, 3),
                Object(2, "OPlayer", 1, 1),
                Object(3, archivalType, 1, 2));
            EntityId terrainId = level.GetEntity(3).Id;
            level.Simulation.SetPlayerInput(
                new PlayerInputState(Left: false, Up: false, Right: false, Down: true));

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.DoesNotContain(level.World.Entities, entity => entity.Id == terrainId);
            Assert.Equal(new GridPosition(1, 2), level.World.Player!.Position);
            Assert.Contains(new PlayerAte(level.World.Player.Id, terrainId), events);
            Assert.True(
                events.ToList().IndexOf(new PlayerAte(level.World.Player.Id, terrainId))
                    < events.ToList().FindIndex(simulationEvent => simulationEvent is PlayerMoveStarted),
                "The recovered order consumes the faced cell before the walk into it starts.");
        }

        /// <summary>Verifies digging is scoreless, matching the recovered scoreless terrain kill.</summary>
        [Fact]
        public void DiggingAwardsNoScore()
        {
            MaterializedLevel level = Materialize(
                Object(1, "CellsMetal01", 1, 3),
                Object(2, "OPlayer", 1, 1),
                Object(3, "CellsEat01", 1, 2));
            level.Simulation.SetPlayerInput(
                new PlayerInputState(Left: false, Up: false, Right: false, Down: true));

            _ = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(0, level.World.Player!.PlayerBody!.Progress.Score);
        }

        /// <summary>Verifies terrain without the recovered player-eat bit still blocks the player.</summary>
        /// <param name="archivalType">A recovered solid terrain type.</param>
        [Theory]
        [InlineData("CellsSolid01")]
        [InlineData("CellsSolidWd")]
        [InlineData("CellsMetal01")]
        [InlineData("CellsWdTop")]
        public void PlayerCannotDigSolidTerrain(string archivalType)
        {
            MaterializedLevel level = Materialize(
                Object(2, "OPlayer", 1, 1),
                Object(3, archivalType, 1, 2));
            EntityId terrainId = level.GetEntity(3).Id;
            level.Simulation.SetPlayerInput(
                new PlayerInputState(Left: false, Up: false, Right: false, Down: true));

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal([level.GetEntity(3)], level.World.GetOccupants(new GridPosition(1, 2)));
            Assert.Equal(new GridPosition(1, 1), level.World.Player!.Position);
            Assert.Contains(new PlayerBlocked(level.World.Player.Id, Direction.Down), events);
            Assert.DoesNotContain(
                events,
                simulationEvent => simulationEvent is PlayerAte ate && ate.EatenEntityId == terrainId);
        }

        /// <summary>
        /// Verifies a pushed weight consumes diggable terrain behind it, which the recovered
        /// eat-cell mask permits through its push-through bit.
        /// </summary>
        [Fact]
        public void PushedWeightConsumesDiggableTerrainBehindIt()
        {
            MaterializedLevel level = Materialize(
                Object(1, "CellsMetal01", 1, 3),
                Object(2, "CellsMetal01", 2, 3),
                Object(3, "CellsMetal01", 3, 3),
                Object(4, "CellsMetal01", 4, 2),
                Object(5, "OPlayer", 1, 2),
                Object(6, "OWeight", 2, 2),
                Object(7, "CellsEat01", 3, 2));
            EntityId terrainId = level.GetEntity(7).Id;
            level.Simulation.SetPlayerInput(
                new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            _ = level.Simulation.Advance(TimeSpan.FromSeconds(2));

            Assert.DoesNotContain(level.World.Entities, entity => entity.Id == terrainId);
            Assert.Equal(new GridPosition(3, 2), level.GetEntity(6).Position);
            Assert.Equal(new GridPosition(2, 2), level.World.Player!.Position);
        }

        private static MaterializedLevel Materialize(params LevelObject[] objects)
        {
            return LevelMaterializer.Materialize(
                new LevelDocument(
                    1,
                    null,
                    new LevelSize(8, 6),
                    new EditorPreview(0, 0, 8, 6),
                    objects));
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
