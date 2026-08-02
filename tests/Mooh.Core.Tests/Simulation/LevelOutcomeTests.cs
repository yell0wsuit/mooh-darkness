using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Shakes;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies the recovered level outcome rules from <c>Game.py</c> <c>__GStatePlay</c>: a level is
    /// won when every level-end lock carrier is gone, and lost when player health reaches zero.
    /// </summary>
    public sealed class LevelOutcomeTests
    {
        /// <summary>Verifies destroying the only lock carrier wins after the recovered 500 ms and 1500 ms settle.</summary>
        [Fact]
        public void DestroyingTheLastLockCarrierWinsAfterTheRecoveredSettle()
        {
            WorldGrid world = new(7, 5);
            Entity player = AddPlayer(world, new GridPosition(0, 0));
            Entity monster = AddBlueMonster(world, new GridPosition(4, 2));
            Entity explosive = AddExplosive(world, new GridPosition(3, 2));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Detonate(explosive.Id);
            IReadOnlyList<SimulationEvent> beforeSettle = simulation.Advance(
                TimeSpan.FromMilliseconds(2199));

            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(monster.Id));
            Assert.Empty(beforeSettle.OfType<LevelWon>());
            Assert.Equal(LevelOutcome.InProgress, simulation.Outcome);

            IReadOnlyList<SimulationEvent> afterSettle = simulation.Advance(
                TimeSpan.FromMilliseconds(1));

            LevelWon won = Assert.Single(afterSettle.OfType<LevelWon>());
            Assert.Equal(player.Id, won.EntityId);
            Assert.Equal(LevelOutcome.Won, simulation.Outcome);
        }

        /// <summary>Verifies a level that never held a lock carrier can never be won.</summary>
        [Fact]
        public void LevelWithoutLockCarriersIsNeverWon()
        {
            WorldGrid world = new(5, 5);
            _ = AddPlayer(world, new GridPosition(0, 0));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(10));

            Assert.Empty(events.OfType<LevelWon>());
            Assert.Equal(LevelOutcome.InProgress, simulation.Outcome);
        }

        /// <summary>Verifies the beetle spawned by a Green Monster death is itself a lock carrier.</summary>
        [Fact]
        public void BeetleSpawnedByTheLastGreenMonsterBlocksTheWin()
        {
            WorldGrid world = new(7, 5);
            _ = AddPlayer(world, new GridPosition(0, 0));
            Entity monster = AddGreenMonster(world, new GridPosition(4, 2));
            Entity explosive = AddExplosive(world, new GridPosition(3, 2));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Detonate(explosive.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(5));

            SmallGreenBeetleSpawned spawned = Assert.Single(events.OfType<SmallGreenBeetleSpawned>());
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(monster.Id));
            Assert.True(world.GetEntity(spawned.SpawnedEntityId)
                .Interactions.HasFlag(InteractionMask.LevelEndLock));
            Assert.Empty(events.OfType<LevelWon>());
            Assert.Equal(LevelOutcome.InProgress, simulation.Outcome);
        }

        /// <summary>Verifies eating the last surviving beetle completes the level.</summary>
        [Fact]
        public void EatingTheLastSpawnedBeetleWinsTheLevel()
        {
            WorldGrid world = new(7, 5);
            _ = AddPlayer(world, new GridPosition(1, 2));
            Entity monster = AddGreenMonster(world, new GridPosition(2, 2));
            Entity explosive = AddExplosive(world, new GridPosition(3, 2));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Detonate(explosive.Id);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(200));
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(monster.Id));

            simulation.SetPlayerInput(
                new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(5));

            _ = Assert.Single(events.OfType<PlayerAte>());
            _ = Assert.Single(events.OfType<LevelWon>());
            Assert.Equal(LevelOutcome.Won, simulation.Outcome);
        }

        /// <summary>Verifies fatal damage loses the level after the recovered 1500 ms settle.</summary>
        [Fact]
        public void FatalDamageLosesTheLevelAfterTheRecoveredSettle()
        {
            WorldGrid world = new(5, 5);
            Entity player = AddPlayer(world, new GridPosition(0, 0));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.ApplyDamage(new DamageRequest(
                player.Id,
                player.Id,
                DamageKind.Explosion,
                100));
            IReadOnlyList<SimulationEvent> beforeSettle = simulation.Advance(
                TimeSpan.FromMilliseconds(1499));

            Assert.Empty(beforeSettle.OfType<LevelLost>());
            Assert.Equal(LevelOutcome.InProgress, simulation.Outcome);

            IReadOnlyList<SimulationEvent> afterSettle = simulation.Advance(
                TimeSpan.FromMilliseconds(1));

            LevelLost lost = Assert.Single(afterSettle.OfType<LevelLost>());
            Assert.Equal(player.Id, lost.EntityId);
            Assert.Equal(LevelOutcome.Lost, simulation.Outcome);
        }

        /// <summary>
        /// Verifies the recovered evaluation order: the loss branch precedes the win branch in
        /// <c>__GStatePlay</c>, so a death observed in the same tick as the final kill loses.
        /// </summary>
        [Fact]
        public void FatalDamageOutranksAWinObservedInTheSameTick()
        {
            WorldGrid world = new(5, 5);
            Entity player = AddPlayer(world, new GridPosition(0, 0));
            Entity monster = AddBlueMonster(world, new GridPosition(3, 3));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.ApplyDamage(new DamageRequest(
                player.Id,
                player.Id,
                DamageKind.Explosion,
                100));
            Assert.True(world.Remove(monster.Id));
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(5));

            _ = Assert.Single(events.OfType<LevelLost>());
            Assert.Empty(events.OfType<LevelWon>());
            Assert.Equal(LevelOutcome.Lost, simulation.Outcome);
        }

        /// <summary>
        /// Verifies the recovered <c>bLockLvlEnd</c> hold: the win waits past its 1500 ms phase
        /// while the player is still shocked.
        /// </summary>
        [Fact]
        public void ShockedPlayerHoldsTheWinOpenPastTheSettle()
        {
            WorldGrid world = new(5, 5);
            Entity player = AddPlayer(world, new GridPosition(0, 0), shockDuration: TimeSpan.FromSeconds(3));
            Entity monster = AddBlueMonster(world, new GridPosition(3, 3));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.ApplyDamage(new DamageRequest(
                player.Id,
                player.Id,
                DamageKind.Explosion,
                10));
            Assert.True(world.Remove(monster.Id));
            IReadOnlyList<SimulationEvent> beforeShockEnds = simulation.Advance(
                TimeSpan.FromMilliseconds(2999));

            Assert.Empty(beforeShockEnds.OfType<LevelWon>());
            Assert.Equal(LevelOutcome.InProgress, simulation.Outcome);

            IReadOnlyList<SimulationEvent> afterShockEnds = simulation.Advance(
                TimeSpan.FromMilliseconds(1));

            _ = Assert.Single(afterShockEnds.OfType<LevelWon>());
            Assert.Equal(LevelOutcome.Won, simulation.Outcome);
        }

        /// <summary>
        /// Verifies the recovered commitment rule: once the win condition is observed the original
        /// never rechecks it, so a lock carrier appearing during the settle does not cancel the win.
        /// </summary>
        [Fact]
        public void LockCarrierAppearingDuringTheSettleDoesNotCancelTheWin()
        {
            WorldGrid world = new(5, 5);
            _ = AddPlayer(world, new GridPosition(0, 0));
            Entity monster = AddBlueMonster(world, new GridPosition(3, 3));
            SimulationWorld simulation = CreateSimulation(world);

            Assert.True(world.Remove(monster.Id));
            _ = simulation.Advance(TimeSpan.FromMilliseconds(500));
            _ = AddBlueMonster(world, new GridPosition(2, 2));
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(2));

            _ = Assert.Single(events.OfType<LevelWon>());
            Assert.Equal(LevelOutcome.Won, simulation.Outcome);
        }

        /// <summary>Verifies the timed profile ignores the health loss condition, which the clock replaces.</summary>
        [Fact]
        public void TimedProfileIgnoresTheHealthLossCondition()
        {
            WorldGrid world = new(5, 5);
            Entity player = AddPlayer(world, new GridPosition(0, 0));
            SimulationWorld simulation = CreateSimulation(world, DifficultySpeeds.Timed);

            _ = simulation.ApplyDamage(new DamageRequest(
                player.Id,
                player.Id,
                DamageKind.Explosion,
                100));
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(5));

            Assert.Empty(events.OfType<LevelLost>());
            Assert.Equal(LevelOutcome.InProgress, simulation.Outcome);
        }

        /// <summary>Verifies a world without a player never reaches an outcome.</summary>
        [Fact]
        public void WorldWithoutAPlayerNeverReachesAnOutcome()
        {
            WorldGrid world = new(5, 5);
            Entity monster = AddBlueMonster(world, new GridPosition(3, 3));
            SimulationWorld simulation = CreateSimulation(world);

            Assert.True(world.Remove(monster.Id));
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(5));

            Assert.Empty(events.OfType<LevelWon>());
            Assert.Equal(LevelOutcome.InProgress, simulation.Outcome);
        }

        private static Entity AddPlayer(
            WorldGrid world,
            GridPosition position,
            TimeSpan? shockDuration = null)
        {
            Entity player = new(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right, shockDuration),
                healthBody: new HealthBody(100, 100, TimeSpan.FromSeconds(1)));
            world.Add(player);
            return player;
        }

        private static Entity AddGreenMonster(WorldGrid world, GridPosition position)
        {
            Entity monster = new(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement
                    | InteractionMask.MovementLocked
                    | InteractionMask.LevelEndLock,
                greenMonsterBody: new GreenMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(250)),
                explosionBody: new ExplosionBody(ExplosionReaction.Destroy, false),
                shakeBody: new ShakeBody(ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3));
            world.Add(monster);
            return monster;
        }

        private static Entity AddBlueMonster(WorldGrid world, GridPosition position)
        {
            Entity monster = new(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement
                    | InteractionMask.MovementLocked
                    | InteractionMask.LevelEndLock,
                blueMonsterBody: new BlueMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(300)),
                explosionBody: new ExplosionBody(ExplosionReaction.Destroy, false),
                shakeBody: new ShakeBody(ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3));
            world.Add(monster);
            return monster;
        }

        private static Entity AddExplosive(WorldGrid world, GridPosition position)
        {
            Entity explosive = new(
                EntityId.New(),
                position,
                InteractionMask.None,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            world.Add(explosive);
            return explosive;
        }

        private static SimulationWorld CreateSimulation(
            WorldGrid world,
            DifficultySpeeds? difficultySpeeds = null)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                difficultySpeeds ?? DifficultySpeeds.Standard);
        }
    }
}
