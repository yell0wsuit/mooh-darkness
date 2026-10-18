using System;
using System.Collections.Generic;

using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Shakes;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>Verifies recovered Blue Monster strikes against eligible non-player objects.</summary>
    public sealed class BlueMonsterObjectAttackTests
    {
        /// <summary>Verifies a strikeable object ahead starts an attack rather than a turn.</summary>
        [Fact]
        public void StrikeableObjectAheadStartsAttack()
        {
            WorldGrid world = new(3, 1);
            Entity monster = NewMonster(0, 0, Direction.Right);
            Entity bottle = NewTarget(1, 0, ShakeReaction.Detonate3x3);
            world.Add(monster);
            world.Add(bottle);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(BlueMonsterState.AttackWindup, monster.BlueMonsterBody!.State);
            Assert.Equal(
                new EnemyAttackStarted(
                    monster.Id,
                    new GridPosition(1, 0),
                    TimeSpan.FromMilliseconds(600)),
                Assert.Single(events));
        }

        /// <summary>Verifies the strike runs the target's recovered ordinary shake handler.</summary>
        [Fact]
        public void StrikeDetonatesExplosiveTarget()
        {
            WorldGrid world = new(3, 1);
            Entity monster = NewMonster(0, 0, Direction.Right);
            Entity bottle = NewTarget(1, 0, ShakeReaction.Detonate3x3);
            world.Add(monster);
            world.Add(bottle);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(600));

            Assert.Contains(events, item => item == new EnemyStrike(monster.Id, new GridPosition(1, 0)));
            Assert.Contains(events, item => item == new ExplosionStarted(bottle.Id, new GridPosition(1, 0)));
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(bottle.Id));
        }

        /// <summary>Verifies a strikeable object on the left turns the monster toward it.</summary>
        [Fact]
        public void StrikeableObjectOnLeftStartsLeftTurn()
        {
            WorldGrid world = new(3, 3);
            Entity monster = NewMonster(1, 1, Direction.Right);
            Entity bottle = NewTarget(1, 0, ShakeReaction.Detonate3x3);
            world.Add(monster);
            world.Add(bottle);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(BlueMonsterState.TurningLeft, monster.BlueMonsterBody!.State);
            Assert.Equal(
                new EnemyTurnStarted(
                    monster.Id,
                    Direction.Right,
                    Direction.Up,
                    TimeSpan.FromMilliseconds(300)),
                Assert.Single(events));
        }

        /// <summary>Verifies an ineligible occupant blocks the walk without being attacked.</summary>
        [Fact]
        public void IneligibleOccupantAheadIsNotAttacked()
        {
            WorldGrid world = new(3, 1);
            Entity monster = NewMonster(0, 0, Direction.Right);
            Entity coin = new(
                EntityId.New(),
                new GridPosition(1, 0),
                InteractionMask.PlayerEdible | InteractionMask.RoundSupport,
                shakeBody: new ShakeBody(ShakeReaction.None, ShakeReaction.Destroy));
            world.Add(monster);
            world.Add(coin);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(BlueMonsterState.TurningRight, monster.BlueMonsterBody!.State);
            _ = Assert.IsType<EnemyTurnStarted>(Assert.Single(events));
        }

        private static Entity NewMonster(int x, int y, Direction facingDirection)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(x, y),
                InteractionMask.BlocksMovement,
                blueMonsterBody: new BlueMonsterBody(
                    1.5625,
                    facingDirection,
                    TimeSpan.FromMilliseconds(300)));
        }

        private static Entity NewTarget(int x, int y, ShakeReaction reaction)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(x, y),
                InteractionMask.BlocksMovement | InteractionMask.BlueMonsterTarget,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false),
                shakeBody: new ShakeBody(reaction, reaction));
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
