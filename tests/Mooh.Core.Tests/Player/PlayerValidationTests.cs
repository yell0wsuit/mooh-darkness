using System;

using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Movement;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies validation at player, planning, and simulation boundaries.
    /// </summary>
    public sealed class PlayerValidationTests
    {
        private const InteractionMask Pushable =
            InteractionMask.BlocksMovement | InteractionMask.PlayerPushable;

        /// <summary>Verifies duplicate player registration remains rejected.</summary>
        [Fact]
        public void DuplicatePlayerRegistrationIsRejected()
        {
            WorldGrid world = new(5, 5);
            world.Add(NewPlayer(new GridPosition(1, 1)));

            _ = Assert.Throws<InvalidOperationException>(
                () => world.Add(NewPlayer(new GridPosition(2, 2))));
        }

        /// <summary>Verifies that an undefined initial facing direction is rejected.</summary>
        [Fact]
        public void UndefinedPlayerFacingDirectionIsRejected()
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerBody(1, 1.5, (Direction)int.MaxValue));
        }

        /// <summary>Verifies that player input requires a registered player.</summary>
        [Fact]
        public void InputWithoutPlayerIsRejected()
        {
            SimulationWorld simulation = CreateSimulation(new WorldGrid(5, 5));

            _ = Assert.Throws<InvalidOperationException>(
                () => simulation.SetPlayerInput(default));
        }

        /// <summary>Verifies that walking motion on a non-player entity is rejected immediately.</summary>
        [Fact]
        public void CorruptWalkingStateIsRejected()
        {
            WorldGrid world = new(5, 5);
            Entity entity = new(EntityId.New(), new GridPosition(2, 2), InteractionMask.None);
            world.Add(entity);
            KinematicsSystem.Start(entity, MotionCause.PlayerWalking, Direction.Right, speed: 1);
            SimulationWorld simulation = CreateSimulation(world);

            _ = Assert.Throws<InvalidOperationException>(
                () => simulation.Advance(TimeSpan.FromSeconds(0.1)));
        }

        /// <summary>Verifies that a movement plan cannot contain one entity more than once.</summary>
        [Fact]
        public void DuplicateMovementPlanMembersAreRejected()
        {
            EntityId id = EntityId.New();

            _ = Assert.Throws<InvalidOperationException>(
                () => new MovementPlan(
                    MoveResult.Pushed,
                    [
                        new MovementStep(id, new GridPosition(1, 1), new GridPosition(2, 1)),
                        new MovementStep(id, new GridPosition(2, 1), new GridPosition(3, 1)),
                    ]));
        }

        /// <summary>Verifies that gravity cannot change while the player is walking.</summary>
        [Fact]
        public void GravityChangeDuringPlayerWalkingIsRejected()
        {
            WorldGrid world = new(6, 5);
            Entity player = NewPlayer(new GridPosition(1, 1));
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(HeldRight);
            _ = simulation.Advance(TimeSpan.FromSeconds(0.1));

            _ = Assert.Throws<InvalidOperationException>(
                () => simulation.SetGravity(Direction.Left));
        }

        /// <summary>Verifies that gravity can change after both the player and one-cell push stop.</summary>
        [Fact]
        public void GravityChangeAfterOneCellPushCompletesIsAllowed()
        {
            WorldGrid world = new(8, 5);
            Entity player = NewPlayer(new GridPosition(1, 1));
            world.Add(player);
            Entity block = new(EntityId.New(), new GridPosition(2, 1), Pushable);
            world.Add(block);
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(HeldRight);
            _ = simulation.Advance(TimeSpan.FromSeconds(0.1));
            simulation.SetPlayerInput(default);
            _ = simulation.Advance(TimeSpan.FromSeconds(0.9));

            Assert.Null(player.Motion);
            Assert.Null(block.Motion);

            simulation.SetGravity(Direction.Left);
        }

        private static PlayerInputState HeldRight => new(
            Left: false,
            Up: false,
            Right: true,
            Down: false);

        private static Entity NewPlayer(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.None,
                playerBody: new PlayerBody(1, 1.5, Direction.Right));
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy());
        }
    }
}
