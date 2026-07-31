using System;

using Mooh.Core.Grid;

using Xunit;

namespace Mooh.Core.Tests.Grid
{
    /// <summary>
    /// Verifies world bounds and dimension validation.
    /// </summary>
    public sealed class WorldGridTests
    {
        /// <summary>Verifies whether representative coordinates lie within the world.</summary>
        /// <param name="x">The horizontal coordinate.</param>
        /// <param name="y">The vertical coordinate.</param>
        /// <param name="expected">The expected containment result.</param>
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

        /// <summary>Verifies that a world rejects non-positive dimensions.</summary>
        /// <param name="width">The candidate width.</param>
        /// <param name="height">The candidate height.</param>
        [Theory]
        [InlineData(0, 10)]
        [InlineData(10, 0)]
        public void ConstructorRejectsNonPositiveDimensions(int width, int height)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => new WorldGrid(width, height));
        }
    }
}
