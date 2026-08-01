using System;

using Mooh.Core.Grid;
using Mooh.Core.Motion;

using Xunit;

namespace Mooh.Core.Tests.Motion
{
    /// <summary>
    /// Verifies active motion state validation and initialization.
    /// </summary>
    public sealed class MotionStateTests
    {
        /// <summary>Verifies that motion requires a finite positive speed.</summary>
        /// <param name="speed">The invalid motion speed.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void MotionStateRejectsInvalidSpeed(double speed)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new MotionState(MotionCause.Falling, Direction.Down, speed));
        }

        /// <summary>Verifies that valid motion starts at the beginning of a cell transit.</summary>
        [Fact]
        public void MotionStateExposesInitialState()
        {
            MotionState motion = new(MotionCause.Sliding, Direction.Right, 2.5);

            Assert.Equal(MotionCause.Sliding, motion.Cause);
            Assert.Equal(Direction.Right, motion.SegmentDirection);
            Assert.Equal(2.5, motion.Speed);
            Assert.Equal(0, motion.Progress);
        }

        /// <summary>Verifies the direction and ordering metadata for a walking segment.</summary>
        [Fact]
        public void WalkingMotionExposesSegmentState()
        {
            MotionState motion = new(
                MotionCause.PlayerWalking,
                Direction.Right,
                2,
                resolutionOrder: 3);

            Assert.Equal(Direction.Right, motion.SegmentDirection);
            Assert.Equal(1, motion.TravelSign);
            Assert.Equal(Direction.Right, motion.TravelDirection);
            Assert.Equal(3, motion.ResolutionOrder);
        }
    }
}
