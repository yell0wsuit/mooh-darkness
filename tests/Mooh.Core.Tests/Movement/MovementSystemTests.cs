using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Movement;

using Xunit;

namespace Mooh.Core.Tests.Movement
{
    /// <summary>
    /// Verifies basic movement and destination interaction behavior.
    /// </summary>
    public sealed class MovementSystemTests
    {
        /// <summary>Verifies that planning an empty-cell move describes it without mutating the world.</summary>
        [Fact]
        public void PlanIntoEmptyCellDoesNotMutateWorld()
        {
            WorldGrid world = new(10, 10);
            Entity mover = Add(world, new GridPosition(2, 3), InteractionMask.None);
            MovementSystem movement = new(world, new DefaultInteractionPolicy());

            MovementPlan plan = movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Moved, plan.Result);
            Assert.Equal(
                [new MovementStep(mover.Id, new GridPosition(2, 3), new GridPosition(3, 3))],
                plan.Steps);
            Assert.Equal(new GridPosition(2, 3), mover.Position);
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

            MovementPlan plan = movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, plan.Result);
            Assert.Empty(plan.Steps);
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

            MovementPlan plan = movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Moved, plan.Result);
            Assert.Equal(
                [new MovementStep(mover.Id, new GridPosition(2, 3), new GridPosition(3, 3))],
                plan.Steps);
            Assert.Equal(new GridPosition(2, 3), mover.Position);
            Assert.Equal([mover], world.GetOccupants(new GridPosition(2, 3)));
            Assert.Equal([decoration], world.GetOccupants(new GridPosition(3, 3)));
        }

        /// <summary>Verifies that the world boundary blocks movement.</summary>
        [Fact]
        public void WorldBoundaryBlocksMovement()
        {
            WorldGrid world = new(10, 10);
            Entity mover = Add(world, new GridPosition(9, 3), InteractionMask.None);
            MovementSystem movement = new(world, new DefaultInteractionPolicy());

            MovementPlan plan = movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, plan.Result);
            Assert.Empty(plan.Steps);
            Assert.Equal(new GridPosition(9, 3), mover.Position);
        }

        /// <summary>Verifies that a movement-locked entity cannot initiate movement.</summary>
        [Fact]
        public void MovementLockedEntityCannotInitiateMovement()
        {
            WorldGrid world = new(10, 10);
            Entity mover = Add(world, new GridPosition(2, 3), InteractionMask.MovementLocked);
            MovementSystem movement = new(world, new DefaultInteractionPolicy());

            MovementPlan plan = movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, plan.Result);
            Assert.Empty(plan.Steps);
            Assert.Equal(new GridPosition(2, 3), mover.Position);
        }

        /// <summary>Verifies that active motion blocks planning for either the mover or a push target.</summary>
        /// <param name="movingMover">Whether the mover rather than the push target is moving.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MovingPlanMemberBlocksPlan(bool movingMover)
        {
            WorldGrid world = new(10, 10);
            Entity mover = Add(world, new GridPosition(2, 3), InteractionMask.None);
            Entity target = Add(
                world,
                new GridPosition(3, 3),
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable);
            KinematicsSystem.Start(movingMover ? mover : target, MotionCause.Falling, Direction.Down, speed: 1);
            MovementSystem movement = new(world, new DefaultInteractionPolicy());

            MovementPlan plan = movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, plan.Result);
            Assert.Empty(plan.Steps);
            Assert.Equal(new GridPosition(2, 3), mover.Position);
            Assert.Equal(new GridPosition(3, 3), target.Position);
        }

        private static Entity Add(WorldGrid world, GridPosition position, InteractionMask mask)
        {
            Entity entity = new(EntityId.New(), position, mask);
            world.Add(entity);
            return entity;
        }
    }
}
