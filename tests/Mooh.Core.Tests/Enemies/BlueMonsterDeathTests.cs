using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>Verifies recovered Blue Monster destruction reactions and kill scoring.</summary>
    public sealed class BlueMonsterDeathTests
    {
        /// <summary>Verifies a reached blast destroys the monster and awards its recovered kill score.</summary>
        [Fact]
        public void ExplosionDeathAwardsRecoveredKillScore()
        {
            WorldGrid world = new(5, 4);
            Entity player = AddPlayer(world, new GridPosition(0, 0));
            Entity monster = AddMonster(world, new GridPosition(2, 2));
            Entity explosive = AddExplosive(world, new GridPosition(1, 2));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Detonate(explosive.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(200));

            Assert.Contains(events, item => item is EntityDestroyed destroyed
                && destroyed.EntityId == monster.Id);
            Assert.Equal(10, player.PlayerBody!.Progress.Score);
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(monster.Id));
        }

        /// <summary>Verifies a falling shake makes a Blue Monster detonate its own blast.</summary>
        [Fact]
        public void FallingShakeStartsBlueMonsterExplosion()
        {
            WorldGrid world = new(5, 5);
            Entity player = AddPlayer(world, new GridPosition(0, 0));
            Entity monster = AddMonster(world, new GridPosition(2, 3));
            Entity falling = new(
                EntityId.New(),
                new GridPosition(2, 1),
                InteractionMask.None,
                new GravityBody(2, canSlide: false));
            world.Add(falling);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Contains(events, item => item == new ExplosionStarted(monster.Id, monster.Position));
            Assert.Equal(10, player.PlayerBody!.Progress.Score);
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(monster.Id));
        }

        private static Entity AddPlayer(WorldGrid world, GridPosition position)
        {
            Entity player = new(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right),
                healthBody: new HealthBody(100, 100, TimeSpan.FromMilliseconds(650)));
            world.Add(player);
            return player;
        }

        private static Entity AddMonster(WorldGrid world, GridPosition position)
        {
            Entity monster = new(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked,
                blueMonsterBody: new BlueMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(300)),
                explosionBody: new ExplosionBody(ExplosionReaction.Destroy, false));
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

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                DifficultySpeeds.Standard);
        }
    }
}
