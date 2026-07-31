using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Movement;

using Xunit;

namespace Mooh.Core.Tests.Movement
{
    /// <summary>
    /// Verifies basic movement and destination interaction behavior.
    /// </summary>
    public sealed class MovementSystemTests
    {
        /// <summary>Verifies that moving into an empty cell updates position and occupancy.</summary>
        [Fact]
        public void MoveIntoEmptyCellUpdatesPositionAndOccupancy()
        {
            WorldGrid world = new(10, 10);
            Entity mover = Add(world, new GridPosition(2, 3), InteractionMask.None);
            MovementSystem movement = new(world, new DefaultInteractionPolicy());

            MoveResult result = movement.Move(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Moved, result);
            Assert.Equal(new GridPosition(3, 3), mover.Position);
            Assert.Empty(world.GetOccupants(new GridPosition(2, 3)));
            Assert.Equal([mover], world.GetOccupants(new GridPosition(3, 3)));
        }

        /// <summary>Verifies that a blocking occupant leaves the world unchanged.</summary>
        /// <param name="blockerMask">The interaction mask assigned to the blocker.</param>
        [Theory]
        [InlineData(InteractionMask.BlocksMovement)]
        [InlineData(InteractionMask.MovementLocked)]
        public void BlockingOccupantLeavesWorldUnchanged(InteractionMask blockerMask)
        {
            WorldGrid world = new(10, 10);
            Entity mover = Add(world, new GridPosition(2, 3), InteractionMask.None);
            Entity blocker = Add(world, new GridPosition(3, 3), blockerMask);
            MovementSystem movement = new(world, new DefaultInteractionPolicy());

            MoveResult result = movement.Move(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, result);
            Assert.Equal(new GridPosition(2, 3), mover.Position);
            Assert.Equal([blocker], world.GetOccupants(new GridPosition(3, 3)));
        }

        /// <summary>Verifies that non-blocking occupants can share a destination.</summary>
        [Fact]
        public void NonBlockingOccupantCanShareDestination()
        {
            WorldGrid world = new(10, 10);
            Entity mover = Add(world, new GridPosition(2, 3), InteractionMask.None);
            Entity decoration = Add(world, new GridPosition(3, 3), InteractionMask.PlayerEdible);
            MovementSystem movement = new(world, new DefaultInteractionPolicy());

            MoveResult result = movement.Move(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Moved, result);
            Assert.Equal([decoration, mover], world.GetOccupants(new GridPosition(3, 3)));
        }

        /// <summary>Verifies that the world boundary blocks movement.</summary>
        [Fact]
        public void WorldBoundaryBlocksMovement()
        {
            WorldGrid world = new(10, 10);
            Entity mover = Add(world, new GridPosition(9, 3), InteractionMask.None);
            MovementSystem movement = new(world, new DefaultInteractionPolicy());

            MoveResult result = movement.Move(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, result);
            Assert.Equal(new GridPosition(9, 3), mover.Position);
        }

        /// <summary>Verifies that a movement-locked entity cannot initiate movement.</summary>
        [Fact]
        public void MovementLockedEntityCannotInitiateMovement()
        {
            WorldGrid world = new(10, 10);
            Entity mover = Add(world, new GridPosition(2, 3), InteractionMask.MovementLocked);
            MovementSystem movement = new(world, new DefaultInteractionPolicy());

            MoveResult result = movement.Move(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, result);
            Assert.Equal(new GridPosition(2, 3), mover.Position);
        }

        private static Entity Add(WorldGrid world, GridPosition position, InteractionMask mask)
        {
            Entity entity = new(EntityId.New(), position, mask);
            world.Add(entity);
            return entity;
        }
    }
}
