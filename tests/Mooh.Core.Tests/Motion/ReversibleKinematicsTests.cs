using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;

using Xunit;

namespace Mooh.Core.Tests.Motion
{
    /// <summary>
    /// Verifies reversible walking progress along a fixed cell segment.
    /// </summary>
    public sealed class ReversibleKinematicsTests
    {
        /// <summary>Verifies that walking can reverse and then resume along the same segment.</summary>
        [Fact]
        public void WalkingCanReverseAndResume()
        {
            Entity entity = NewEntity();
            KinematicsSystem.Start(entity, MotionCause.PlayerWalking, Direction.Right, speed: 2);

            KinematicsSystem.Advance(entity, TimeSpan.FromSeconds(0.25));
            Assert.Equal(0.5, entity.Motion!.Progress, precision: 12);

            KinematicsSystem.Reverse(entity);
            KinematicsSystem.Advance(entity, TimeSpan.FromSeconds(0.1));

            Assert.Equal(0.3, entity.Motion.Progress, precision: 12);
            Assert.Equal(-1, entity.Motion.TravelSign);
            Assert.Equal(Direction.Left, entity.Motion.TravelDirection);

            KinematicsSystem.Reverse(entity);
            KinematicsSystem.Advance(entity, TimeSpan.FromSeconds(0.1));

            Assert.Equal(0.5, entity.Motion.Progress, precision: 12);
            Assert.Equal(1, entity.Motion.TravelSign);
            Assert.Equal(Direction.Right, entity.Motion.TravelDirection);
        }

        /// <summary>Verifies that gravity-driven motion cannot be reversed.</summary>
        [Fact]
        public void GravityMotionCannotReverse()
        {
            Entity entity = NewEntity();
            KinematicsSystem.Start(entity, MotionCause.Falling, Direction.Down, speed: 2);

            _ = Assert.Throws<InvalidOperationException>(() => KinematicsSystem.Reverse(entity));

            Assert.Equal(1, entity.Motion!.TravelSign);
            Assert.Equal(Direction.Down, entity.Motion.TravelDirection);
        }

        /// <summary>Verifies exact advancement to both coordinator-selected segment boundaries.</summary>
        [Fact]
        public void WalkingAdvancesExactlyToSelectedBoundaries()
        {
            Entity entity = NewEntity();
            KinematicsSystem.Start(entity, MotionCause.PlayerWalking, Direction.Right, speed: 2);

            KinematicsSystem.Advance(entity, TimeSpan.FromSeconds(0.5));
            Assert.Equal(1, entity.Motion!.Progress, precision: 12);

            KinematicsSystem.Reverse(entity);
            KinematicsSystem.Advance(entity, TimeSpan.FromSeconds(0.5));
            Assert.Equal(0, entity.Motion.Progress, precision: 12);
        }

        private static Entity NewEntity()
        {
            return new Entity(EntityId.New(), new GridPosition(2, 2), InteractionMask.None);
        }
    }
}
