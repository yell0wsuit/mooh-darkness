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

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies gravity and round sliding takeover at pushed-object boundaries.
    /// </summary>
    public sealed class PushGravityTakeoverTests
    {
        private const InteractionMask Pushable =
            InteractionMask.BlocksMovement | InteractionMask.PlayerPushable;

        /// <summary>Verifies that an object pushed off support falls instead of continuing its push.</summary>
        [Fact]
        public void PushedObjectFallsImmediatelyAfterLeavingSupport()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario();
            Entity block = Add(
                world,
                new GridPosition(2, 1),
                Pushable,
                new GravityBody(fallSpeed: 1, canSlide: false));
            _ = AddSolid(world, new GridPosition(2, 2));
            simulation.SetPlayerInput(HeldRight);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(
                TimeSpan.FromSeconds(2.0 / 3.0));

            Assert.Equal(new GridPosition(3, 2), block.Position);
            Assert.NotNull(block.Motion);
            Assert.Equal(MotionCause.Falling, block.Motion.Cause);
            Assert.Equal(Direction.Down, block.Motion.SegmentDirection);
            Assert.Equal(
                [
                    new PushStarted(block.Id, Direction.Right),
                    new PlayerMoveStarted(player.Id, Direction.Right),
                    new CellEntered(block.Id, new GridPosition(3, 1)),
                    new FallStarted(block.Id, Direction.Down),
                ],
                events);
        }

        /// <summary>Verifies deterministic slide takeover and the subsequent transition to falling.</summary>
        [Fact]
        public void PushedRoundObjectSlidesThenFalls()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario();
            Entity block = Add(
                world,
                new GridPosition(2, 1),
                Pushable,
                new GravityBody(fallSpeed: 1, canSlide: true));
            _ = AddSolid(world, new GridPosition(2, 2));
            _ = AddSolid(world, new GridPosition(3, 2));
            _ = AddSolid(world, new GridPosition(1, 2));
            simulation.SetPlayerInput(HeldRight);

            IReadOnlyList<SimulationEvent> started = simulation.Advance(TimeSpan.FromSeconds(0.1));
            simulation.SetPlayerInput(default);
            IReadOnlyList<SimulationEvent> completed = simulation.Advance(
                TimeSpan.FromSeconds(47.0 / 30.0));

            Assert.Equal(
                [
                    new PushStarted(block.Id, Direction.Right),
                    new PlayerMoveStarted(player.Id, Direction.Right),
                ],
                started);
            Assert.Equal(new GridPosition(4, 2), block.Position);
            Assert.NotNull(block.Motion);
            Assert.Equal(MotionCause.Falling, block.Motion.Cause);
            Assert.Equal(
                [
                    new CellEntered(block.Id, new GridPosition(3, 1)),
                    new SlideStarted(block.Id, Direction.Right),
                    new CellEntered(player.Id, new GridPosition(2, 1)),
                    new PlayerStopped(player.Id),
                    new CellEntered(block.Id, new GridPosition(4, 1)),
                    new FallStarted(block.Id, Direction.Down),
                ],
                completed);
        }

        private static PlayerInputState HeldRight => new(
            Left: false,
            Up: false,
            Right: true,
            Down: false);

        private static (WorldGrid World, Entity Player, SimulationWorld Simulation) CreateScenario()
        {
            WorldGrid world = new(8, 5);
            Entity player = new(
                EntityId.New(),
                new GridPosition(1, 1),
                InteractionMask.None,
                playerBody: new PlayerBody(moveSpeed: 1, pushSpeedMultiplier: 1.5, Direction.Right));
            world.Add(player);
            SimulationWorld simulation = new(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                random: new Random(0));
            return (world, player, simulation);
        }

        private static Entity Add(
            WorldGrid world,
            GridPosition position,
            InteractionMask interactions,
            GravityBody? gravityBody = null)
        {
            Entity entity = new(EntityId.New(), position, interactions, gravityBody);
            world.Add(entity);
            return entity;
        }

        private static Entity AddSolid(WorldGrid world, GridPosition position)
        {
            return Add(
                world,
                position,
                InteractionMask.BlocksMovement | InteractionMask.RoundSupport);
        }
    }
}
