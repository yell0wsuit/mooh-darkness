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
    /// Verifies starting continuous player walking from held input.
    /// </summary>
    public sealed class PlayerMovementStartTests
    {
        /// <summary>Verifies that held input commits destination authority before fractional walking completes.</summary>
        [Fact]
        public void HeldDirectionStartsFractionalWalking()
        {
            (WorldGrid World, Entity Player, SimulationWorld Simulation) context = CreatePlayerWorld();
            PlayerInputState input = new(Left: false, Up: false, Right: true, Down: false);

            context.Simulation.SetPlayerInput(input);

            Assert.Null(context.Player.Motion);
            Assert.Equal(Direction.Right, context.Player.PlayerBody!.FacingDirection);

            IReadOnlyList<SimulationEvent> events = context.Simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Equal(new GridPosition(3, 2), context.Player.Position);
            Assert.Empty(context.World.GetOccupants(new GridPosition(2, 2)));
            Assert.Equal([context.Player], context.World.GetOccupants(new GridPosition(3, 2)));
            Assert.NotNull(context.Player.Motion);
            Assert.Equal(MotionCause.PlayerWalking, context.Player.Motion.Cause);
            Assert.Equal(Direction.Right, context.Player.Motion.SegmentDirection);
            Assert.Equal(0.5, context.Player.Motion.Progress, precision: 12);
            Assert.Equal(Direction.Right, context.Player.PlayerBody.FacingDirection);
            Assert.Equal([new PlayerMoveStarted(context.Player.Id, Direction.Right)], events);
        }

        /// <summary>Verifies that no held direction leaves a stationary player unchanged.</summary>
        [Fact]
        public void NoInputLeavesPlayerStationary()
        {
            (WorldGrid _, Entity player, SimulationWorld simulation) = CreatePlayerWorld();

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Null(player.Motion);
            Assert.Equal(new GridPosition(2, 2), player.Position);
            Assert.Equal(Direction.Right, player.PlayerBody!.FacingDirection);
            Assert.Empty(events);
        }

        /// <summary>Verifies that the world boundary blocks walking after updating facing.</summary>
        [Fact]
        public void BoundaryBlocksWalking()
        {
            (WorldGrid _, Entity player, SimulationWorld simulation) = CreatePlayerWorld(
                playerPosition: new GridPosition(4, 2));
            simulation.SetPlayerInput(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Null(player.Motion);
            Assert.Equal(new GridPosition(4, 2), player.Position);
            Assert.Equal(Direction.Right, player.PlayerBody!.FacingDirection);
            Assert.Equal([new PlayerBlocked(player.Id, Direction.Right)], events);
        }

        /// <summary>Verifies that a solid occupant blocks walking.</summary>
        [Fact]
        public void OccupantBlocksWalking()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreatePlayerWorld();
            Entity blocker = new(
                EntityId.New(),
                new GridPosition(3, 2),
                InteractionMask.BlocksMovement);
            world.Add(blocker);
            simulation.SetPlayerInput(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Null(player.Motion);
            Assert.Equal(new GridPosition(2, 2), player.Position);
            Assert.Equal([blocker], world.GetOccupants(blocker.Position));
            Assert.Equal([new PlayerBlocked(player.Id, Direction.Right)], events);
        }

        /// <summary>Verifies fixed left, up, right, down priority when starting movement.</summary>
        [Fact]
        public void MultipleHeldDirectionsUseFixedPriority()
        {
            (WorldGrid _, Entity player, SimulationWorld simulation) = CreatePlayerWorld(
                facingDirection: Direction.Left);
            simulation.SetPlayerInput(new PlayerInputState(Left: true, Up: true, Right: true, Down: true));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.NotNull(player.Motion);
            Assert.Equal(Direction.Left, player.Motion.SegmentDirection);
            Assert.Equal(Direction.Left, player.PlayerBody!.FacingDirection);
            Assert.Equal([new PlayerMoveStarted(player.Id, Direction.Left)], events);
        }

        /// <summary>Verifies that gravity takes priority over walking for an unsupported player.</summary>
        [Fact]
        public void GravityStartsBeforePlayerWalking()
        {
            (WorldGrid _, Entity player, SimulationWorld simulation) = CreatePlayerWorld(
                gravityBody: new GravityBody(fallSpeed: 2, canSlide: false));
            simulation.SetPlayerInput(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.NotNull(player.Motion);
            Assert.Equal(MotionCause.Falling, player.Motion.Cause);
            Assert.Equal(Direction.Down, player.Motion.SegmentDirection);
            Assert.Equal(Direction.Right, player.PlayerBody!.FacingDirection);
            Assert.Equal([new FallStarted(player.Id, Direction.Down)], events);
        }

        /// <summary>Verifies that input cannot be assigned when the world has no player.</summary>
        [Fact]
        public void SetPlayerInputWithoutPlayerThrows()
        {
            WorldGrid world = new(5, 5);
            SimulationWorld simulation = new(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy());

            _ = Assert.Throws<InvalidOperationException>(
                () => simulation.SetPlayerInput(default));
        }

        private static (WorldGrid World, Entity Player, SimulationWorld Simulation) CreatePlayerWorld(
            GridPosition? playerPosition = null,
            GravityBody? gravityBody = null,
            Direction facingDirection = Direction.Right)
        {
            WorldGrid world = new(5, 5);
            Entity player = new(
                EntityId.New(),
                playerPosition ?? new GridPosition(2, 2),
                InteractionMask.None,
                gravityBody,
                new PlayerBody(moveSpeed: 2, pushSpeedMultiplier: 1.5, facingDirection));
            world.Add(player);
            SimulationWorld simulation = new(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy());
            return (world, player, simulation);
        }
    }
}
