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

        /// <summary>
        /// Matches the only recovered combination that survives both gravity-axis gates: the
        /// balloon and the bubble carry <c>CNT_CMaskObjVPush</c> and <c>CNT_CMaskPlCanPushThEat</c>
        /// together.
        /// </summary>
        private const InteractionMask GravityAxisPushable = Pushable
            | InteractionMask.GravityAxisPushable | InteractionMask.PushesThroughOccupants;

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
                pushSpeedMultiplier: 1.5,
                facingDirection: direction);
            simulation.SetPlayerInput(Held(direction));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Equal(new GridPosition(4, 4).Offset(direction), player.Position);
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
                pushSpeedMultiplier: 1.5,
                facingDirection: direction);
            simulation.SetPlayerInput(Held(direction));
            _ = simulation.Advance(TimeSpan.FromSeconds(0.25));
            Direction reverse = direction.Opposite();
            simulation.SetPlayerInput(Held(reverse));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.Null(player.Motion);
            Assert.NotNull(player.PlayerBody!.Rotation);
            Assert.Equal(0.11, player.PlayerBody.Rotation.RemainingSeconds, precision: 12);
            Assert.Equal([new PlayerRotationStarted(player.Id, direction, reverse)], events);
        }

        /// <summary>Verifies perpendicular boundary turns in every direction.</summary>
        /// <param name="direction">The initial walking direction.</param>
        [Theory]
        [MemberData(nameof(Directions))]
        public void BoundaryTurningIsDirectionIndependent(Direction direction)
        {
            (WorldGrid _, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 2,
                pushSpeedMultiplier: 1.5,
                facingDirection: direction);
            simulation.SetPlayerInput(Held(direction));
            _ = simulation.Advance(TimeSpan.FromSeconds(0.25));
            Direction turn = direction.TurnLeft();
            simulation.SetPlayerInput(Held(turn));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            GridPosition entered = new GridPosition(4, 4).Offset(direction);
            Assert.Equal(entered, player.Position);
            Assert.Null(player.Motion);
            Assert.NotNull(player.PlayerBody!.Rotation);
            Assert.Equal(
                [
                    new CellEntered(player.Id, entered),
                    new PlayerRotationStarted(player.Id, direction, turn),
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
                pushSpeedMultiplier: 1.5,
                facingDirection: direction);
            GridPosition blockStart = player.Position.Offset(direction);
            InteractionMask interactions = IsOnSameAxis(direction, Direction.Down)
                ? GravityAxisPushable
                : Pushable;
            Entity block = Add(world, blockStart, interactions);
            simulation.SetPlayerInput(Held(direction));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(
                TimeSpan.FromSeconds(2.0 / 3.0));

            GridPosition blockDestination = blockStart.Offset(direction);
            Assert.Equal(blockDestination, block.Position);
            Assert.Null(block.Motion);
            Assert.Equal(blockStart, player.Position);
            Assert.Equal(
                [
                    new PushStarted(block.Id, direction),
                    new PlayerMoveStarted(player.Id, direction),
                    new CellEntered(block.Id, blockDestination),
                ],
                events);
        }

        /// <summary>Verifies that every pushed occupant needs permission for gravity-axis pushes.</summary>
        [Fact]
        public void GravityAxisPushRequiresCapabilityOnEveryPushedOccupant()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1.5,
                gravityDirection: Direction.Down,
                facingDirection: Direction.Down);
            GridPosition blockStart = player.Position.Offset(Direction.Down);
            Entity capable = Add(world, blockStart, GravityAxisPushable);
            Entity ordinary = Add(world, blockStart, Pushable);
            simulation.SetPlayerInput(Held(Direction.Down));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.Null(player.Motion);
            Assert.Null(capable.Motion);
            Assert.Null(ordinary.Motion);
            Assert.Equal([new PlayerBlocked(player.Id, Direction.Down)], events);
        }

        /// <summary>
        /// Verifies the recovered second gravity-axis gate. <c>GamePlayer.CtrlMove</c> reaches the
        /// ordinary push branch through <c>elif vm and ...</c>, so an occupant that clears the
        /// <c>CNT_CMaskObjVPush</c> gate but carries no <c>CNT_CMaskPlCanPushThEat</c> — the
        /// recovered barrel — still cannot be pushed along gravity.
        /// </summary>
        [Fact]
        public void GravityAxisPushAlsoRequiresThePushThroughEatCapability()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1.5,
                gravityDirection: Direction.Down,
                facingDirection: Direction.Down);
            Entity barrel = Add(
                world,
                player.Position.Offset(Direction.Down),
                Pushable | InteractionMask.GravityAxisPushable);
            simulation.SetPlayerInput(Held(Direction.Down));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.Null(player.Motion);
            Assert.Null(barrel.Motion);
            Assert.Equal([new PlayerBlocked(player.Id, Direction.Down)], events);
        }

        /// <summary>
        /// Verifies the same occupant is still pushable perpendicular to gravity, where the
        /// recovered branch only requires an empty destination.
        /// </summary>
        [Fact]
        public void PushThroughEatIsNotRequiredPerpendicularToGravity()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1.5,
                gravityDirection: Direction.Down,
                facingDirection: Direction.Right);
            Entity barrel = Add(
                world,
                player.Position.Offset(Direction.Right),
                Pushable | InteractionMask.GravityAxisPushable);
            simulation.SetPlayerInput(Held(Direction.Right));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.NotNull(barrel.Motion);
            Assert.Equal(
                [
                    new PushStarted(barrel.Id, Direction.Right),
                    new PlayerMoveStarted(player.Id, Direction.Right),
                ],
                events);
        }

        /// <summary>Verifies that ordinary pushes remain available perpendicular to gravity.</summary>
        [Fact]
        public void OrdinaryPushPerpendicularToGravityStarts()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateScenario(
                moveSpeed: 1,
                pushSpeedMultiplier: 1.5,
                gravityDirection: Direction.Down,
                facingDirection: Direction.Right);
            Entity block = Add(world, player.Position.Offset(Direction.Right), Pushable);
            simulation.SetPlayerInput(Held(Direction.Right));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.NotNull(player.Motion);
            Assert.NotNull(block.Motion);
            Assert.Equal(
                [
                    new PushStarted(block.Id, Direction.Right),
                    new PlayerMoveStarted(player.Id, Direction.Right),
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
                gravityDirection,
                facingDirection: direction);
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
            Assert.Equal(blockDestination.Offset(gravityDirection), block.Position);
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

        private static bool IsOnSameAxis(Direction first, Direction second)
        {
            return ((int)first & 1) == ((int)second & 1);
        }

        private static (WorldGrid World, Entity Player, SimulationWorld Simulation) CreateScenario(
            double moveSpeed,
            double pushSpeedMultiplier,
            Direction gravityDirection = Direction.Down,
            Direction facingDirection = Direction.Down)
        {
            WorldGrid world = new(9, 9);
            Entity player = new(
                EntityId.New(),
                new GridPosition(4, 4),
                InteractionMask.None,
                playerBody: new PlayerBody(moveSpeed, pushSpeedMultiplier, facingDirection));
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
