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

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>Verifies recovered Green Monster death and small-beetle spawning behavior.</summary>
    public sealed class GreenMonsterDeathTests
    {
        /// <summary>Verifies an ordinary blast kills the monster, scores it, and spawns one green beetle.</summary>
        [Fact]
        public void ExplosionDeathSpawnsOneSmallGreenBeetle()
        {
            WorldGrid world = new(5, 4);
            Entity player = AddPlayer(world, new GridPosition(0, 0));
            Entity monster = AddMonster(world, new GridPosition(2, 2));
            Entity explosive = AddExplosive(world, new GridPosition(1, 2));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Detonate(explosive.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(200));

            SmallGreenBeetleSpawned spawned = Assert.Single(
                events.OfType<SmallGreenBeetleSpawned>());
            Assert.Equal(monster.Id, spawned.EntityId);
            Assert.Equal(monster.Position, spawned.Position);
            Entity beetle = world.GetEntity(spawned.SpawnedEntityId);
            Assert.Equal(SmallBeetleColor.Green, beetle.SmallBeetleBody!.Color);
            Assert.Equal(monster.Position, beetle.Position);
            Assert.Equal(10, player.PlayerBody!.Progress.Score);
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(monster.Id));
        }

        /// <summary>Verifies a falling shake makes a normal Green Monster detonate itself.</summary>
        [Fact]
        public void FallingShakeStartsGreenMonsterExplosionAndSpawnsBeetle()
        {
            WorldGrid world = new(5, 5);
            _ = AddPlayer(world, new GridPosition(0, 0));
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
            _ = Assert.Single(events.OfType<SmallGreenBeetleSpawned>());
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(monster.Id));
        }

        /// <summary>Verifies a child inherits transit and snaps to the parent's physically nearest cell.</summary>
        [Fact]
        public void MovingMonsterSpawnsBeetleInPhysicallyNearestCell()
        {
            WorldGrid world = new(6, 5);
            _ = AddPlayer(world, new GridPosition(0, 0));
            Entity monster = AddMonster(
                world,
                new GridPosition(3, 2),
                moveSpeed: 0.5,
                movementLocked: false);
            Entity chain = AddExplosive(world, new GridPosition(3, 2));
            Entity source = AddExplosive(world, new GridPosition(2, 2));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Detonate(source.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(400));

            Assert.Contains(events, item => item is ExplosionStarted started
                && started.EntityId == chain.Id);
            SmallGreenBeetleSpawned spawned = Assert.Single(
                events.OfType<SmallGreenBeetleSpawned>());
            Assert.Equal(new GridPosition(3, 2), spawned.Position);
            Assert.Equal(spawned.Position, world.GetEntity(spawned.SpawnedEntityId).Position);
        }

        /// <summary>Verifies equivalent simulations assign the spawned child the same identity.</summary>
        [Fact]
        public void SpawnedBeetleIdentityIsDeterministic()
        {
            EntityId first = SpawnOnce();
            EntityId second = SpawnOnce();

            Assert.Equal(first, second);

            static EntityId SpawnOnce()
            {
                WorldGrid world = new(5, 4);
                _ = AddPlayer(
                    world,
                    new GridPosition(0, 0),
                    new EntityId(new Guid("00000000-0000-0000-0000-000000000001")));
                _ = AddMonster(
                    world,
                    new GridPosition(2, 2),
                    id: new EntityId(new Guid("00000000-0000-0000-0000-000000000002")));
                Entity explosive = AddExplosive(
                    world,
                    new GridPosition(1, 2),
                    new EntityId(new Guid("00000000-0000-0000-0000-000000000003")));
                SimulationWorld simulation = CreateSimulation(world);

                _ = simulation.Detonate(explosive.Id);
                SmallGreenBeetleSpawned spawned = Assert.Single(
                    simulation.Advance(TimeSpan.FromMilliseconds(200))
                        .OfType<SmallGreenBeetleSpawned>());
                return spawned.SpawnedEntityId;
            }
        }

        private static Entity AddPlayer(
            WorldGrid world,
            GridPosition position,
            EntityId? id = null)
        {
            Entity player = new(
                id ?? EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right),
                healthBody: new HealthBody(100, 100, TimeSpan.FromSeconds(1)));
            world.Add(player);
            return player;
        }

        private static Entity AddMonster(
            WorldGrid world,
            GridPosition position,
            double moveSpeed = 1.5625,
            bool movementLocked = true,
            EntityId? id = null)
        {
            Entity monster = new(
                id ?? EntityId.New(),
                position,
                InteractionMask.BlocksMovement
                    | (movementLocked ? InteractionMask.MovementLocked : InteractionMask.None),
                greenMonsterBody: new GreenMonsterBody(
                    moveSpeed,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(300)),
                explosionBody: new ExplosionBody(ExplosionReaction.Destroy, false),
                shakeBody: new ShakeBody(ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3));
            world.Add(monster);
            return monster;
        }

        private static Entity AddExplosive(
            WorldGrid world,
            GridPosition position,
            EntityId? id = null)
        {
            Entity explosive = new(
                id ?? EntityId.New(),
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
