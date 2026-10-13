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
    /// Verifies the recovered mine: <c>Weap_Mine</c> lays it in the faced cell after the 250 ms
    /// <c>ST_Mine*</c>, and the deployed <c>WMine</c> — laid or placed — is a shadow that runs a 3x3
    /// blast as soon as any <c>CNT_CMaskCellLockMotion</c> carrier shares its cell, the collision
    /// <c>sub_4A2F30</c> checks every object cycle.
    /// </summary>
    public sealed class MineTests
    {
        /// <summary>Verifies laying a mine takes 250 ms and spends one.</summary>
        [Fact]
        public void LayingAMinePutsItInTheFacedCell()
        {
            MaterializedLevel level = Corridor([Object(2, "WMinesBox", 3, 0)]);
            Entity player = level.GetEntity(1);
            CollectMines(level);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> start = level.Simulation.Advance(TimeSpan.FromMilliseconds(10));
            level.Simulation.SetPlayerInput(default);
            IReadOnlyList<SimulationEvent> laid = level.Simulation.Advance(TimeSpan.FromMilliseconds(245));

            Assert.Contains(start, item => item == new PlayerWeaponUseStarted(
                player.Id,
                WeaponKind.Mine,
                Direction.Right,
                TimeSpan.FromMilliseconds(250)));
            MineLaid mine = Assert.Single(laid.OfType<MineLaid>());
            Assert.Equal(new GridPosition(4, 0), mine.Position);
            Assert.Equal("WMine", level.World.GetEntity(mine.MineEntityId).ArchivalIdentity?.ArchivalType);
            Assert.Equal(4, player.PlayerBody!.Progress.AmmoOf(WeaponKind.Mine));
        }

        /// <summary>Verifies a monster walking onto a placed mine sets it off.</summary>
        [Fact]
        public void AMonsterWalkingOntoAMineSetsItOff()
        {
            MaterializedLevel level = Corridor(
            [
                Object(2, "WMine", 10, 0),
                Object(3, "MGrL", 12, 0),
            ]);
            Entity mine = level.GetEntity(2);
            Entity monster = level.GetEntity(3);

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(2));

            Assert.Contains(events, item => item == new ExplosionStarted(mine.Id, new GridPosition(10, 0)));
            Assert.Contains(events, item => item is EntityDestroyed destroyed && destroyed.EntityId == monster.Id);
        }

        /// <summary>Verifies the player walking onto a mine sets it off too.</summary>
        [Fact]
        public void ThePlayerWalkingOntoAMineSetsItOff()
        {
            MaterializedLevel level = Corridor([Object(2, "WMine", 3, 0)]);
            Entity mine = level.GetEntity(2);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(200));

            Assert.Contains(events, item => item == new ExplosionStarted(mine.Id, new GridPosition(3, 0)));
        }

        /// <summary>Walks right onto the mine box beside the player and selects the mine.</summary>
        private static void CollectMines(MaterializedLevel level)
        {
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(400));
            _ = level.Simulation.SelectWeapon(WeaponKind.Mine);
            Assert.Equal(new GridPosition(3, 0), level.GetEntity(1).Position);
            Assert.Equal(WeaponKind.Mine, level.GetEntity(1).PlayerBody!.Progress.CurrentWeapon);
        }

        private static PlayerInputState Fire()
        {
            return new PlayerInputState(false, false, false, false, Fire: true);
        }

        /// <summary>Builds a one-row corridor with the player at x = 2.</summary>
        private static MaterializedLevel Corridor(List<LevelObject> objects)
        {
            List<LevelObject> all = [Object(1, "OPlayer", 2, 0), .. objects];
            return LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(16, 1), new EditorPreview(0, 0, 16, 1), all),
                Direction.Down,
                Undrained.Standard);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], new Dictionary<string, JsonElement>());
        }
    }
}
