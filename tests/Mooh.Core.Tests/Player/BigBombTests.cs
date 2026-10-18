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
    /// Verifies the recovered big bomb: <c>Weap_BBomb</c> places it in the player's own cell after
    /// the 750 ms <c>ST_BBombPlace</c>, it ticks for 7675 ms, and <c>BBombWork</c> floods outward
    /// with energy 15, paying 14 for a cell that held an explodable and 1 for an empty one, one
    /// ring every 200 ms, never entering a cell that holds a non-explodable occupant.
    /// </summary>
    public sealed class BigBombTests
    {
        /// <summary>Verifies placing takes 750 ms facing down and spends the bomb.</summary>
        [Fact]
        public void PlacingPutsTheBombInThePlayersOwnCell()
        {
            MaterializedLevel level = Corridor();
            Entity player = level.GetEntity(1);
            CollectAndSelect(level);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> start = level.Simulation.Advance(TimeSpan.FromMilliseconds(10));
            level.Simulation.SetPlayerInput(default);

            Assert.Contains(start, item => item == new PlayerWeaponUseStarted(
                player.Id,
                WeaponKind.BigBomb,
                Direction.Down,
                TimeSpan.FromMilliseconds(750)));
            Assert.Equal(Direction.Down, player.PlayerBody!.FacingDirection);

            IReadOnlyList<SimulationEvent> placed = level.Simulation.Advance(TimeSpan.FromMilliseconds(745));

            BigBombPlaced bomb = Assert.Single(placed.OfType<BigBombPlaced>());
            Assert.Equal(player.Position, bomb.Position);
            Assert.Equal(0, player.PlayerBody.Progress.AmmoOf(WeaponKind.BigBomb));
            Assert.Equal(WeaponKind.Grenade, player.PlayerBody.Progress.CurrentWeapon);
        }

        /// <summary>
        /// Verifies the fuse, then the flood: it spreads one cell every 200 ms, stops at the player
        /// who costs 14, never enters the metal wall, and blasts its own cell a second time.
        /// </summary>
        [Fact]
        public void TheBombFloodsOutwardWhenItsFuseEnds()
        {
            MaterializedLevel level = Corridor(wallX: 9);
            Entity player = level.GetEntity(1);
            CollectAndSelect(level);
            level.Simulation.SetPlayerInput(Fire());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(10));
            level.Simulation.SetPlayerInput(default);
            BigBombPlaced placed = Assert.Single(
                level.Simulation.Advance(TimeSpan.FromMilliseconds(745)).OfType<BigBombPlaced>());
            WalkLeft(level, cells: 4);
            Assert.Equal(new GridPosition(2, 0), player.Position);

            double sincePlacement = 755 - 750 + WalkLeftMilliseconds(4);
            IReadOnlyList<SimulationEvent> waiting = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(7675 - sincePlacement - 5));
            Assert.DoesNotContain(waiting, item => item is ExplosionStarted);

            List<SimulationEvent> blast = [.. level.Simulation.Advance(TimeSpan.FromSeconds(4))];

            Assert.Contains(blast, item => item == new ExplosionStarted(placed.BombEntityId, new GridPosition(6, 0)));
            int[] reachedX = [.. blast.OfType<ExplosionCellReached>().Select(reached => reached.Position.X)];
            Assert.Equal(2, reachedX.Count(x => x == 6));
            Assert.Contains(2, reachedX);
            Assert.DoesNotContain(1, reachedX);
            Assert.Contains(8, reachedX);
            Assert.DoesNotContain(9, reachedX);
            Assert.DoesNotContain(10, reachedX);
            Assert.Contains(blast, item => item is DamageApplied damage && damage.EntityId == player.Id);
        }

        /// <summary>Verifies a blast reaching the bomb sets its flood off at once.</summary>
        [Fact]
        public void ABlastSetsTheBombOffAtOnce()
        {
            MaterializedLevel level = Corridor();
            CollectAndSelect(level);
            level.Simulation.SetPlayerInput(Fire());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(10));
            level.Simulation.SetPlayerInput(default);
            BigBombPlaced placed = Assert.Single(
                level.Simulation.Advance(TimeSpan.FromMilliseconds(745)).OfType<BigBombPlaced>());
            WalkLeft(level, cells: 4);
            Entity grenade = new(
                EntityId.New(),
                new GridPosition(7, 0),
                Interactions.InteractionMask.None,
                explosionBody: new Explosions.ExplosionBody(Explosions.ExplosionReaction.Detonate3x3, false));
            level.World.Add(grenade);

            _ = level.Simulation.Detonate(grenade.Id);
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(250));

            Assert.Contains(events, item => item == new ExplosionStarted(placed.BombEntityId, new GridPosition(6, 0)));
        }

        private static double WalkLeftMilliseconds(int cells)
        {
            return 105 + (400 * cells) + 200;
        }

        /// <summary>
        /// Turns left from facing down, a 105 ms quarter turn, and walks whole cells, releasing just
        /// before the last arrival.
        /// </summary>
        private static void WalkLeft(MaterializedLevel level, int cells)
        {
            level.Simulation.SetPlayerInput(new PlayerInputState(true, false, false, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(105 + (400 * cells) - 20));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(220));
        }

        /// <summary>Walks right onto the big-bomb pickup and selects the big bomb.</summary>
        private static void CollectAndSelect(MaterializedLevel level)
        {
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(400));
            _ = level.Simulation.SelectWeapon(WeaponKind.BigBomb);
            Assert.Equal(WeaponKind.BigBomb, level.GetEntity(1).PlayerBody!.Progress.CurrentWeapon);
            Assert.Equal(new GridPosition(6, 0), level.GetEntity(1).Position);
        }

        private static PlayerInputState Fire()
        {
            return new PlayerInputState(false, false, false, false, Fire: true);
        }

        /// <summary>
        /// Builds a one-row corridor with the player at x = 5 and a big-bomb pickup at x = 6, with an
        /// optional metal wall.
        /// </summary>
        private static MaterializedLevel Corridor(int? wallX = null)
        {
            List<LevelObject> objects =
            [
                Object(1, "OPlayer", 5, 0),
                Object(2, "WBBomb", 6, 0),
            ];
            if (wallX is int x)
            {
                objects.Add(Object(3, "CellsMetal01", x, 0));
            }

            return LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(20, 1), new EditorPreview(0, 0, 20, 1), objects),
                Direction.Down,
                Undrained.Standard);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], new Dictionary<string, JsonElement>());
        }
    }
}
