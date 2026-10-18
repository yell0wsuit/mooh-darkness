using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

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
    /// Verifies the recovered rocket: <c>Weap_Rocket</c> launches only left or right after the
    /// 560 ms <c>ST_Rock*</c>, and the rocket flies at twice the normal speed until the cell ahead
    /// holds a still, visible occupant, where it runs its own 3x3 blast and shakes that cell. It
    /// never bores through terrain, and a collision with a mine or a locking occupant sets it off.
    /// </summary>
    public sealed class RocketTests
    {
        /// <summary>Verifies a launch takes 560 ms, spends a rocket and puts it in the faced cell.</summary>
        [Fact]
        public void ALaunchPutsARocketInTheFacedCell()
        {
            MaterializedLevel level = Corridor([]);
            Entity player = level.GetEntity(1);
            CollectRockets(level);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> start = level.Simulation.Advance(TimeSpan.FromMilliseconds(10));
            level.Simulation.SetPlayerInput(default);
            IReadOnlyList<SimulationEvent> fired = level.Simulation.Advance(TimeSpan.FromMilliseconds(555));

            Assert.Contains(start, item => item == new PlayerWeaponUseStarted(
                player.Id,
                WeaponKind.Rocket,
                Direction.Right,
                TimeSpan.FromMilliseconds(560)));
            RocketFired rocket = Assert.Single(fired.OfType<RocketFired>());
            Assert.Equal(new GridPosition(4, 0), rocket.Position);
            Assert.Equal(Direction.Right, rocket.Direction);
            Assert.Equal(4, player.PlayerBody!.Progress.AmmoOf(WeaponKind.Rocket));
        }

        /// <summary>Verifies there is no launch up or down, because only two launch states exist.</summary>
        [Fact]
        public void ThereIsNoLaunchUpOrDown()
        {
            MaterializedLevel level = Corridor([], height: 3, playerY: 1);
            Entity player = level.GetEntity(1);
            CollectRockets(level, y: 1);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, true, false, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(50));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));
            Assert.Equal(Direction.Up, player.PlayerBody!.FacingDirection);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(800));

            Assert.DoesNotContain(events, item => item is PlayerWeaponUseStarted or RocketFired);
            Assert.Equal(5, player.PlayerBody.Progress.AmmoOf(WeaponKind.Rocket));
        }

        /// <summary>Verifies a rocket stops in front of a wall, blasts its own cell and shakes the wall's.</summary>
        [Fact]
        public void ARocketBlastsInFrontOfAWall()
        {
            MaterializedLevel level = Corridor([Object(3, "CellsMetal01", 9, 0)]);
            CollectRockets(level);
            RocketFired rocket = Launch(level);

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.Contains(events, item => item == new ExplosionStarted(rocket.RocketEntityId, new GridPosition(8, 0)));
        }

        /// <summary>Verifies a rocket does not bore through earth the way a flying arrow does.</summary>
        [Fact]
        public void ARocketDoesNotBoreThroughEarth()
        {
            MaterializedLevel level = Corridor([Object(3, "CellsEat01", 9, 0)]);
            CollectRockets(level);
            RocketFired rocket = Launch(level);

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.Contains(events, item => item == new ExplosionStarted(rocket.RocketEntityId, new GridPosition(8, 0)));
        }

        /// <summary>
        /// Verifies a rocket flying into a mine's cell collides with it: the rocket's collision mask
        /// includes <c>CNT_CMaskShadowWeap</c>, it blasts, and the mine's <c>OnShake</c> blasts too.
        /// </summary>
        [Fact]
        public void ARocketCollidingWithAMineSetsBothOff()
        {
            MaterializedLevel level = Corridor([Object(3, "WMine", 8, 0)]);
            Entity mine = level.GetEntity(3);
            CollectRockets(level);
            RocketFired rocket = Launch(level);

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.Contains(events, item => item == new ExplosionStarted(rocket.RocketEntityId, new GridPosition(8, 0)));
            Assert.Contains(events, item => item is ExplosionStarted started && started.EntityId == mine.Id);
        }

        private static RocketFired Launch(MaterializedLevel level)
        {
            level.Simulation.SetPlayerInput(Fire());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(10));
            level.Simulation.SetPlayerInput(default);
            return Assert.Single(level.Simulation.Advance(TimeSpan.FromMilliseconds(555)).OfType<RocketFired>());
        }

        /// <summary>Walks right onto the rocket box beside the player and selects the rocket.</summary>
        private static void CollectRockets(MaterializedLevel level, int y = 0)
        {
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(400));
            _ = level.Simulation.SelectWeapon(WeaponKind.Rocket);
            Assert.Equal(new GridPosition(3, y), level.GetEntity(1).Position);
            Assert.Equal(WeaponKind.Rocket, level.GetEntity(1).PlayerBody!.Progress.CurrentWeapon);
        }

        private static PlayerInputState Fire()
        {
            return new PlayerInputState(false, false, false, false, Fire: true);
        }

        /// <summary>
        /// Builds a corridor with the player at x = 2 and a rocket box at x = 3. A taller corridor
        /// keeps a metal floor beneath the player's row.
        /// </summary>
        private static MaterializedLevel Corridor(List<LevelObject> objects, int height = 1, int playerY = 0)
        {
            List<LevelObject> all =
            [
                Object(1, "OPlayer", 2, playerY),
                Object(2, "WRBox", 3, playerY),
                .. objects,
            ];
            if (playerY + 1 < height)
            {
                for (int x = 0; x < 16; x++)
                {
                    all.Add(Object(100 + x, "CellsMetal01", x, playerY + 1));
                }
            }

            return LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(16, height), new EditorPreview(0, 0, 16, height), all),
                Direction.Down,
                Undrained.Standard);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], new Dictionary<string, JsonElement>());
        }
    }
}
