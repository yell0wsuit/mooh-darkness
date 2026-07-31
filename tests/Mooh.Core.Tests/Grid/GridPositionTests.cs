using Mooh.Core.Grid;

using Xunit;

namespace Mooh.Core.Tests.Grid
{
    public sealed class GridPositionTests
    {
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
