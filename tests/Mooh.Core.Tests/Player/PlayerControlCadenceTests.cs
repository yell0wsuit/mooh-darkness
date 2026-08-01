using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies mid-cell reversal and boundary-based player control cadence.
    /// </summary>
    public sealed class PlayerControlCadenceTests
    {
        /// <summary>Verifies that opposite same-axis input reverses fractional walking immediately.</summary>
        [Fact]
        public void OppositeInputReversesImmediately()
        {
            (Entity player, SimulationWorld simulation) = CreateScenario();
            StartRightToHalfCell(simulation);

            simulation.SetPlayerInput(Held(Direction.Left));
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.Equal(0.3, player.Motion!.Progress, precision: 12);
            Assert.Equal(Direction.Left, player.Motion.TravelDirection);
            Assert.Equal(Direction.Left, player.PlayerBody!.FacingDirection);
            Assert.Equal([new PlayerReversed(player.Id, Direction.Left)], events);
        }

        /// <summary>Verifies that holding the opposite key does not toggle direction every update.</summary>
        [Fact]
        public void RepeatedOppositeInputDoesNotToggleRepeatedly()
        {
            (Entity player, SimulationWorld simulation) = CreateScenario();
            StartRightToHalfCell(simulation);
            simulation.SetPlayerInput(Held(Direction.Left));
            _ = simulation.Advance(TimeSpan.FromSeconds(0.1));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.Equal(0.1, player.Motion!.Progress, precision: 12);
            Assert.Equal(Direction.Left, player.Motion.TravelDirection);
            Assert.Empty(events);
        }

        /// <summary>Verifies that forward input resumes forward travel after reversal.</summary>
        [Fact]
        public void ForwardInputAfterReversalResumesForwardTravel()
        {
            (Entity player, SimulationWorld simulation) = CreateScenario();
            StartRightToHalfCell(simulation);
            simulation.SetPlayerInput(Held(Direction.Left));
            _ = simulation.Advance(TimeSpan.FromSeconds(0.1));

            simulation.SetPlayerInput(Held(Direction.Right));
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.Equal(0.5, player.Motion!.Progress, precision: 12);
            Assert.Equal(Direction.Right, player.Motion.TravelDirection);
            Assert.Equal(Direction.Right, player.PlayerBody!.FacingDirection);
            Assert.Equal([new PlayerReversed(player.Id, Direction.Right)], events);
        }

        /// <summary>Verifies return to the origin and immediate reevaluation of held input.</summary>
        [Fact]
        public void ReachingOriginReturnsAndStartsFromCurrentInput()
        {
            (Entity player, SimulationWorld simulation) = CreateScenario();
            StartRightToHalfCell(simulation);
            simulation.SetPlayerInput(Held(Direction.Left));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Equal(new GridPosition(1, 2), player.Position);
            Assert.NotNull(player.Motion);
            Assert.Equal(Direction.Left, player.Motion.SegmentDirection);
            Assert.Equal(0, player.Motion.Progress, precision: 12);
            Assert.Equal(
                [
                    new PlayerReversed(player.Id, Direction.Left),
                    new PlayerReturned(player.Id, new GridPosition(2, 2)),
                    new PlayerMoveStarted(player.Id, Direction.Left),
                ],
                events);
        }

        /// <summary>Verifies that perpendicular input does not alter an active segment.</summary>
        [Fact]
        public void PerpendicularInputDoesNotAlterActiveSegment()
        {
            (Entity player, SimulationWorld simulation) = CreateScenario();
            StartRightToHalfCell(simulation);
            simulation.SetPlayerInput(Held(Direction.Up));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.1));

            Assert.Equal(0.7, player.Motion!.Progress, precision: 12);
            Assert.Equal(Direction.Right, player.Motion.TravelDirection);
            Assert.Equal(Direction.Right, player.PlayerBody!.FacingDirection);
            Assert.Empty(events);
        }

        /// <summary>Verifies that released input coasts to the boundary and then stops.</summary>
        [Fact]
        public void ReleasedInputCoastsToBoundaryThenStops()
        {
            (Entity player, SimulationWorld simulation) = CreateScenario();
            StartRightToHalfCell(simulation);
            simulation.SetPlayerInput(default);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Equal(new GridPosition(3, 2), player.Position);
            Assert.Null(player.Motion);
            Assert.Equal(
                [
                    new CellEntered(player.Id, new GridPosition(3, 2)),
                    new PlayerStopped(player.Id),
                ],
                events);
        }

        /// <summary>Verifies that held forward input starts the next segment at a boundary.</summary>
        [Fact]
        public void HeldForwardContinuesIntoNextSegment()
        {
            (Entity player, SimulationWorld simulation) = CreateScenario();
            StartRightToHalfCell(simulation);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.5));

            Assert.Equal(new GridPosition(4, 2), player.Position);
            Assert.NotNull(player.Motion);
            Assert.Equal(Direction.Right, player.Motion.SegmentDirection);
            Assert.Equal(0.5, player.Motion.Progress, precision: 12);
            Assert.Equal(
                [
                    new CellEntered(player.Id, new GridPosition(3, 2)),
                    new PlayerMoveStarted(player.Id, Direction.Right),
                ],
                events);
        }

        /// <summary>Verifies that held perpendicular input turns only after entering the destination cell.</summary>
        [Fact]
        public void HeldPerpendicularTurnsAtBoundary()
        {
            (Entity player, SimulationWorld simulation) = CreateScenario();
            StartRightToHalfCell(simulation);
            simulation.SetPlayerInput(Held(Direction.Up));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Equal(new GridPosition(3, 1), player.Position);
            Assert.NotNull(player.Motion);
            Assert.Equal(Direction.Up, player.Motion.SegmentDirection);
            Assert.Equal(Direction.Up, player.PlayerBody!.FacingDirection);
            Assert.Equal(
                [
                    new CellEntered(player.Id, new GridPosition(3, 2)),
                    new PlayerMoveStarted(player.Id, Direction.Up),
                ],
                events);
        }

        /// <summary>Verifies that a transient perpendicular press is not queued.</summary>
        [Fact]
        public void ReleasedPerpendicularInputHasNoBoundaryEffect()
        {
            (Entity player, SimulationWorld simulation) = CreateScenario();
            StartRightToHalfCell(simulation);
            simulation.SetPlayerInput(Held(Direction.Up));
            _ = simulation.Advance(TimeSpan.FromSeconds(0.1));
            simulation.SetPlayerInput(default);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.15));

            Assert.Equal(new GridPosition(3, 2), player.Position);
            Assert.Null(player.Motion);
            Assert.Equal(
                [
                    new CellEntered(player.Id, new GridPosition(3, 2)),
                    new PlayerStopped(player.Id),
                ],
                events);
        }

        /// <summary>Verifies that multi-key priority is reevaluated at the boundary.</summary>
        [Fact]
        public void MultiplePerpendicularInputsUsePriorityAtBoundary()
        {
            (Entity player, SimulationWorld simulation) = CreateScenario();
            StartRightToHalfCell(simulation);
            simulation.SetPlayerInput(new PlayerInputState(Left: false, Up: true, Right: false, Down: true));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Equal(new GridPosition(3, 1), player.Position);
            Assert.NotNull(player.Motion);
            Assert.Equal(Direction.Up, player.Motion.SegmentDirection);
            Assert.Equal(
                [
                    new CellEntered(player.Id, new GridPosition(3, 2)),
                    new PlayerMoveStarted(player.Id, Direction.Up),
                ],
                events);
        }

        /// <summary>Verifies that a newly blocked continuation emits a blocked event and stops.</summary>
        [Fact]
        public void BlockedNextSegmentStopsAfterCellEntry()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateWorldScenario();
            Entity blocker = new(
                EntityId.New(),
                new GridPosition(4, 2),
                InteractionMask.BlocksMovement);
            world.Add(blocker);
            StartRightToHalfCell(simulation);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Equal(new GridPosition(3, 2), player.Position);
            Assert.Null(player.Motion);
            Assert.Equal(
                [
                    new CellEntered(player.Id, new GridPosition(3, 2)),
                    new PlayerBlocked(player.Id, Direction.Right),
                ],
                events);
        }

        /// <summary>Verifies that a new same-cell occupant cannot undo an already-authoritative segment.</summary>
        [Fact]
        public void OccupantAddedToAuthoritativeDestinationDoesNotCancelSegment()
        {
            (WorldGrid world, Entity player, SimulationWorld simulation) = CreateWorldScenario();
            StartRightToHalfCell(simulation);
            Entity pushable = new(
                EntityId.New(),
                new GridPosition(3, 2),
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable);
            world.Add(pushable);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(0.25));

            Assert.Equal(new GridPosition(4, 2), player.Position);
            Assert.NotNull(player.Motion);
            Assert.Equal(new GridPosition(3, 2), pushable.Position);
            Assert.Null(pushable.Motion);
            Assert.Equal(
                [
                    new CellEntered(player.Id, new GridPosition(3, 2)),
                    new PlayerMoveStarted(player.Id, Direction.Right),
                ],
                events);
        }

        private static void StartRightToHalfCell(SimulationWorld simulation)
        {
            simulation.SetPlayerInput(Held(Direction.Right));
            _ = simulation.Advance(TimeSpan.FromSeconds(0.25));
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

        private static (Entity Player, SimulationWorld Simulation) CreateScenario()
        {
            (WorldGrid _, Entity player, SimulationWorld simulation) = CreateWorldScenario();
            return (player, simulation);
        }

        private static (WorldGrid World, Entity Player, SimulationWorld Simulation) CreateWorldScenario()
        {
            WorldGrid world = new(6, 6);
            Entity player = new(
                EntityId.New(),
                new GridPosition(2, 2),
                InteractionMask.None,
                playerBody: new PlayerBody(moveSpeed: 2, pushSpeedMultiplier: 1.5, Direction.Down));
            world.Add(player);
            SimulationWorld simulation = new(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy());
            return (world, player, simulation);
        }
    }
}
