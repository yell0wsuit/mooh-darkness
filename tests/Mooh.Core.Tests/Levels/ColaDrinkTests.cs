using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Levels
{
    /// <summary>
    /// Verifies the recovered cola sequence: <c>BonCola</c> records <c>ST_BonCola</c> through
    /// <c>OnCellStateSet</c>, the next cell arrival plays the four drink states, and only
    /// <c>_AEnd_BonCola</c> turns the protection on.
    /// </summary>
    public sealed class ColaDrinkTests
    {
        /// <summary>The recovered quarter turn plus one standard cell of walking.</summary>
        private const double ArrivalMilliseconds = 505;

        /// <summary>The recovered drink: 6 + 4 + 5 + 8 frames at 50 ms.</summary>
        private const double DrinkMilliseconds = 1150;

        /// <summary>Verifies collecting cola scores and starts the drink only on the next arrival.</summary>
        [Fact]
        public void CollectionDefersTheDrinkToTheNextCellArrival()
        {
            MaterializedLevel level = Materialize(colaCells: [2]);
            Entity player = level.GetEntity(1);
            level.Simulation.SetPlayerInput(Right());

            IReadOnlyList<SimulationEvent> collection = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));

            Assert.Contains(collection, item => item is PickupCollected { ScoreAwarded: 250 });
            Assert.DoesNotContain(collection, item => item is PlayerColaDrinkStarted);
            Assert.Equal(TimeSpan.Zero, player.PlayerBody!.Progress.ColaRemaining);

            IReadOnlyList<SimulationEvent> arrival = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));

            Assert.Contains(arrival, item => item == new PlayerColaDrinkStarted(
                player.Id,
                TimeSpan.FromMilliseconds(DrinkMilliseconds)));
            Assert.Equal(PlayerActionKind.ColaDrink, player.PlayerBody.Action?.Kind);
        }

        /// <summary>
        /// Verifies the drink holds the player for exactly 1150 ms, then starts the 45 s window and
        /// leaves the player facing down, which is <c>_AEnd_BonCola</c>'s <c>ST_StBottom</c>.
        /// </summary>
        [Fact]
        public void DrinkHoldsThePlayerThenStartsTheProtection()
        {
            MaterializedLevel level = Materialize(colaCells: [2]);
            Entity player = level.GetEntity(1);
            level.Simulation.SetPlayerInput(Right());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(600));
            level.Simulation.SetPlayerInput(default);

            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(ArrivalMilliseconds + DrinkMilliseconds - 600 - 1));
            Assert.Equal(PlayerActionKind.ColaDrink, player.PlayerBody!.Action?.Kind);
            Assert.Equal(TimeSpan.Zero, player.PlayerBody.Progress.ColaRemaining);

            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(2));

            Assert.Null(player.PlayerBody.Action);
            Assert.Equal(Direction.Down, player.PlayerBody.FacingDirection);
            Assert.InRange(
                player.PlayerBody.Progress.ColaRemaining,
                TimeSpan.FromMilliseconds(44_998),
                TimeSpan.FromSeconds(45));
            IReadOnlyList<SimulationEvent> absorbed = level.Simulation.ApplyDamage(
                new DamageRequest(player.Id, player.Id, DamageKind.Fire, 20));
            Assert.Contains(absorbed, item => item is DamageIgnored);
        }

        /// <summary>Verifies the player still holds a direction during the drink without moving.</summary>
        [Fact]
        public void HeldInputDoesNotMoveThePlayerDuringTheDrink()
        {
            MaterializedLevel level = Materialize(colaCells: [2]);
            Entity player = level.GetEntity(1);
            level.Simulation.SetPlayerInput(Right());

            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(ArrivalMilliseconds + DrinkMilliseconds - 10));

            Assert.Equal(new GridPosition(2, 1), player.Position);
            Assert.Null(player.Motion);
        }

        /// <summary>
        /// Verifies a hit during the drink lands normally and abandons it, because
        /// <c>OnHelthLost</c> enters <c>ST_ShockIn</c> and <c>_AEnd_BonCola</c> never runs.
        /// </summary>
        [Fact]
        public void DamageDuringTheDrinkAbandonsTheBonus()
        {
            MaterializedLevel level = Materialize(colaCells: [2]);
            Entity player = level.GetEntity(1);
            level.Simulation.SetPlayerInput(Right());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(600));
            level.Simulation.SetPlayerInput(default);

            IReadOnlyList<SimulationEvent> hit = level.Simulation.ApplyDamage(
                new DamageRequest(player.Id, player.Id, DamageKind.Fire, 20));
            _ = level.Simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.Contains(hit, item => item is DamageApplied);
            Assert.Contains(hit, item => item is PlayerShockStarted);
            Assert.Equal(TimeSpan.Zero, player.PlayerBody!.Progress.ColaRemaining);
        }

        /// <summary>Verifies a burn on the arrival that would start the drink abandons it instead.</summary>
        [Fact]
        public void BurnOnTheArrivalAbandonsTheDrink()
        {
            MaterializedLevel level = Materialize(colaCells: [2], fireCell: 2);
            Entity player = level.GetEntity(1);
            level.Simulation.SetPlayerInput(Right());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(600));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.Contains(events, item => item is DamageApplied { Kind: DamageKind.Fire });
            Assert.DoesNotContain(events, item => item is PlayerColaDrinkStarted);
            Assert.False(player.PlayerBody!.HasPendingColaDrink);
            Assert.Equal(TimeSpan.Zero, player.PlayerBody.Progress.ColaRemaining);
        }

        /// <summary>Verifies BonOn adds the second cola's 45 seconds to the remainder after its drink ends.</summary>
        [Fact]
        public void SecondColaExtendsTheWindowAfterItsOwnDrink()
        {
            MaterializedLevel level = Materialize(colaCells: [2, 3]);
            Entity player = level.GetEntity(1);
            level.Simulation.SetPlayerInput(Right());

            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(3000));
            Assert.InRange(
                player.PlayerBody!.Progress.ColaRemaining,
                TimeSpan.FromMilliseconds(43_600),
                TimeSpan.FromMilliseconds(43_700));
            Assert.Equal(PlayerActionKind.ColaDrink, player.PlayerBody.Action?.Kind);

            TimeSpan remaining = player.PlayerBody.Progress.ColaRemaining;
            TimeSpan elapsed = TimeSpan.FromMilliseconds(320);
            _ = level.Simulation.Advance(elapsed);

            TimeSpan expected = remaining + TimeSpan.FromSeconds(45) - elapsed;
            Assert.InRange(
                player.PlayerBody.Progress.ColaRemaining,
                expected - TimeSpan.FromMilliseconds(1),
                expected + TimeSpan.FromMilliseconds(1));
        }

        private static PlayerInputState Right()
        {
            return new PlayerInputState(Left: false, Up: false, Right: true, Down: false);
        }

        private static MaterializedLevel Materialize(int[] colaCells, int? fireCell = null)
        {
            List<LevelObject> objects = [Object(1, "OPlayer", 1, 1)];
            if (fireCell is int fireX)
            {
                objects.Add(Object(50, "OFire", fireX, 1));
            }

            for (int index = 0; index < colaCells.Length; index++)
            {
                objects.Add(Object(10 + index, "BCola", colaCells[index], 1));
            }

            for (int x = 0; x <= 7; x++)
            {
                objects.Add(Object(100 + x, "CellsMetal01", x, 2));
            }

            return LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(8, 4), new EditorPreview(0, 0, 8, 4), objects));
        }

        private static LevelObject Object(int id, string type, int x, int y)
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
