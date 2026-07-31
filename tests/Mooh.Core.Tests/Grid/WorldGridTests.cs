using System;

using Mooh.Core.Grid;

using Xunit;

namespace Mooh.Core.Tests.Grid
{
    public sealed class WorldGridTests
    {
        [Theory]
        [InlineData(0, 0, true)]
        [InlineData(9, 9, true)]
        [InlineData(-1, 0, false)]
        [InlineData(10, 9, false)]
        public void ContainsRecognizesBounds(int x, int y, bool expected)
        {
            WorldGrid world = new(10, 10);

            Assert.Equal(expected, world.Contains(new GridPosition(x, y)));
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(10, 0)]
        public void ConstructorRejectsNonPositiveDimensions(int width, int height)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => new WorldGrid(width, height));
        }
    }
}
