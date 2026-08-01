using System;
using System.Collections.Generic;

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
    /// <summary>Verifies autonomous green-monster walking and clockwise turning.</summary>
    public sealed class GreenMonsterMovementTests
    {
        /// <summary>Verifies monster configuration rejects invalid values.</summary>
        /// <param name="moveSpeed">The candidate walking speed.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void BodyRejectsInvalidMoveSpeed(double moveSpeed)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new GreenMonsterBody(moveSpeed, Direction.Right, TimeSpan.FromMilliseconds(250)));
        }

        /// <summary>Verifies turn duration must be positive.</summary>
        [Fact]
        public void BodyRejectsInvalidTurnDuration()
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new GreenMonsterBody(2, Direction.Right, TimeSpan.Zero));
        }

        /// <summary>Verifies facing must be a defined cardinal direction.</summary>
        [Fact]
        public void BodyRejectsUndefinedFacingDirection()
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new GreenMonsterBody(2, (Direction)int.MaxValue, TimeSpan.FromMilliseconds(250)));
        }

        /// <summary>Verifies a monster reserves and walks into a clear cell without pushing.</summary>
        [Fact]
        public void ClearCellStartsWalking()
        {
            WorldGrid world = new(3, 1);
            Entity monster = NewMonster(0, 0, Direction.Right, moveSpeed: 2);
            world.Add(monster);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(250));

            Assert.Equal(new GridPosition(1, 0), monster.Position);
            Assert.Equal(GreenMonsterState.Walking, monster.GreenMonsterBody!.State);
            Assert.Equal(MotionCause.MonsterWalking, monster.Motion!.Cause);
            Assert.Equal(0.5, monster.Motion.Progress, precision: 10);
            Assert.Equal(
                new EnemyMoveStarted(monster.Id, new GridPosition(0, 0), new GridPosition(1, 0)),
                Assert.Single(events));
        }

        /// <summary>Verifies a monster turns clockwise when its forward cell is blocked.</summary>
        [Fact]
        public void BlockedCellStartsClockwiseTurn()
        {
            WorldGrid world = new(3, 3);
            Entity monster = NewMonster(1, 1, Direction.Right);
            Entity blocker = new(
                EntityId.New(),
                new GridPosition(2, 1),
                InteractionMask.BlocksMovement);
            world.Add(monster);
            world.Add(blocker);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(249));

            Assert.Equal(new GridPosition(1, 1), monster.Position);
            Assert.Null(monster.Motion);
            Assert.Equal(GreenMonsterState.Turning, monster.GreenMonsterBody!.State);
            Assert.Equal(Direction.Right, monster.GreenMonsterBody.FacingDirection);
            Assert.Equal(TimeSpan.FromMilliseconds(1), monster.GreenMonsterBody.Remaining);
            Assert.Equal(
                new EnemyTurnStarted(
                    monster.Id,
                    Direction.Right,
                    Direction.Down,
                    TimeSpan.FromMilliseconds(250)),
                Assert.Single(events));
        }

        /// <summary>Verifies monsters treat player-pushable occupants as blockers instead of pushing them.</summary>
        [Fact]
        public void PushableOccupantStartsTurnWithoutMovingOccupant()
        {
            WorldGrid world = new(3, 1);
            Entity monster = NewMonster(0, 0, Direction.Right);
            Entity pushable = new(
                EntityId.New(),
                new GridPosition(1, 0),
                InteractionMask.PlayerPushable);
            world.Add(monster);
            world.Add(pushable);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(new GridPosition(0, 0), monster.Position);
            Assert.Equal(new GridPosition(1, 0), pushable.Position);
            Assert.Null(pushable.Motion);
            Assert.Equal(GreenMonsterState.Turning, monster.GreenMonsterBody!.State);
            _ = Assert.IsType<EnemyTurnStarted>(Assert.Single(events));
        }

        /// <summary>Verifies turn completion updates facing and immediately resumes decision-making.</summary>
        [Fact]
        public void CompletedTurnWalksInNewDirection()
        {
            WorldGrid world = new(3, 3);
            Entity monster = NewMonster(2, 1, Direction.Right);
            world.Add(monster);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(250));

            Assert.Equal(Direction.Down, monster.GreenMonsterBody!.FacingDirection);
            Assert.Equal(GreenMonsterState.Walking, monster.GreenMonsterBody.State);
            Assert.Equal(new GridPosition(2, 2), monster.Position);
            Assert.Equal(
                [
                    new EnemyTurnStarted(
                        monster.Id,
                        Direction.Right,
                        Direction.Down,
                        TimeSpan.FromMilliseconds(250)),
                    new EnemyTurnCompleted(monster.Id, Direction.Down),
                    new EnemyMoveStarted(
                        monster.Id,
                        new GridPosition(2, 1),
                        new GridPosition(2, 2)),
                ],
                events);
        }

        /// <summary>Verifies reaching a cell boundary triggers the next forward decision.</summary>
        [Fact]
        public void CellBoundaryStartsNextWalkingSegment()
        {
            WorldGrid world = new(3, 1);
            Entity monster = NewMonster(0, 0, Direction.Right, moveSpeed: 2);
            world.Add(monster);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(500));

            Assert.Equal(new GridPosition(2, 0), monster.Position);
            Assert.Equal(GreenMonsterState.Walking, monster.GreenMonsterBody!.State);
            Assert.Equal(0, monster.Motion!.Progress, precision: 10);
            Assert.Equal(
                [
                    new EnemyMoveStarted(
                        monster.Id,
                        new GridPosition(0, 0),
                        new GridPosition(1, 0)),
                    new CellEntered(monster.Id, new GridPosition(1, 0)),
                    new EnemyMoveStarted(
                        monster.Id,
                        new GridPosition(1, 0),
                        new GridPosition(2, 0)),
                ],
                events);
        }

        /// <summary>Verifies simultaneous boundaries resolve monster decisions before player control.</summary>
        [Fact]
        public void SimultaneousBoundariesResolveMonsterBeforePlayerContinuation()
        {
            WorldGrid world = new(6, 1);
            Entity monster = NewMonster(0, 0, Direction.Right, moveSpeed: 2);
            Entity player = new(
                EntityId.New(),
                new GridPosition(3, 0),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2, 1.5, Direction.Right));
            world.Add(monster);
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(500));

            Assert.Equal(new GridPosition(2, 0), monster.Position);
            Assert.Equal(new GridPosition(5, 0), player.Position);
            Assert.Equal(
                [
                    new EnemyMoveStarted(
                        monster.Id,
                        new GridPosition(0, 0),
                        new GridPosition(1, 0)),
                    new PlayerMoveStarted(player.Id, Direction.Right),
                    new CellEntered(monster.Id, new GridPosition(1, 0)),
                    new CellEntered(player.Id, new GridPosition(4, 0)),
                    new EnemyMoveStarted(
                        monster.Id,
                        new GridPosition(1, 0),
                        new GridPosition(2, 0)),
                    new PlayerMoveStarted(player.Id, Direction.Right),
                ],
                events);
        }

        /// <summary>Verifies simultaneous monster decisions follow world registration order.</summary>
        [Fact]
        public void DecisionsFollowRegistrationOrder()
        {
            WorldGrid world = new(1, 2);
            Entity first = NewMonster(0, 0, Direction.Left);
            Entity second = NewMonster(0, 1, Direction.Right);
            world.Add(first);
            world.Add(second);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(
                [
                    new EnemyTurnStarted(
                        first.Id,
                        Direction.Left,
                        Direction.Up,
                        TimeSpan.FromMilliseconds(250)),
                    new EnemyTurnStarted(
                        second.Id,
                        Direction.Right,
                        Direction.Down,
                        TimeSpan.FromMilliseconds(250)),
                ],
                events);
        }

        /// <summary>Verifies monster walking cannot be assigned to an entity without a monster body.</summary>
        [Fact]
        public void MonsterMotionRequiresMonsterBody()
        {
            WorldGrid world = new(2, 1);
            Entity entity = new(EntityId.New(), new GridPosition(0, 0), InteractionMask.None);
            world.Add(entity);
            KinematicsSystem.Start(entity, MotionCause.MonsterWalking, Direction.Right, 1);
            SimulationWorld simulation = CreateSimulation(world);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => simulation.Advance(TimeSpan.FromMilliseconds(1)));

            Assert.Equal("Monster walking motion requires a green-monster body.", exception.Message);
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0));
        }

        private static Entity NewMonster(
            int x,
            int y,
            Direction facingDirection,
            double moveSpeed = 1.5625)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(x, y),
                InteractionMask.BlocksMovement,
                greenMonsterBody: new GreenMonsterBody(
                    moveSpeed,
                    facingDirection,
                    TimeSpan.FromMilliseconds(250)));
        }
    }
}
