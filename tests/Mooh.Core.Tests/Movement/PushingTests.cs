using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Movement;

using Xunit;

namespace Mooh.Core.Tests.Movement
{
    /// <summary>
    /// Verifies atomic adjacent-cell pushing behavior.
    /// </summary>
    public sealed class PushingTests
    {
        private const InteractionMask Pushable =
            InteractionMask.BlocksMovement | InteractionMask.PlayerPushable;

        /// <summary>Verifies that a successful push is planned farthest-first without mutation.</summary>
        [Fact]
        public void PushPlansBlockBeforeMoverWithoutMutation()
        {
            (WorldGrid World, MovementSystem Movement) context = CreateContext();
            Entity mover = Add(context.World, 2, InteractionMask.None);
            Entity block = Add(context.World, 3, Pushable);

            MovementPlan plan = context.Movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Pushed, plan.Result);
            Assert.Equal(
                [
                    new MovementStep(block.Id, new GridPosition(3, 1), new GridPosition(4, 1)),
                    new MovementStep(mover.Id, new GridPosition(2, 1), new GridPosition(3, 1)),
                ],
                plan.Steps);
            Assert.Equal(new GridPosition(2, 1), mover.Position);
            Assert.Equal(new GridPosition(3, 1), block.Position);
        }

        /// <summary>Verifies that an object in the cell beyond the adjacent push target blocks the push.</summary>
        [Fact]
        public void CrossCellPushChainIsBlocked()
        {
            (WorldGrid World, MovementSystem Movement) context = CreateContext();
            Entity mover = Add(context.World, 2, InteractionMask.None);
            Entity first = Add(context.World, 3, Pushable);
            Entity second = Add(context.World, 4, Pushable);

            MovementPlan plan = context.Movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, plan.Result);
            Assert.Empty(plan.Steps);
            Assert.Equal(new GridPosition(2, 1), mover.Position);
            Assert.Equal(new GridPosition(3, 1), first.Position);
            Assert.Equal(new GridPosition(4, 1), second.Position);
        }

        /// <summary>Verifies that multiple pushables sharing a cell move together.</summary>
        [Fact]
        public void TwoPushablesInSameCellMoveTogether()
        {
            (WorldGrid World, MovementSystem Movement) context = CreateContext();
            Entity mover = Add(context.World, 2, InteractionMask.None);
            Entity first = Add(context.World, 3, Pushable);
            Entity second = Add(context.World, 3, Pushable);

            MovementPlan plan = context.Movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Pushed, plan.Result);
            Assert.Equal(
                [
                    new MovementStep(second.Id, new GridPosition(3, 1), new GridPosition(4, 1)),
                    new MovementStep(first.Id, new GridPosition(3, 1), new GridPosition(4, 1)),
                    new MovementStep(mover.Id, new GridPosition(2, 1), new GridPosition(3, 1)),
                ],
                plan.Steps);
            Assert.Equal(new GridPosition(2, 1), mover.Position);
            Assert.Equal(new GridPosition(3, 1), first.Position);
            Assert.Equal(new GridPosition(3, 1), second.Position);
        }

        /// <summary>Verifies that a boundary-blocked push leaves every entity unchanged.</summary>
        [Fact]
        public void BoundaryBlockedPushRollsBackEveryEntity()
        {
            (WorldGrid World, MovementSystem Movement) context = CreateContext(width: 5);
            Entity mover = Add(context.World, 2, InteractionMask.None);
            Entity first = Add(context.World, 3, Pushable);
            Entity second = Add(context.World, 4, Pushable);

            MovementPlan plan = context.Movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, plan.Result);
            Assert.Empty(plan.Steps);
            Assert.Equal(new GridPosition(2, 1), mover.Position);
            Assert.Equal(new GridPosition(3, 1), first.Position);
            Assert.Equal(new GridPosition(4, 1), second.Position);
        }

        /// <summary>Verifies that a solid object behind a push chain leaves every entity unchanged.</summary>
        [Fact]
        public void SolidObjectBehindPushChainRollsBackEveryEntity()
        {
            (WorldGrid World, MovementSystem Movement) context = CreateContext();
            Entity mover = Add(context.World, 2, InteractionMask.None);
            Entity block = Add(context.World, 3, Pushable);
            Entity wall = Add(context.World, 4, InteractionMask.BlocksMovement);

            MovementPlan plan = context.Movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, plan.Result);
            Assert.Empty(plan.Steps);
            Assert.Equal(new GridPosition(2, 1), mover.Position);
            Assert.Equal(new GridPosition(3, 1), block.Position);
            Assert.Equal(new GridPosition(4, 1), wall.Position);
        }

        /// <summary>Verifies that a locked pushable blocks a push without mutating the world.</summary>
        [Fact]
        public void LockedPushableBlocksPushAndRollsBack()
        {
            (WorldGrid World, MovementSystem Movement) context = CreateContext();
            Entity mover = Add(context.World, 2, InteractionMask.None);
            Entity block = Add(context.World, 3, Pushable | InteractionMask.MovementLocked);

            MovementPlan plan = context.Movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Blocked, plan.Result);
            Assert.Empty(plan.Steps);
            Assert.Equal(new GridPosition(2, 1), mover.Position);
            Assert.Equal(new GridPosition(3, 1), block.Position);
        }

        /// <summary>
        /// Verifies a shadow occupant beyond the push target does not block the push. The recovered
        /// gate is <c>CellObjCheck(n, 0, 0, True)</c>, and a zero <c>ulMaskAny</c> makes the world
        /// list skip every occupant carrying <c>CNT_OrMaskShadow</c>, so a message table, a
        /// teleporter, fire or a deployed mine reads as an empty cell.
        /// </summary>
        [Fact]
        public void ShadowOccupantBeyondPushTargetDoesNotBlockPush()
        {
            (WorldGrid World, MovementSystem Movement) context = CreateContext();
            Entity mover = Add(context.World, 2, InteractionMask.None);
            Entity block = Add(context.World, 3, Pushable);
            Entity table = Add(context.World, 4, InteractionMask.OccupancyTransparent);

            MovementPlan plan = context.Movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Pushed, plan.Result);
            Assert.Equal(
                [
                    new MovementStep(block.Id, new GridPosition(3, 1), new GridPosition(4, 1)),
                    new MovementStep(mover.Id, new GridPosition(2, 1), new GridPosition(3, 1)),
                ],
                plan.Steps);
            Assert.Equal(new GridPosition(4, 1), table.Position);
        }

        /// <summary>
        /// Verifies a shadow occupant sharing the push target's cell does not turn the push into a
        /// blocked move either, because the same filter hides it from the destination scan.
        /// </summary>
        [Fact]
        public void ShadowOccupantSharingPushTargetDoesNotBlockPush()
        {
            (WorldGrid World, MovementSystem Movement) context = CreateContext();
            Entity mover = Add(context.World, 2, InteractionMask.None);
            Entity block = Add(context.World, 3, Pushable);
            _ = Add(context.World, 3, InteractionMask.OccupancyTransparent);

            MovementPlan plan = context.Movement.Plan(new MoveRequest(mover.Id, Direction.Right));

            Assert.Equal(MoveResult.Pushed, plan.Result);
            Assert.Equal(
                [
                    new MovementStep(block.Id, new GridPosition(3, 1), new GridPosition(4, 1)),
                    new MovementStep(mover.Id, new GridPosition(2, 1), new GridPosition(3, 1)),
                ],
                plan.Steps);
        }

        private static (WorldGrid World, MovementSystem Movement) CreateContext(int width = 10)
        {
            WorldGrid world = new(width, 3);
            return (world, new MovementSystem(world, new DefaultInteractionPolicy()));
        }

        private static Entity Add(WorldGrid world, int x, InteractionMask mask)
        {
            Entity entity = new(EntityId.New(), new GridPosition(x, 1), mask);
            world.Add(entity);
            return entity;
        }
    }
}
