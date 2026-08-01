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
    /// Verifies deterministic route selection and transitions for sliding bodies.
    /// </summary>
    public sealed class RoundSlidingTests
    {
        /// <summary>Verifies selection of the sole viable right-hand route.</summary>
        [Fact]
        public void RightRouteOnlyStartsRightSlide()
        {
            SlidingScenario scenario = CreateScenario();
            _ = AddSolid(scenario.World, new GridPosition(1, 1));

            IReadOnlyList<SimulationEvent> events = AdvanceFractionally(scenario, seed: 0);

            AssertSlide(scenario.Body, Direction.Right);
            Assert.Equal([new SlideStarted(scenario.Body.Id, Direction.Right)], events);
        }

        /// <summary>Verifies selection of the sole viable left-hand route.</summary>
        [Fact]
        public void LeftRouteOnlyStartsLeftSlide()
        {
            SlidingScenario scenario = CreateScenario();
            _ = AddSolid(scenario.World, new GridPosition(3, 1));

            IReadOnlyList<SimulationEvent> events = AdvanceFractionally(scenario, seed: 0);

            AssertSlide(scenario.Body, Direction.Left);
            Assert.Equal([new SlideStarted(scenario.Body.Id, Direction.Left)], events);
        }

        /// <summary>Verifies that random zero selects the gravity-relative left turn.</summary>
        [Fact]
        public void BothRoutesWithRandomZeroChooseTurnLeft()
        {
            SlidingScenario scenario = CreateScenario();

            IReadOnlyList<SimulationEvent> events = AdvanceFractionally(scenario, seed: 1);

            AssertSlide(scenario.Body, Direction.Down.TurnLeft());
            Assert.Equal([new SlideStarted(scenario.Body.Id, Direction.Right)], events);
        }

        /// <summary>Verifies that random one selects the gravity-relative right turn.</summary>
        [Fact]
        public void BothRoutesWithRandomOneChooseTurnRight()
        {
            SlidingScenario scenario = CreateScenario();

            IReadOnlyList<SimulationEvent> events = AdvanceFractionally(scenario, seed: 0);

            AssertSlide(scenario.Body, Direction.Down.TurnRight());
            Assert.Equal([new SlideStarted(scenario.Body.Id, Direction.Left)], events);
        }

        /// <summary>Verifies that a blocked lateral cell invalidates its route.</summary>
        [Fact]
        public void BlockedLateralCellInvalidatesRoute()
        {
            SlidingScenario scenario = CreateScenario();
            _ = AddSolid(scenario.World, new GridPosition(3, 1));

            _ = AdvanceFractionally(scenario, seed: 0);

            AssertSlide(scenario.Body, Direction.Left);
        }

        /// <summary>Verifies that a blocked diagonal cell invalidates its route.</summary>
        [Fact]
        public void BlockedDiagonalCellInvalidatesRoute()
        {
            SlidingScenario scenario = CreateScenario();
            _ = AddSolid(scenario.World, new GridPosition(3, 2));

            _ = AdvanceFractionally(scenario, seed: 0);

            AssertSlide(scenario.Body, Direction.Left);
        }

        /// <summary>Verifies that non-sliding gravity bodies remain supported.</summary>
        [Fact]
        public void BodyWithoutSlidingCapabilityRemainsStationary()
        {
            SlidingScenario scenario = CreateScenario(canSlide: false);

            IReadOnlyList<SimulationEvent> events = AdvanceFractionally(scenario, seed: 0);

            Assert.Null(scenario.Body.Motion);
            Assert.Empty(events);
        }

        /// <summary>Verifies the ordered transition from a completed slide into falling.</summary>
        [Fact]
        public void CompletedRightSlideTransitionsToFalling()
        {
            SlidingScenario scenario = CreateScenario();
            _ = AddSolid(scenario.World, new GridPosition(1, 1));
            SimulationWorld simulation = CreateSimulation(scenario.World, seed: 0);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new GridPosition(3, 2), scenario.Body.Position);
            Assert.NotNull(scenario.Body.Motion);
            Assert.Equal(MotionCause.Falling, scenario.Body.Motion.Cause);
            Assert.Equal(Direction.Down, scenario.Body.Motion.SegmentDirection);
            Assert.Equal(
                [
                    new SlideStarted(scenario.Body.Id, Direction.Right),
                    new CellEntered(scenario.Body.Id, new GridPosition(3, 1)),
                    new FallStarted(scenario.Body.Id, Direction.Down),
                ],
                events);
        }

        /// <summary>Verifies that an authoritative destination prevents a conflicting slide from starting.</summary>
        [Fact]
        public void AuthoritativeDestinationPreventsConflictingSlideStart()
        {
            WorldGrid world = new(5, 5);
            Entity earlier = new(
                EntityId.New(),
                new GridPosition(3, 0),
                InteractionMask.BlocksMovement,
                new GravityBody(1, canSlide: false));
            Entity body = new(
                EntityId.New(),
                new GridPosition(2, 1),
                InteractionMask.None,
                new GravityBody(1, canSlide: true));
            world.Add(earlier);
            world.Add(body);
            _ = AddSolid(world, new GridPosition(2, 2));
            _ = AddSolid(world, new GridPosition(1, 1));

            IReadOnlyList<SimulationEvent> events = CreateSimulation(world, seed: 0).Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new GridPosition(3, 2), earlier.Position);
            Assert.Equal(new GridPosition(2, 1), body.Position);
            Assert.Null(body.Motion);
            Assert.Equal(
                [
                    new FallStarted(earlier.Id, Direction.Down),
                    new CellEntered(earlier.Id, new GridPosition(3, 1)),
                ],
                events);
        }

        private static IReadOnlyList<SimulationEvent> AdvanceFractionally(
            SlidingScenario scenario,
            int seed)
        {
            return CreateSimulation(scenario.World, seed).Advance(TimeSpan.FromSeconds(0.25));
        }

        private static SimulationWorld CreateSimulation(WorldGrid world, int seed)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                random: new Random(seed));
        }

        private static SlidingScenario CreateScenario(bool canSlide = true)
        {
            WorldGrid world = new(5, 5);
            Entity body = new(
                EntityId.New(),
                new GridPosition(2, 1),
                InteractionMask.None,
                new GravityBody(1, canSlide));
            world.Add(body);
            _ = AddSolid(world, new GridPosition(2, 2));
            return new SlidingScenario(world, body);
        }

        private static Entity AddSolid(WorldGrid world, GridPosition position)
        {
            Entity entity = new(EntityId.New(), position, InteractionMask.BlocksMovement);
            world.Add(entity);
            return entity;
        }

        private static void AssertSlide(Entity body, Direction direction)
        {
            Assert.NotNull(body.Motion);
            Assert.Equal(MotionCause.Sliding, body.Motion.Cause);
            Assert.Equal(direction, body.Motion.SegmentDirection);
            Assert.Equal(0.25, body.Motion.Progress, precision: 10);
        }

        private sealed record SlidingScenario(WorldGrid World, Entity Body);
    }
}
