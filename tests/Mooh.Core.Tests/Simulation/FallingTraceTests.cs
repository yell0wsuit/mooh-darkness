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
    /// Verifies ordered cell crossings and falling termination.
    /// </summary>
    public sealed class FallingTraceTests
    {
        /// <summary>Verifies a multi-cell fall ending immediately above solid support.</summary>
        [Fact]
        public void FallingBodyCrossesCellsAndLandsOnSupport()
        {
            WorldGrid world = new(5, 5);
            Entity body = AddBody(world, new GridPosition(2, 0), 1);
            _ = AddEntity(world, new GridPosition(2, 3), InteractionMask.BlocksMovement);

            IReadOnlyList<SimulationEvent> events = CreateSimulation(world).Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(new GridPosition(2, 2), body.Position);
            Assert.Null(body.Motion);
            Assert.Equal(
                [
                    new FallStarted(body.Id, Direction.Down),
                    new CellEntered(body.Id, new GridPosition(2, 1)),
                    new CellEntered(body.Id, new GridPosition(2, 2)),
                    new Landed(body.Id, Direction.Down),
                ],
                events);
        }

        /// <summary>Verifies that one update can cross several open cells.</summary>
        [Fact]
        public void FallingBodyCrossesMultipleOpenCells()
        {
            WorldGrid world = new(3, 6);
            Entity body = AddBody(world, new GridPosition(1, 0), 2);

            IReadOnlyList<SimulationEvent> events = CreateSimulation(world).Advance(TimeSpan.FromSeconds(1.5));

            Assert.Equal(new GridPosition(1, 3), body.Position);
            Assert.NotNull(body.Motion);
            Assert.Equal(MotionCause.Falling, body.Motion.Cause);
            Assert.Equal(0, body.Motion.Progress, precision: 10);
            Assert.Equal(
                [
                    new FallStarted(body.Id, Direction.Down),
                    new CellEntered(body.Id, new GridPosition(1, 1)),
                    new CellEntered(body.Id, new GridPosition(1, 2)),
                    new CellEntered(body.Id, new GridPosition(1, 3)),
                ],
                events);
        }

        /// <summary>Verifies landing after entering the final in-bounds cell.</summary>
        [Fact]
        public void FallingBodyLandsAtWorldBoundary()
        {
            WorldGrid world = new(3, 5);
            Entity body = AddBody(world, new GridPosition(1, 3), 1);

            IReadOnlyList<SimulationEvent> events = CreateSimulation(world).Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new GridPosition(1, 4), body.Position);
            Assert.Null(body.Motion);
            Assert.Equal(
                [
                    new FallStarted(body.Id, Direction.Down),
                    new CellEntered(body.Id, new GridPosition(1, 4)),
                    new Landed(body.Id, Direction.Down),
                ],
                events);
        }

        /// <summary>Verifies stable crossing order when an earlier mover occupies a shared destination.</summary>
        [Fact]
        public void EarlierEntityCanBlockLaterEntityInSameBoundaryBatch()
        {
            WorldGrid world = new(3, 4);
            Entity first = AddBody(world, new GridPosition(1, 0), 1, InteractionMask.BlocksMovement);
            Entity second = AddBody(world, new GridPosition(1, 0), 1);

            IReadOnlyList<SimulationEvent> events = CreateSimulation(world).Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new GridPosition(1, 1), first.Position);
            Assert.NotNull(first.Motion);
            Assert.Equal(new GridPosition(1, 0), second.Position);
            Assert.Null(second.Motion);
            Assert.Equal(
                [
                    new FallStarted(first.Id, Direction.Down),
                    new FallStarted(second.Id, Direction.Down),
                    new CellEntered(first.Id, new GridPosition(1, 1)),
                    new Landed(second.Id, Direction.Down),
                ],
                events);
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(world, Direction.Down, new DefaultGravityCollisionPolicy());
        }

        private static Entity AddBody(
            WorldGrid world,
            GridPosition position,
            double speed,
            InteractionMask interactions = InteractionMask.None)
        {
            return AddEntity(world, position, interactions, new GravityBody(speed, canSlide: false));
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
