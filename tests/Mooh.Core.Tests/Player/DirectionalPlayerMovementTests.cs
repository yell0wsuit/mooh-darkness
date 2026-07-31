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
    /// Verifies player movement behavior after rotating scenarios through every cardinal direction.
    /// </summary>
    public sealed class DirectionalPlayerMovementTests
    {
        private const InteractionMask Pushable =
            InteractionMask.BlocksMovement | InteractionMask.PlayerPushable;

        /// <summary>Gets every cardinal player movement direction.</summary>
        public static TheoryData<Direction> Directions =>
            [
                Direction.Left,
                Direction.Up,
                Direction.Right,
                Direction.Down,
            ];

        /// <summary>Verifies fractional walking in every direction.</summary>
        /// <param name="direction">The walking direction.</param>
        [Theory]
        [MemberData(nameof(Directions))]
        public void WalkingIsDirectionIndependent(Direction direction)
        {
            (WorldGrid _, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 2,
                pushSpeedMultiplier: 1.5);
            simulation.SetPlayerInput(Held(direction));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Equal(new GridPosition(4, 4), player.Position);
            Assert.NotNull(player.Motion);
            Assert.Equal(direction, player.Motion.SegmentDirection);
            Assert.Equal(0.5, player.Motion.Progress, precision: 12);
            Assert.Equal([new PlayerMoveStarted(player.Id, direction)], events);
        }

        /// <summary>Verifies same-axis reversal in every direction.</summary>
        /// <param name="direction">The initial walking direction.</param>
        [Theory]
        [MemberData(nameof(Directions))]
        public void ReversalIsDirectionIndependent(Direction direction)
        {
            (WorldGrid _, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 2,
                pushSpeedMultiplier: 1.5);
            simulation.SetPlayerInput(Held(direction));
            _ = simulation.Advance(TimeSpan.FromSeconds(0.25));
            Direction reverse = direction.Opposite();
            simulation.SetPlayerInput(Held(reverse));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.Equal(0.3, player.Motion!.Progress, precision: 12);
            Assert.Equal(reverse, player.Motion.TravelDirection);
            Assert.Equal([new PlayerReversed(player.Id, reverse)], events);
        }

        /// <summary>Verifies perpendicular boundary turns in every direction.</summary>
        /// <param name="direction">The initial walking direction.</param>
        [Theory]
        [MemberData(nameof(Directions))]
        public void BoundaryTurningIsDirectionIndependent(Direction direction)
        {
            (WorldGrid _, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 2,
                pushSpeedMultiplier: 1.5);
            simulation.SetPlayerInput(Held(direction));
            _ = simulation.Advance(TimeSpan.FromSeconds(0.25));
            Direction turn = direction.TurnLeft();
            simulation.SetPlayerInput(Held(turn));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            GridPosition entered = new GridPosition(4, 4).Offset(direction);
            Assert.Equal(entered, player.Position);
            Assert.NotNull(player.Motion);
            Assert.Equal(turn, player.Motion.SegmentDirection);
            Assert.Equal(
                [
                    new CellEntered(player.Id, entered),
                    new PlayerMoveStarted(player.Id, turn),
                ],
                events);
        }

        /// <summary>Verifies single-object pushing in every direction.</summary>
        /// <param name="direction">The push direction.</param>
        [Theory]
        [MemberData(nameof(Directions))]
        public void SinglePushIsDirectionIndependent(Direction direction)
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1.5);
            GridPosition blockStart = player.Position.Offset(direction);
            Entity block = Add(world, blockStart, Pushable);
            simulation.SetPlayerInput(Held(direction));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(
                TimeSpan.FromSeconds(2.0 / 3.0));

            GridPosition blockDestination = blockStart.Offset(direction);
            Assert.Equal(blockDestination, block.Position);
            Assert.Equal(new GridPosition(4, 4), player.Position);
            Assert.Equal(
                [
                    new PushStarted(block.Id, direction),
                    new PlayerMoveStarted(player.Id, direction),
                    new CellEntered(block.Id, blockDestination),
                ],
                events);
        }

        /// <summary>Verifies push-to-gravity takeover in every rotated orientation.</summary>
        /// <param name="direction">The push direction.</param>
        [Theory]
        [MemberData(nameof(Directions))]
        public void PushToGravityIsDirectionIndependent(Direction direction)
        {
            Direction gravityDirection = direction.TurnRight();
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1.5,
                gravityDirection);
            GridPosition blockStart = player.Position.Offset(direction);
            Entity block = Add(
                world,
                blockStart,
                Pushable,
                new GravityBody(fallSpeed: 1, canSlide: false));
            _ = Add(world, blockStart.Offset(gravityDirection), InteractionMask.BlocksMovement);
            simulation.SetPlayerInput(Held(direction));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(
                TimeSpan.FromSeconds(2.0 / 3.0));

            GridPosition blockDestination = blockStart.Offset(direction);
            Assert.Equal(blockDestination, block.Position);
            Assert.NotNull(block.Motion);
            Assert.Equal(MotionCause.Falling, block.Motion.Cause);
            Assert.Equal(gravityDirection, block.Motion.SegmentDirection);
            Assert.Equal(
                [
                    new PushStarted(block.Id, direction),
                    new PlayerMoveStarted(player.Id, direction),
                    new CellEntered(block.Id, blockDestination),
                    new FallStarted(block.Id, gravityDirection),
                ],
                events);
        }

        private static PlayerInputState Held(Direction direction)
        {
            return direction switch
            {
                Direction.Left => new PlayerInputState(Left: true, Up: false, Right: false, Down: false),
                Direction.Up => new PlayerInputState(Left: false, Up: true, Right: false, Down: false),
                Direction.Right => new PlayerInputState(Left: false, Up: false, Right: true, Down: false),
                Direction.Down => new PlayerInputState(Left: false, Up: false, Right: false, Down: true),
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
        }

        private static (WorldGrid World, Entity Player, SimulationWorld Simulation) CreateScenario(
            double moveSpeed,
            double pushSpeedMultiplier,
            Direction gravityDirection = Direction.Down)
        {
            WorldGrid world = new(9, 9);
            Entity player = new(
                EntityId.New(),
                new GridPosition(4, 4),
                InteractionMask.None,
                playerBody: new PlayerBody(moveSpeed, pushSpeedMultiplier, Direction.Down));
            world.Add(player);
            SimulationWorld simulation = new(
                world,
                gravityDirection,
                new DefaultGravityCollisionPolicy());
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
    }
}
