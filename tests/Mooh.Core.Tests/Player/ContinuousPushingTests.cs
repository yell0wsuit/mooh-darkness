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
    /// Verifies continuous player push chains and independent pushed-object continuation.
    /// </summary>
    public sealed class ContinuousPushingTests
    {
        private const InteractionMask Pushable =
            InteractionMask.BlocksMovement | InteractionMask.PlayerPushable;

        /// <summary>Verifies push speed and event order before the player reaches its boundary.</summary>
        [Fact]
        public void PushedBlockEntersBeforePlayerAtMultipliedSpeed()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1.5);
            Entity block = Add(world, x: 2, Pushable);
            simulation.SetPlayerInput(HeldRight);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(
                TimeSpan.FromSeconds(2.0 / 3.0));

            Assert.Equal(new GridPosition(4, 1), block.Position);
            Assert.NotNull(block.Motion);
            Assert.Equal(MotionCause.PlayerPushed, block.Motion.Cause);
            Assert.Equal(new GridPosition(2, 1), player.Position);
            Assert.NotNull(player.Motion);
            Assert.Equal(2.0 / 3.0, player.Motion.Progress, precision: 6);
            Assert.Equal(
                [
                    new PushStarted(block.Id, Direction.Right),
                    new PlayerMoveStarted(player.Id, Direction.Right),
                    new CellEntered(block.Id, new GridPosition(3, 1)),
                ],
                events);
        }

        /// <summary>Verifies farthest-first start and equal-time boundary resolution for a chain.</summary>
        [Fact]
        public void PushChainResolvesFarthestFirstAtEqualBoundaryTime()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1);
            Entity first = Add(world, x: 2, Pushable);
            Entity second = Add(world, x: 3, Pushable);
            simulation.SetPlayerInput(HeldRight);

            IReadOnlyList<SimulationEvent> started = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.Equal(0, second.Motion!.ResolutionOrder);
            Assert.Equal(1, first.Motion!.ResolutionOrder);
            Assert.Equal(
                [
                    new PushStarted(second.Id, Direction.Right),
                    new PushStarted(first.Id, Direction.Right),
                    new PlayerMoveStarted(player.Id, Direction.Right),
                ],
                started);

            simulation.SetPlayerInput(default);
            IReadOnlyList<SimulationEvent> crossed = simulation.Advance(TimeSpan.FromSeconds(0.9));

            Assert.Equal(new GridPosition(5, 1), second.Position);
            Assert.Equal(new GridPosition(4, 1), first.Position);
            Assert.Equal(new GridPosition(2, 1), player.Position);
            Assert.Equal(
                [
                    new CellEntered(second.Id, new GridPosition(4, 1)),
                    new CellEntered(first.Id, new GridPosition(3, 1)),
                    new CellEntered(player.Id, new GridPosition(2, 1)),
                    new PlayerStopped(player.Id),
                ],
                crossed);
        }

        /// <summary>Verifies that same-cell pushables cross together in deterministic plan order.</summary>
        [Fact]
        public void SameCellPushablesCrossTogetherInPlanOrder()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1);
            Entity first = Add(world, x: 2, Pushable);
            Entity second = Add(world, x: 2, Pushable);
            simulation.SetPlayerInput(HeldRight);
            _ = simulation.Advance(TimeSpan.FromSeconds(0.1));
            simulation.SetPlayerInput(default);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.9));

            Assert.Equal([first, second], world.GetOccupants(new GridPosition(4, 1)));
            Assert.Equal(new GridPosition(2, 1), player.Position);
            Assert.Equal(
                [
                    new CellEntered(first.Id, new GridPosition(3, 1)),
                    new CellEntered(second.Id, new GridPosition(3, 1)),
                    new CellEntered(player.Id, new GridPosition(2, 1)),
                    new PlayerStopped(player.Id),
                ],
                events);
        }

        /// <summary>Verifies that a pushed block continues cell-by-cell until its next cell is blocked.</summary>
        [Fact]
        public void PushedBlockContinuesUntilBlocked()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1.5);
            Entity block = Add(world, x: 2, Pushable);
            _ = Add(world, x: 5, InteractionMask.BlocksMovement);
            simulation.SetPlayerInput(HeldRight);
            _ = simulation.Advance(TimeSpan.FromSeconds(0.1));
            simulation.SetPlayerInput(default);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1.9));

            Assert.Equal(new GridPosition(4, 1), block.Position);
            Assert.Null(block.Motion);
            Assert.Equal(new GridPosition(2, 1), player.Position);
            Assert.Null(player.Motion);
            Assert.Equal(
                [
                    new CellEntered(block.Id, new GridPosition(3, 1)),
                    new CellEntered(player.Id, new GridPosition(2, 1)),
                    new PlayerStopped(player.Id),
                    new CellEntered(block.Id, new GridPosition(4, 1)),
                ],
                events);
        }

        /// <summary>Verifies that an occupant added to the authoritative destination does not cancel transit.</summary>
        [Fact]
        public void NewlyOccupiedAuthoritativeDestinationDoesNotCancelPushedBlock()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1.5);
            Entity block = Add(world, x: 2, Pushable);
            simulation.SetPlayerInput(HeldRight);
            _ = simulation.Advance(TimeSpan.FromSeconds(0.1));
            simulation.SetPlayerInput(default);
            Entity blocker = Add(world, x: 3, InteractionMask.BlocksMovement);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.5));

            Assert.Equal(new GridPosition(3, 1), block.Position);
            Assert.NotNull(block.Motion);
            Assert.Equal([player], world.GetOccupants(new GridPosition(2, 1)));
            Assert.Equal([block, blocker], world.GetOccupants(new GridPosition(3, 1)));
            Assert.Equal(new GridPosition(2, 1), player.Position);
            Assert.DoesNotContain(events, item => item is CellEntered entered && entered.EntityId == block.Id);
        }

        private static PlayerInputState HeldRight => new(
            Left: false,
            Up: false,
            Right: true,
            Down: false);

        private static (WorldGrid World, Entity Player, SimulationWorld Simulation) CreateScenario(
            double moveSpeed,
            double pushSpeedMultiplier)
        {
            WorldGrid world = new(8, 3);
            Entity player = new(
                EntityId.New(),
                new GridPosition(1, 1),
                InteractionMask.None,
                playerBody: new PlayerBody(moveSpeed, pushSpeedMultiplier, Direction.Right));
            world.Add(player);
            SimulationWorld simulation = new(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy());
            return (world, player, simulation);
        }

        private static Entity Add(WorldGrid world, int x, InteractionMask interactions)
        {
            Entity entity = new(EntityId.New(), new GridPosition(x, 1), interactions);
            world.Add(entity);
            return entity;
        }
    }
}
