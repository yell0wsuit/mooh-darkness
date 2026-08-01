using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies starting gravity-driven motion and advancing fractional transit.
    /// </summary>
    public sealed class FallingMotionTests
    {
        /// <summary>Verifies that an unsupported body starts falling and advances fractionally.</summary>
        [Fact]
        public void UnsupportedBodyStartsAndAdvancesFractionally()
        {
            WorldGrid world = new(5, 5);
            Entity entity = AddEntity(
                world,
                new GridPosition(2, 1),
                InteractionMask.None,
                new GravityBody(2, canSlide: false));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Equal(new GridPosition(2, 2), entity.Position);
            Assert.Empty(world.GetOccupants(new GridPosition(2, 1)));
            Assert.Equal([entity], world.GetOccupants(new GridPosition(2, 2)));
            Assert.NotNull(entity.Motion);
            Assert.Equal(MotionCause.Falling, entity.Motion.Cause);
            Assert.Equal(Direction.Down, entity.Motion.SegmentDirection);
            Assert.Equal(0.5, entity.Motion.Progress, precision: 10);
            Assert.Equal([new FallStarted(entity.Id, Direction.Down)], events);
        }

        /// <summary>Verifies that direct solid support prevents falling.</summary>
        [Fact]
        public void DirectlySupportedBodyDoesNotStart()
        {
            WorldGrid world = new(5, 5);
            Entity entity = AddEntity(
                world,
                new GridPosition(2, 1),
                InteractionMask.None,
                new GravityBody(2, canSlide: false));
            _ = AddEntity(world, new GridPosition(2, 2), InteractionMask.BlocksMovement);

            IReadOnlyList<SimulationEvent> events = CreateSimulation(world).Advance(TimeSpan.FromSeconds(0.25));

            Assert.Null(entity.Motion);
            Assert.Empty(events);
        }

        /// <summary>Verifies that a vacated cell cannot receive a falling body for 150 milliseconds.</summary>
        [Fact]
        public void VacatedCellBlocksFallingFor150Milliseconds()
        {
            WorldGrid world = new(5, 5);
            Entity body = AddEntity(
                world,
                new GridPosition(2, 1),
                InteractionMask.None,
                new GravityBody(1, canSlide: false));
            Entity player = new(
                EntityId.New(),
                new GridPosition(2, 2),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(10, 1, Direction.Right));
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            _ = simulation.Advance(TimeSpan.FromMilliseconds(100));
            simulation.SetPlayerInput(default);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(49));

            Assert.Equal(new GridPosition(2, 1), body.Position);
            Assert.Null(body.Motion);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(new GridPosition(2, 2), body.Position);
            Assert.NotNull(body.Motion);
            Assert.Equal([new FallStarted(body.Id, Direction.Down)], events);
        }

        /// <summary>Verifies that entities without gravity behavior remain stationary.</summary>
        [Fact]
        public void BodyWithoutGravityIsIgnored()
        {
            WorldGrid world = new(5, 5);
            Entity entity = AddEntity(world, new GridPosition(2, 1), InteractionMask.None);

            IReadOnlyList<SimulationEvent> events = CreateSimulation(world).Advance(TimeSpan.FromSeconds(0.25));

            Assert.Null(entity.Motion);
            Assert.Equal(new GridPosition(2, 1), entity.Position);
            Assert.Empty(events);
        }

        /// <summary>Verifies that zero elapsed time neither evaluates nor advances gravity.</summary>
        [Fact]
        public void ZeroElapsedTimeIsNoOp()
        {
            WorldGrid world = new(5, 5);
            Entity entity = AddEntity(
                world,
                new GridPosition(2, 1),
                InteractionMask.None,
                new GravityBody(2, canSlide: false));

            IReadOnlyList<SimulationEvent> events = CreateSimulation(world).Advance(TimeSpan.Zero);

            Assert.Null(entity.Motion);
            Assert.Empty(events);
        }

        /// <summary>Verifies that active motion cannot be started a second time.</summary>
        [Fact]
        public void KinematicsRejectsStartingEntityAlreadyInMotion()
        {
            Entity entity = new(
                EntityId.New(),
                new GridPosition(1, 1),
                InteractionMask.None,
                new GravityBody(1, canSlide: false));
            KinematicsSystem.Start(entity, MotionCause.Falling, Direction.Down, 1);

            _ = Assert.Throws<InvalidOperationException>(
                () => KinematicsSystem.Start(entity, MotionCause.Falling, Direction.Down, 1));
        }

        /// <summary>Verifies direct fractional advancement and stopping of active motion.</summary>
        [Fact]
        public void KinematicsAdvancesAndStopsMotion()
        {
            Entity entity = new(EntityId.New(), new GridPosition(1, 1), InteractionMask.None);
            KinematicsSystem.Start(entity, MotionCause.Sliding, Direction.Right, 2);

            KinematicsSystem.Advance(entity, TimeSpan.FromSeconds(0.25));

            Assert.NotNull(entity.Motion);
            Assert.Equal(0.5, entity.Motion.Progress, precision: 10);

            KinematicsSystem.Stop(entity);

            Assert.Null(entity.Motion);
        }

        /// <summary>Verifies that negative elapsed time is rejected.</summary>
        [Fact]
        public void NegativeElapsedTimeIsRejected()
        {
            WorldGrid world = new(5, 5);
            SimulationWorld simulation = CreateSimulation(world);

            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => simulation.Advance(TimeSpan.FromSeconds(-0.1)));
        }

        /// <summary>Verifies that substep travel is finite and within one cell.</summary>
        /// <param name="maximumTravelPerSubstep">The invalid travel limit.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1.1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void InvalidMaximumTravelIsRejected(double maximumTravelPerSubstep)
        {
            WorldGrid world = new(5, 5);

            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new SimulationWorld(
                    world,
                    Direction.Down,
                    new DefaultGravityCollisionPolicy(),
                    maximumTravelPerSubstep));
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(world, Direction.Down, new DefaultGravityCollisionPolicy());
        }

        private static Entity AddEntity(
            WorldGrid world,
            GridPosition position,
            InteractionMask interactions,
            GravityBody? gravityBody = null)
        {
            Entity entity = new(EntityId.New(), position, interactions, gravityBody);
            world.Add(entity);
            return entity;
        }
    }
}
