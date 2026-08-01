using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>Verifies recovered blue-monster left-hand navigation.</summary>
    public sealed class BlueMonsterMovementTests
    {
        /// <summary>Verifies blue-monster configuration rejects an invalid movement speed.</summary>
        [Fact]
        public void BodyRejectsInvalidMoveSpeed()
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new BlueMonsterBody(0, Direction.Right, TimeSpan.FromMilliseconds(300)));
        }

        /// <summary>Verifies an open left cell takes priority over an open forward cell.</summary>
        [Fact]
        public void OpenLeftCellStartsLeftTurn()
        {
            WorldGrid world = new(3, 3);
            Entity monster = NewMonster(1, 1, Direction.Right);
            world.Add(monster);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(299));

            Assert.Equal(BlueMonsterState.TurningLeft, monster.BlueMonsterBody!.State);
            Assert.Equal(Direction.Right, monster.BlueMonsterBody.FacingDirection);
            Assert.Equal(TimeSpan.FromMilliseconds(1), monster.BlueMonsterBody.Remaining);
            Assert.Equal(
                new EnemyTurnStarted(
                    monster.Id,
                    Direction.Right,
                    Direction.Up,
                    TimeSpan.FromMilliseconds(300)),
                Assert.Single(events));
        }

        /// <summary>Verifies a blocked left cell makes the monster walk into a clear forward cell.</summary>
        [Fact]
        public void BlockedLeftCellWalksForward()
        {
            WorldGrid world = new(3, 3);
            Entity monster = NewMonster(1, 1, Direction.Right);
            Entity blocker = NewBlocker(1, 0);
            world.Add(monster);
            world.Add(blocker);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(100));

            Assert.Equal(BlueMonsterState.Walking, monster.BlueMonsterBody!.State);
            Assert.Equal(new GridPosition(2, 1), monster.Position);
            Assert.Equal(0.15625, monster.Motion!.Progress, precision: 12);
            Assert.Equal(
                new EnemyMoveStarted(monster.Id, new GridPosition(1, 1), new GridPosition(2, 1)),
                Assert.Single(events));
        }

        /// <summary>Verifies counter-motion in the entered cell slows monster walking.</summary>
        [Fact]
        public void CounterMotionSlowsWalking()
        {
            WorldGrid world = new(3, 1);
            Entity monster = NewMonster(0, 0, Direction.Right);
            Entity counterMover = new(
                EntityId.New(),
                new GridPosition(1, 0),
                InteractionMask.None);
            world.Add(monster);
            world.Add(counterMover);
            KinematicsSystem.Start(
                counterMover,
                MotionCause.Falling,
                Direction.Left,
                speed: 1);
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.NotNull(monster.Motion);
            Assert.Equal(1.171875, monster.Motion.Speed, precision: 12);
        }

        /// <summary>Verifies blocked left and forward cells make the monster turn right.</summary>
        [Fact]
        public void BlockedLeftAndForwardCellsStartRightTurn()
        {
            WorldGrid world = new(3, 3);
            Entity monster = NewMonster(1, 1, Direction.Right);
            world.Add(monster);
            world.Add(NewBlocker(1, 0));
            world.Add(NewBlocker(2, 1));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(BlueMonsterState.TurningRight, monster.BlueMonsterBody!.State);
            Assert.Equal(
                new EnemyTurnStarted(
                    monster.Id,
                    Direction.Right,
                    Direction.Down,
                    TimeSpan.FromMilliseconds(300)),
                Assert.Single(events));
        }

        /// <summary>Verifies a player on the left causes a left turn toward that player.</summary>
        [Fact]
        public void AttackablePlayerOnLeftStartsLeftTurn()
        {
            WorldGrid world = new(3, 3);
            Entity monster = NewMonster(1, 1, Direction.Right);
            Entity player = NewPlayer(1, 0);
            world.Add(monster);
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(BlueMonsterState.TurningLeft, monster.BlueMonsterBody!.State);
        }

        /// <summary>Verifies a completed left turn checks forward before considering another left turn.</summary>
        [Fact]
        public void CompletedLeftTurnWalksForwardInsteadOfTurningLeftAgain()
        {
            WorldGrid world = new(3, 3);
            Entity monster = NewMonster(1, 1, Direction.Right);
            world.Add(monster);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(300));

            Assert.Equal(Direction.Up, monster.BlueMonsterBody!.FacingDirection);
            Assert.Equal(BlueMonsterState.Walking, monster.BlueMonsterBody.State);
            Assert.Equal(new GridPosition(1, 0), monster.Position);
            Assert.Equal(
                [
                    new EnemyTurnStarted(
                        monster.Id,
                        Direction.Right,
                        Direction.Up,
                        TimeSpan.FromMilliseconds(300)),
                    new EnemyTurnCompleted(monster.Id, Direction.Up),
                    new EnemyMoveStarted(
                        monster.Id,
                        new GridPosition(1, 1),
                        new GridPosition(1, 0)),
                ],
                events);
        }

        /// <summary>Verifies mixed monster decisions follow world registration order.</summary>
        [Fact]
        public void MixedMonsterDecisionsFollowRegistrationOrder()
        {
            WorldGrid world = new(5, 3);
            Entity blue = NewMonster(1, 1, Direction.Right);
            Entity green = new(
                EntityId.New(),
                new GridPosition(3, 1),
                InteractionMask.BlocksMovement,
                greenMonsterBody: new GreenMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(250)));
            world.Add(blue);
            world.Add(green);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(
                [
                    new EnemyTurnStarted(
                        blue.Id,
                        Direction.Right,
                        Direction.Up,
                        TimeSpan.FromMilliseconds(300)),
                    new EnemyMoveStarted(
                        green.Id,
                        new GridPosition(3, 1),
                        new GridPosition(4, 1)),
                ],
                events);
        }

        /// <summary>Verifies simultaneous mixed turn completions defer decisions until registration-order dispatch.</summary>
        [Fact]
        public void SimultaneousMixedTurnCompletionsPreserveDecisionOrder()
        {
            WorldGrid world = new(5, 3);
            Entity green = new(
                EntityId.New(),
                new GridPosition(0, 1),
                InteractionMask.BlocksMovement,
                greenMonsterBody: new GreenMonsterBody(
                    1.5625,
                    Direction.Left,
                    TimeSpan.FromMilliseconds(300)));
            Entity blue = NewMonster(3, 1, Direction.Right);
            world.Add(green);
            world.Add(blue);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(300));

            Assert.Equal(
                [
                    new EnemyTurnStarted(
                        green.Id,
                        Direction.Left,
                        Direction.Up,
                        TimeSpan.FromMilliseconds(300)),
                    new EnemyTurnStarted(
                        blue.Id,
                        Direction.Right,
                        Direction.Up,
                        TimeSpan.FromMilliseconds(300)),
                    new EnemyTurnCompleted(green.Id, Direction.Up),
                    new EnemyTurnCompleted(blue.Id, Direction.Up),
                    new EnemyMoveStarted(
                        green.Id,
                        new GridPosition(0, 1),
                        new GridPosition(0, 0)),
                    new EnemyMoveStarted(
                        blue.Id,
                        new GridPosition(3, 1),
                        new GridPosition(3, 0)),
                ],
                events);
        }

        /// <summary>Verifies one entity cannot carry both monster behavior components.</summary>
        [Fact]
        public void EntityCannotHaveBothMonsterBodies()
        {
            WorldGrid world = new(2, 1);
            Entity ambiguous = new(
                EntityId.New(),
                new GridPosition(0, 0),
                InteractionMask.BlocksMovement,
                greenMonsterBody: new GreenMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(250)),
                blueMonsterBody: new BlueMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(300)));
            world.Add(ambiguous);
            SimulationWorld simulation = CreateSimulation(world);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => simulation.Advance(TimeSpan.FromMilliseconds(1)));

            Assert.Equal(
                "An entity cannot have both green- and blue-monster bodies.",
                exception.Message);
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0));
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

        private static Entity NewBlocker(int x, int y)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(x, y),
                InteractionMask.BlocksMovement);
        }

        private static Entity NewPlayer(int x, int y)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(x, y),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Down),
                healthBody: new HealthBody(100, 100, TimeSpan.FromMilliseconds(500)));
        }
    }
}
