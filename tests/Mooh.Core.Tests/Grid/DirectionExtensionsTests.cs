using Mooh.Core.Grid;

using Xunit;

namespace Mooh.Core.Tests.Grid
{
    /// <summary>
    /// Verifies cardinal direction geometry relative to gravity.
    /// </summary>
    public sealed class DirectionExtensionsTests
    {
        /// <summary>Verifies that opposite directions reverse each cardinal direction.</summary>
        /// <param name="direction">The direction to reverse.</param>
        /// <param name="expected">The expected reverse direction.</param>
        [Theory]
        [InlineData(Direction.Left, Direction.Right)]
        [InlineData(Direction.Up, Direction.Down)]
        [InlineData(Direction.Right, Direction.Left)]
        [InlineData(Direction.Down, Direction.Up)]
        public void OppositeReturnsReverseDirection(Direction direction, Direction expected)
        {
            Assert.Equal(expected, direction.Opposite());
        }

        /// <summary>Verifies counter-clockwise turns from each cardinal direction.</summary>
        /// <param name="direction">The starting direction.</param>
        /// <param name="expected">The expected turned direction.</param>
        [Theory]
        [InlineData(Direction.Left, Direction.Down)]
        [InlineData(Direction.Up, Direction.Left)]
        [InlineData(Direction.Right, Direction.Up)]
        [InlineData(Direction.Down, Direction.Right)]
        public void TurnLeftReturnsCounterClockwiseDirection(Direction direction, Direction expected)
        {
            Assert.Equal(expected, direction.TurnLeft());
        }

        /// <summary>Verifies clockwise turns from each cardinal direction.</summary>
        /// <param name="direction">The starting direction.</param>
        /// <param name="expected">The expected turned direction.</param>
        [Theory]
        [InlineData(Direction.Left, Direction.Up)]
        [InlineData(Direction.Up, Direction.Right)]
        [InlineData(Direction.Right, Direction.Down)]
        [InlineData(Direction.Down, Direction.Left)]
        public void TurnRightReturnsClockwiseDirection(Direction direction, Direction expected)
        {
            Assert.Equal(expected, direction.TurnRight());
        }

        /// <summary>Verifies the two lateral directions relative to downward gravity.</summary>
        [Fact]
        public void DownRelativeSidesAreRightThenLeft()
        {
            Assert.Equal(Direction.Right, Direction.Down.TurnLeft());
            Assert.Equal(Direction.Left, Direction.Down.TurnRight());
        }
    }
}
