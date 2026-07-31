using Mooh.Core.Grid;

using Xunit;

namespace Mooh.Core.Tests.Grid
{
    /// <summary>
    /// Verifies coordinate operations on <see cref="GridPosition"/>.
    /// </summary>
    public sealed class GridPositionTests
    {
        /// <summary>Verifies that each cardinal direction offsets the expected coordinate.</summary>
        /// <param name="direction">The direction to apply.</param>
        /// <param name="expectedX">The expected horizontal coordinate.</param>
        /// <param name="expectedY">The expected vertical coordinate.</param>
        [Theory]
        [InlineData(Direction.Left, 3, 4)]
        [InlineData(Direction.Up, 4, 3)]
        [InlineData(Direction.Right, 5, 4)]
        [InlineData(Direction.Down, 4, 5)]
        public void OffsetUsesCardinalDirection(Direction direction, int expectedX, int expectedY)
        {
            GridPosition result = new GridPosition(4, 4).Offset(direction);

            Assert.Equal(new GridPosition(expectedX, expectedY), result);
        }
    }
}
