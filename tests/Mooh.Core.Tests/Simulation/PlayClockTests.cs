using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies the recovered timed play clock: <c>GameInit</c> starts it at
    /// <c>CNT_GPrmTimedTime</c>, <c>GameHealthDec</c> and <c>GameHealthAdd</c> redirect health to it
    /// at one second per unit, and running out takes the win-shaped settle to <c>GST_Looser</c>.
    /// </summary>
    public sealed class PlayClockTests
    {
        /// <summary>Verifies only the timed profile has a clock, and it starts at 400 s.</summary>
        [Fact]
        public void OnlyTheTimedProfileStartsTheRecoveredClock()
        {
            (_, Entity timed) = Create(DifficultySpeeds.Timed);
            (_, Entity standard) = Create(DifficultySpeeds.Standard);

            Assert.Equal(TimeSpan.FromSeconds(400), timed.HealthBody!.Clock?.Remaining);
            Assert.Null(standard.HealthBody!.Clock);
        }

        /// <summary>
        /// Verifies damage spends seconds instead of health, while the hit still shocks and arms
        /// immunity, because <c>OnHelthLost</c> does both above the redirect.
        /// </summary>
        [Fact]
        public void DamageSpendsSecondsAndLeavesHealthAlone()
        {
            (SimulationWorld simulation, Entity player) = Create(DifficultySpeeds.Timed);

            IReadOnlyList<SimulationEvent> events = simulation.ApplyDamage(
                new DamageRequest(player.Id, player.Id, DamageKind.Explosion, 20));

            Assert.Equal(100, player.HealthBody!.Current);
            Assert.Equal(TimeSpan.FromSeconds(380), player.HealthBody.Clock!.Remaining);
            Assert.Contains(events, item => item == new DamageApplied(player.Id, player.Id, DamageKind.Explosion, 20, 100));
            Assert.Contains(events, item => item is PlayerShockStarted);
            Assert.True(player.HealthBody.ImmunityRemaining > TimeSpan.Zero);
        }

        /// <summary>Verifies healing adds seconds and is not clamped to the starting time.</summary>
        [Fact]
        public void HealingAddsSecondsBeyondTheStartingTime()
        {
            List<LevelObject> objects =
            [
                Object(1, "OPlayer", 1, 1),
                Object(2, "OEat02", 2, 1),
            ];
            for (int x = 0; x <= 4; x++)
            {
                objects.Add(Object(10 + x, "CellsMetal01", x, 2));
            }

            MaterializedLevel level = LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(5, 4), new EditorPreview(0, 0, 5, 4), objects),
                Direction.Down,
                DifficultySpeeds.Timed);
            Entity player = level.GetEntity(1);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));

            TimeSpan expected = TimeSpan.FromSeconds(400 - 0.3 + DifficultySpeeds.Timed.FoodHeal);
            Assert.InRange(
                player.HealthBody!.Clock!.Remaining,
                expected - TimeSpan.FromMilliseconds(1),
                expected + TimeSpan.FromMilliseconds(1));
        }

        /// <summary>
        /// Verifies running out of time loses the level after the recovered 500 ms and 1500 ms
        /// waits, the shape of the win settle rather than the 1500 ms health loss.
        /// </summary>
        [Fact]
        public void RunningOutLosesAfterTheWinShapedSettle()
        {
            (SimulationWorld simulation, Entity player) = Create(DifficultySpeeds.Timed);

            _ = simulation.Advance(TimeSpan.FromSeconds(400));
            Assert.Equal(TimeSpan.Zero, player.HealthBody!.Clock!.Remaining);
            IReadOnlyList<SimulationEvent> settling = simulation.Advance(TimeSpan.FromMilliseconds(1999));
            Assert.Empty(settling.OfType<LevelLost>());
            Assert.Equal(LevelOutcome.InProgress, simulation.Outcome);

            IReadOnlyList<SimulationEvent> lost = simulation.Advance(TimeSpan.FromMilliseconds(2));

            _ = Assert.Single(lost.OfType<LevelLost>());
            Assert.Equal(LevelOutcome.Lost, simulation.Outcome);
            Assert.Equal(100, player.HealthBody.Current);
        }

        /// <summary>Verifies a <c>bLockLvlEnd</c> state holds the timeout settle open.</summary>
        [Fact]
        public void ALevelEndHoldExtendsTheTimeoutSettle()
        {
            (SimulationWorld simulation, Entity player) = Create(DifficultySpeeds.Timed);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(401_900));
            _ = simulation.ApplyDamage(new DamageRequest(player.Id, player.Id, DamageKind.Explosion, 10));

            IReadOnlyList<SimulationEvent> held = simulation.Advance(TimeSpan.FromMilliseconds(500));
            Assert.Empty(held.OfType<LevelLost>());

            IReadOnlyList<SimulationEvent> lost = simulation.Advance(TimeSpan.FromMilliseconds(300));
            _ = Assert.Single(lost.OfType<LevelLost>());
        }

        /// <summary>Verifies a timed level is still won by clearing its lock carriers.</summary>
        [Fact]
        public void ATimedLevelIsStillWonByClearingItsCarriers()
        {
            WorldGrid world = new(5, 5);
            world.Add(NewPlayer());
            Entity carrier = new(
                EntityId.New(),
                new GridPosition(3, 3),
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked | InteractionMask.LevelEndLock,
                explosionBody: new ExplosionBody(ExplosionReaction.Destroy, false));
            world.Add(carrier);
            Entity explosive = new(
                EntityId.New(),
                new GridPosition(2, 3),
                InteractionMask.None,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            world.Add(explosive);
            SimulationWorld simulation = CreateSimulation(world, DifficultySpeeds.Timed);

            _ = simulation.Detonate(explosive.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(3));

            _ = Assert.Single(events.OfType<LevelWon>());
            Assert.Equal(LevelOutcome.Won, simulation.Outcome);
        }

        private static (SimulationWorld Simulation, Entity Player) Create(DifficultySpeeds speeds)
        {
            WorldGrid world = new(5, 5);
            Entity player = NewPlayer();
            world.Add(player);
            return (CreateSimulation(world, speeds), player);
        }

        private static Entity NewPlayer()
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(0, 0),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right),
                healthBody: new HealthBody(100, 100, TimeSpan.FromSeconds(1)));
        }

        private static SimulationWorld CreateSimulation(WorldGrid world, DifficultySpeeds speeds)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                speeds);
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
