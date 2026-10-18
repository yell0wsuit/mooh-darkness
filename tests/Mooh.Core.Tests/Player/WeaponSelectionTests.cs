using System;
using System.Collections.Generic;
using System.Text.Json;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies the recovered weapon list: <c>WeapValReset</c> starts each level on the grenade,
    /// <c>WeapCurrSet</c> selects only a stocked weapon, <c>WeapNext</c> cycles to the next stocked
    /// one, <c>WeapDec</c> moves on when a weapon runs out and <c>WeapInc</c> switches to a pickup
    /// when the current weapon is empty.
    /// </summary>
    public sealed class WeaponSelectionTests
    {
        /// <summary>Verifies a level starts on the grenade with the recovered five grenades.</summary>
        [Fact]
        public void ALevelStartsOnTheGrenade()
        {
            PlayerProgress progress = Materialize().GetEntity(1).PlayerBody!.Progress;

            Assert.Equal(WeaponKind.Grenade, progress.CurrentWeapon);
            Assert.Equal(5, progress.AmmoOf(WeaponKind.Grenade));
            Assert.Equal(0, progress.AmmoOf(WeaponKind.Rocket));
            Assert.Equal(0, progress.AmmoOf(WeaponKind.Mine));
            Assert.Equal(0, progress.AmmoOf(WeaponKind.BigBomb));
        }

        /// <summary>Verifies an empty weapon cannot be selected and a stocked one can.</summary>
        [Fact]
        public void OnlyAStockedWeaponCanBeSelected()
        {
            MaterializedLevel level = Materialize(pickup: "WRBox");
            Entity player = level.GetEntity(1);

            Assert.Empty(level.Simulation.SelectWeapon(WeaponKind.Mine));
            CollectPickup(level);
            IReadOnlyList<SimulationEvent> events = level.Simulation.SelectWeapon(WeaponKind.Rocket);

            Assert.Equal([new PlayerWeaponSelected(player.Id, WeaponKind.Rocket)], events);
            Assert.Equal(WeaponKind.Rocket, player.PlayerBody!.Progress.CurrentWeapon);
            Assert.Empty(level.Simulation.SelectWeapon(WeaponKind.Rocket));
        }

        /// <summary>Verifies the next weapon skips empty ones and wraps back around.</summary>
        [Fact]
        public void NextSkipsEmptyWeaponsAndWraps()
        {
            MaterializedLevel level = Materialize(pickup: "WMinesBox");
            PlayerProgress progress = level.GetEntity(1).PlayerBody!.Progress;
            CollectPickup(level);

            _ = level.Simulation.SelectNextWeapon();
            Assert.Equal(WeaponKind.Mine, progress.CurrentWeapon);
            _ = level.Simulation.SelectNextWeapon();
            Assert.Equal(WeaponKind.Grenade, progress.CurrentWeapon);
        }

        /// <summary>Verifies picking up ammunition while the current weapon is empty selects it.</summary>
        [Fact]
        public void APickupSelectsItselfWhenTheCurrentWeaponIsEmpty()
        {
            MaterializedLevel level = Materialize(pickup: "WMinesBox");
            PlayerProgress progress = level.GetEntity(1).PlayerBody!.Progress;
            SpendAllGrenades(level);
            Assert.Equal(WeaponKind.Grenade, progress.CurrentWeapon);

            CollectPickup(level);

            Assert.Equal(WeaponKind.Mine, progress.CurrentWeapon);
        }

        /// <summary>Verifies spending the last round moves on to the next stocked weapon.</summary>
        [Fact]
        public void RunningOutMovesOnToTheNextStockedWeapon()
        {
            MaterializedLevel level = Materialize(pickup: "WMinesBox");
            PlayerProgress progress = level.GetEntity(1).PlayerBody!.Progress;
            CollectPickup(level);

            SpendAllGrenades(level);

            Assert.Equal(0, progress.AmmoOf(WeaponKind.Grenade));
            Assert.Equal(WeaponKind.Mine, progress.CurrentWeapon);
        }

        /// <summary>
        /// Throws grenades off the right edge of the ledge until none are left. Each one falls the
        /// whole height of the level and detonates far below the player.
        /// </summary>
        private static void SpendAllGrenades(MaterializedLevel level)
        {
            Entity player = level.GetEntity(1);
            PlayerProgress progress = player.PlayerBody!.Progress;
            if (player.PlayerBody.FacingDirection != Core.Grid.Direction.Right)
            {
                level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
                _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(50));
                level.Simulation.SetPlayerInput(default);
                _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));
            }

            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, false, false, Fire: true));
            for (int attempt = 0; attempt < 20 && progress.AmmoOf(WeaponKind.Grenade) > 0; attempt++)
            {
                _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(1600));
            }

            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromSeconds(3));
        }

        /// <summary>Walks left onto the pickup and back, ending on the ledge edge facing right.</summary>
        private static void CollectPickup(MaterializedLevel level)
        {
            level.Simulation.SetPlayerInput(new PlayerInputState(true, false, false, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(500));
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(500));
        }

        /// <summary>
        /// Builds a two-cell ledge high above an empty pit. The player stands on its right-hand
        /// cell and the optional pickup on its left-hand cell.
        /// </summary>
        private static MaterializedLevel Materialize(string? pickup = null)
        {
            List<LevelObject> objects =
            [
                Object(1, "OPlayer", 1, 3),
                Object(10, "CellsMetal01", 0, 4),
                Object(11, "CellsMetal01", 1, 4),
            ];
            if (pickup is not null)
            {
                objects.Add(Object(2, pickup, 0, 3));
            }

            return LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(5, 12), new EditorPreview(0, 0, 5, 12), objects),
                Core.Grid.Direction.Down,
                Undrained.Standard);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], new Dictionary<string, JsonElement>());
        }
    }
}
