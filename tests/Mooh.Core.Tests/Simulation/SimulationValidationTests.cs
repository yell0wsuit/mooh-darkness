using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies simulation dependency, argument, and no-op validation.
    /// </summary>
    public sealed class SimulationValidationTests
    {
        /// <summary>Verifies that elapsed simulation time cannot be negative.</summary>
        [Fact]
        public void AdvanceRejectsNegativeElapsedTime()
        {
            SimulationWorld simulation = CreateSimulation(new WorldGrid(3, 3));

            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => simulation.Advance(TimeSpan.FromTicks(-1)));
        }

        /// <summary>Verifies that zero elapsed time cannot start an unsupported body.</summary>
        [Fact]
        public void ZeroElapsedTimeDoesNotChangeState()
        {
            WorldGrid world = new(3, 3);
            Entity body = new(
                EntityId.New(),
                new GridPosition(1, 1),
                InteractionMask.None,
                new GravityBody(1, canSlide: false));
            world.Add(body);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.Zero);

            Assert.Null(body.Motion);
            Assert.Equal(new GridPosition(1, 1), body.Position);
            Assert.Empty(events);
        }

        /// <summary>Verifies that maximum substep travel is finite and within one cell.</summary>
        /// <param name="maximumTravelPerSubstep">The invalid travel limit.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1.1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void ConstructorRejectsInvalidMaximumTravel(double maximumTravelPerSubstep)
        {
            WorldGrid world = new(3, 3);

            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new SimulationWorld(
                    world,
                    Direction.Down,
                    new DefaultGravityCollisionPolicy(),
                    maximumTravelPerSubstep));
        }

        /// <summary>Verifies constructor rejection of a null world.</summary>
        [Fact]
        public void ConstructorRejectsNullWorld()
        {
            _ = Assert.Throws<ArgumentNullException>(
                () => new SimulationWorld(null!, Direction.Down, new DefaultGravityCollisionPolicy()));
        }

        /// <summary>Verifies constructor rejection of a null collision policy.</summary>
        [Fact]
        public void ConstructorRejectsNullCollisionPolicy()
        {
            _ = Assert.Throws<ArgumentNullException>(
                () => new SimulationWorld(new WorldGrid(3, 3), Direction.Down, null!));
        }

        /// <summary>Verifies constructor rejection of a null random generator.</summary>
        [Fact]
        public void ConstructorRejectsNullRandomGenerator()
        {
            _ = Assert.Throws<ArgumentNullException>(
                () => new SimulationWorld(
                    new WorldGrid(3, 3),
                    Direction.Down,
                    new DefaultGravityCollisionPolicy(),
                    null!));
        }

        /// <summary>Verifies rejection of an undefined gravity direction.</summary>
        [Fact]
        public void SetGravityRejectsUndefinedDirection()
        {
            SimulationWorld simulation = CreateSimulation(new WorldGrid(3, 3));

            _ = Assert.Throws<ArgumentOutOfRangeException>(() => simulation.SetGravity((Direction)int.MaxValue));
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(world, Direction.Down, new DefaultGravityCollisionPolicy());
        }
    }
}
