using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

using Xunit;

namespace Mooh.Core.Tests.Gravity
{
    /// <summary>
    /// Verifies default gravity occupancy rules.
    /// </summary>
    public sealed class GravityCollisionPolicyTests
    {
        private readonly DefaultGravityCollisionPolicy _policy = new();

        /// <summary>Verifies that gravity cannot enter cells outside the world.</summary>
        [Fact]
        public void OutsideWorldIsBlocked()
        {
            WorldGrid world = new(3, 3);
            Entity mover = AddEntity(world, new GridPosition(1, 0), InteractionMask.None);

            Assert.False(_policy.CanEnter(world, mover, new GridPosition(1, -1)));
        }

        /// <summary>Verifies that gravity can enter an empty cell.</summary>
        [Fact]
        public void EmptyCellIsOpen()
        {
            WorldGrid world = new(3, 3);
            Entity mover = AddEntity(world, new GridPosition(1, 0), InteractionMask.None);

            Assert.True(_policy.CanEnter(world, mover, new GridPosition(1, 1)));
        }

        /// <summary>Verifies that occupants without obstruction semantics do not block gravity.</summary>
        [Fact]
        public void NoneOccupantsAreOpen()
        {
            WorldGrid world = new(3, 3);
            Entity mover = AddEntity(world, new GridPosition(1, 0), InteractionMask.None);
            _ = AddEntity(world, new GridPosition(1, 1), InteractionMask.None);
            _ = AddEntity(world, new GridPosition(1, 1), InteractionMask.None);

            Assert.True(_policy.CanEnter(world, mover, new GridPosition(1, 1)));
        }

        /// <summary>Verifies each semantic obstruction that blocks gravity.</summary>
        /// <param name="mask">The blocking interaction capability.</param>
        [Theory]
        [InlineData(InteractionMask.BlocksMovement)]
        [InlineData(InteractionMask.MovementLocked)]
        [InlineData(InteractionMask.PlayerEdible)]
        [InlineData(InteractionMask.CanBePushedThrough)]
        public void BlockingOccupantPreventsEntry(InteractionMask mask)
        {
            WorldGrid world = new(3, 3);
            Entity mover = AddEntity(world, new GridPosition(1, 0), InteractionMask.None);
            _ = AddEntity(world, new GridPosition(1, 1), InteractionMask.None);
            _ = AddEntity(world, new GridPosition(1, 1), mask);

            Assert.False(_policy.CanEnter(world, mover, new GridPosition(1, 1)));
        }

        private static Entity AddEntity(WorldGrid world, GridPosition position, InteractionMask mask)
        {
            Entity entity = new(EntityId.New(), position, mask);
            world.Add(entity);
            return entity;
        }
    }
}
