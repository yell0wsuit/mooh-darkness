using System;
using System.Collections.Generic;

using Mooh.Core.Content;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Levels
{
    /// <summary>Verifies recovered collection effects for placed archival pickups.</summary>
    public sealed class PickupTests
    {
        /// <summary>Verifies score, treasure, healing, keys, ammunition, and bonuses update player state.</summary>
        [Theory]
        [InlineData("OCoin01", 1, 1, 5, 0, 0, 0, false, false, 50)]
        [InlineData("OCoin02", 5, 1, 5, 0, 0, 0, false, false, 50)]
        [InlineData("OCoin03", 10, 1, 5, 0, 0, 0, false, false, 50)]
        [InlineData("ODiam04", 50, 1, 5, 0, 0, 0, false, false, 50)]
        [InlineData("OEat02", 50, 0, 5, 0, 0, 0, false, false, 90)]
        [InlineData("OKDKey2", 100, 0, 5, 0, 0, 0, true, false, 50)]
        [InlineData("WBomb", 5, 0, 6, 0, 0, 0, false, false, 50)]
        [InlineData("WGrBox", 100, 0, 10, 0, 0, 0, false, false, 50)]
        [InlineData("WROne", 5, 0, 5, 1, 0, 0, false, false, 50)]
        [InlineData("WRBox", 100, 0, 5, 5, 0, 0, false, false, 50)]
        [InlineData("WMinesBox", 100, 0, 5, 0, 5, 0, false, false, 50)]
        [InlineData("WBBomb", 250, 0, 5, 0, 0, 1, false, false, 50)]
        [InlineData("BCola", 250, 0, 5, 0, 0, 0, false, false, 50)]
        [InlineData("BIce", 250, 0, 5, 0, 0, 0, false, false, 50)]
        public void AppliesRecoveredPickupEffects(
            string archivalType,
            int score,
            int treasure,
            int grenades,
            int rockets,
            int mines,
            int bigBombs,
            bool hasKeyTwo,
            bool colaActive,
            double health)
        {
            MaterializedLevel level = MaterializePickup(archivalType);
            EntityPair pair = GetPlayerAndPickup(level);
            _ = level.Simulation.ApplyDamage(new Core.Combat.DamageRequest(
                pair.Pickup.Id,
                pair.Player.Id,
                Core.Combat.DamageKind.Fire,
                50));
            _ = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(400));

            Assert.Contains(events, item => item is PlayerAte);
            PlayerProgress progress = pair.Player.PlayerBody!.Progress;
            Assert.Equal(score, progress.Score);
            Assert.Equal(treasure, progress.TreasureCollected);
            Assert.Equal(grenades, progress.Grenades);
            Assert.Equal(rockets, progress.Rockets);
            Assert.Equal(mines, progress.Mines);
            Assert.Equal(bigBombs, progress.BigBombs);
            Assert.Equal(hasKeyTwo, progress.HasKey(2));
            Assert.Equal(colaActive, progress.ColaRemaining > TimeSpan.Zero);
            Assert.Equal(archivalType == "BIce", progress.IceRemaining > TimeSpan.Zero);
            Assert.Equal(health, pair.Player.HealthBody!.Current);
            Assert.Contains(events, item => item is PickupCollected collected
                && collected.PickupEntityId == pair.Pickup.Id
                && collected.ScoreAwarded == score);
        }

        /// <summary>Verifies collecting the last treasure awards the recovered 5000-point bonus once.</summary>
        [Fact]
        public void LastTreasureAwardsCompletionBonus()
        {
            MaterializedLevel level = MaterializePickup("ODiam01", includeExtraTreasure: false);
            EntityPair pair = GetPlayerAndPickup(level);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(400));

            Assert.Contains(events, item => item is PlayerAte);
            Assert.Equal(5050, pair.Player.PlayerBody!.Progress.Score);
            PickupCollected collected = Assert.Single(events, item => item is PickupCollected)
                as PickupCollected ?? throw new InvalidOperationException();
            Assert.Equal(5050, collected.ScoreAwarded);
            Assert.True(pair.Player.PlayerBody.Progress.HasEarnedTreasureBonus);
        }

        /// <summary>Verifies active cola rejects damage and bonus timers advance with simulation time.</summary>
        [Fact]
        public void ColaProtectsUntilItsRecoveredTimerExpires()
        {
            MaterializedLevel level = MaterializePickup("BCola");
            EntityPair pair = GetPlayerAndPickup(level);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            // The protection starts when the drink that follows the next cell arrival ends.
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(2000));
            level.Simulation.SetPlayerInput(default);

            IReadOnlyList<SimulationEvent> protectedEvents = level.Simulation.ApplyDamage(
                new Core.Combat.DamageRequest(
                    pair.Pickup.Id,
                    pair.Player.Id,
                    Core.Combat.DamageKind.Fire,
                    20));
            _ = level.Simulation.Advance(TimeSpan.FromSeconds(45));
            IReadOnlyList<SimulationEvent> appliedEvents = level.Simulation.ApplyDamage(
                new Core.Combat.DamageRequest(
                    pair.Pickup.Id,
                    pair.Player.Id,
                    Core.Combat.DamageKind.Fire,
                    20));

            Assert.Contains(protectedEvents, item => item is DamageIgnored);
            Assert.Contains(appliedEvents, item => item is DamageApplied);
        }

        private static MaterializedLevel MaterializePickup(
            string archivalType,
            bool includeExtraTreasure = true)
        {
            List<LevelObject> objects =
            [
                Object(1, "OPlayer", 1, 1),
                Object(2, archivalType, 2, 1),
                Object(3, "CellsSolid01", 2, 2),
            ];
            if (includeExtraTreasure
                && ArchivalObjectCatalog.Get(archivalType).Kind is EntityKind.Coin or EntityKind.Diamond)
            {
                objects.Add(Object(4, "OCoin01", 4, 1));
            }

            LevelDocument level = new(
                1,
                null,
                new LevelSize(5, 4),
                new EditorPreview(0, 0, 5, 4),
                objects);
            return LevelMaterializer.Materialize(level, Direction.Down, Undrained.Standard);
        }

        private static EntityPair GetPlayerAndPickup(MaterializedLevel level)
        {
            return new EntityPair(level.GetEntity(1), level.GetEntity(2));
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

        private sealed record EntityPair(
            Entities.Entity Player,
            Entities.Entity Pickup);
    }
}
