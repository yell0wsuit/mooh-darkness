using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies equivalent falling and sliding behavior in every gravity direction.
    /// </summary>
    public sealed class DirectionalGravityTests
    {
        /// <summary>Gets rotated scenarios in which a one-cell fall ends on support.</summary>
        public static TheoryData<Direction, GridPosition, GridPosition, GridPosition> FallingScenarios =>
            new()
            {
                { Direction.Left, new GridPosition(3, 2), new GridPosition(1, 2), new GridPosition(2, 2) },
                { Direction.Up, new GridPosition(2, 3), new GridPosition(2, 1), new GridPosition(2, 2) },
                { Direction.Right, new GridPosition(1, 2), new GridPosition(3, 2), new GridPosition(2, 2) },
                { Direction.Down, new GridPosition(2, 1), new GridPosition(2, 3), new GridPosition(2, 2) },
            };

        /// <summary>Gets rotated scenarios using the gravity-relative left slide route.</summary>
        public static TheoryData<Direction, GridPosition, GridPosition, Direction, GridPosition> SlidingScenarios =>
            new()
            {
                {
                    Direction.Left,
                    new GridPosition(3, 2),
                    new GridPosition(2, 2),
                    Direction.Down,
                    new GridPosition(3, 3)
                },
                {
                    Direction.Up,
                    new GridPosition(2, 3),
                    new GridPosition(2, 2),
                    Direction.Left,
                    new GridPosition(1, 3)
                },
                {
                    Direction.Right,
                    new GridPosition(1, 2),
                    new GridPosition(2, 2),
                    Direction.Up,
                    new GridPosition(1, 1)
                },
                {
                    Direction.Down,
                    new GridPosition(2, 1),
                    new GridPosition(2, 2),
                    Direction.Right,
                    new GridPosition(3, 1)
                },
            };

        /// <summary>Verifies rotated cell entry and landing traces.</summary>
        /// <param name="gravityDirection">The direction of gravity.</param>
        /// <param name="start">The body's starting cell.</param>
        /// <param name="support">The solid support cell.</param>
        /// <param name="expectedEntry">The cell entered before landing.</param>
        [Theory]
        [MemberData(nameof(FallingScenarios))]
        public void FallingAndLandingAreDirectionIndependent(
            Direction gravityDirection,
            GridPosition start,
            GridPosition support,
            GridPosition expectedEntry)
        {
            WorldGrid world = new(5, 5);
            Entity body = AddBody(world, start, canSlide: false);
            _ = AddSolid(world, support);

            IReadOnlyList<SimulationEvent> events =
                CreateSimulation(world, gravityDirection).Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(expectedEntry, body.Position);
            Assert.Null(body.Motion);
            Assert.Equal(
                [
                    new FallStarted(body.Id, gravityDirection),
                    new CellEntered(body.Id, expectedEntry),
                    new Landed(body.Id, gravityDirection),
                ],
                events);
        }

        /// <summary>Verifies rotated round sliding and transition into falling.</summary>
        /// <param name="gravityDirection">The direction of gravity.</param>
        /// <param name="start">The body's starting cell.</param>
        /// <param name="support">The direct support cell.</param>
        /// <param name="expectedSlide">The gravity-relative left slide direction.</param>
        /// <param name="expectedEntry">The cell entered by the slide.</param>
        [Theory]
        [MemberData(nameof(SlidingScenarios))]
        public void RoundSlidingIsDirectionIndependent(
            Direction gravityDirection,
            GridPosition start,
            GridPosition support,
            Direction expectedSlide,
            GridPosition expectedEntry)
        {
            WorldGrid world = new(5, 5);
            Entity body = AddBody(world, start, canSlide: true);
            _ = AddSolid(world, support);

            IReadOnlyList<SimulationEvent> events =
                CreateSimulation(world, gravityDirection).Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(expectedEntry, body.Position);
            Assert.NotNull(body.Motion);
            Assert.Equal(MotionCause.Falling, body.Motion.Cause);
            Assert.Equal(gravityDirection, body.Motion.Direction);
            Assert.Equal(
                [
                    new SlideStarted(body.Id, expectedSlide),
                    new CellEntered(body.Id, expectedEntry),
                    new FallStarted(body.Id, gravityDirection),
                ],
                events);
        }

        /// <summary>Verifies changing gravity while all entities are stationary.</summary>
        [Fact]
        public void StationaryWorldCanChangeGravityDirection()
        {
            WorldGrid world = new(5, 5);
            Entity body = AddBody(world, new GridPosition(2, 1), canSlide: false);
            _ = AddSolid(world, new GridPosition(2, 2));
            SimulationWorld simulation = CreateSimulation(world, Direction.Down);

            simulation.SetGravity(Direction.Right);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.NotNull(body.Motion);
            Assert.Equal(Direction.Right, body.Motion.Direction);
            Assert.Equal([new FallStarted(body.Id, Direction.Right)], events);
        }

        /// <summary>Verifies changing gravity cannot interrupt active gravity-driven motion.</summary>
        [Fact]
        public void MovingWorldRejectsGravityDirectionChangeWithoutMutation()
        {
            WorldGrid world = new(3, 3);
            Entity body = AddBody(world, new GridPosition(1, 1), canSlide: false);
            SimulationWorld simulation = CreateSimulation(world, Direction.Down);
            _ = simulation.Advance(TimeSpan.FromSeconds(0.25));
            MotionState originalMotion = Assert.IsType<MotionState>(body.Motion);
            double originalProgress = originalMotion.Progress;

            _ = Assert.Throws<InvalidOperationException>(() => simulation.SetGravity(Direction.Right));

            Assert.Same(originalMotion, body.Motion);
            Assert.Equal(Direction.Down, body.Motion.Direction);
            Assert.Equal(originalProgress, body.Motion.Progress);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.75));
            Assert.Contains(new Landed(body.Id, Direction.Down), events);
        }

        private static SimulationWorld CreateSimulation(WorldGrid world, Direction gravityDirection)
        {
            return new SimulationWorld(
                world,
                gravityDirection,
                new DefaultGravityCollisionPolicy(),
                new Random(1));
        }

        private static Entity AddBody(WorldGrid world, GridPosition position, bool canSlide)
        {
            Entity entity = new(
                EntityId.New(),
                position,
                InteractionMask.None,
                new GravityBody(1, canSlide));
            world.Add(entity);
            return entity;
        }

        private static Entity AddSolid(WorldGrid world, GridPosition position)
        {
            Entity entity = new(EntityId.New(), position, InteractionMask.BlocksMovement);
            world.Add(entity);
            return entity;
        }
    }
}
