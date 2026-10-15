using Mooh.Core.Grid;

using Xunit;

namespace Mooh.Core.Tests.Grid
{
    /// <summary>
    /// Verifies the recovered quarter-turn coordinate transform. Left and right turns rewrite
    /// every object's cell before the world's dimensions are swapped, so both mappings use
    /// the pre-rotation width and height.
    /// </summary>
    public sealed class WorldRotationTests
    {
        /// <summary>
        /// Verifies the recovered left mapping <c>newX = height - 1 - y</c>, <c>newY = x</c>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 4, 0)]
        [InlineData(6, 0, 4, 6)]
        [InlineData(0, 4, 0, 0)]
        [InlineData(6, 4, 0, 6)]
        [InlineData(2, 1, 3, 2)]
        public void ALeftTurnSendsTheTopEdgeToTheRightEdge(int x, int y, int expectedX, int expectedY)
        {
            GridPosition rotated = WorldRotation.Map(
                new GridPosition(x, y),
                WorldRotationDirection.Left,
                width: 7,
                height: 5);

            Assert.Equal(new GridPosition(expectedX, expectedY), rotated);
        }

        /// <summary>
        /// Verifies the recovered right mapping <c>newX = y</c>, <c>newY = width - 1 - x</c>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 6)]
        [InlineData(6, 0, 0, 0)]
        [InlineData(0, 4, 4, 6)]
        [InlineData(6, 4, 4, 0)]
        [InlineData(2, 1, 1, 4)]
        public void ARightTurnSendsTheTopEdgeToTheLeftEdge(int x, int y, int expectedX, int expectedY)
        {
            GridPosition rotated = WorldRotation.Map(
                new GridPosition(x, y),
                WorldRotationDirection.Right,
                width: 7,
                height: 5);

            Assert.Equal(new GridPosition(expectedX, expectedY), rotated);
        }

        /// <summary>
        /// Verifies the two directions are exact inverses of one another, so a level that spins one
        /// way and back is restored cell for cell. The second turn reads the swapped dimensions.
        /// </summary>
        [Fact]
        public void TurningLeftThenRightRestoresEveryCell()
        {
            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    GridPosition original = new(x, y);
                    GridPosition left = WorldRotation.Map(
                        original,
                        WorldRotationDirection.Left,
                        width: 7,
                        height: 5);
                    GridPosition restored = WorldRotation.Map(
                        left,
                        WorldRotationDirection.Right,
                        width: 5,
                        height: 7);

                    Assert.Equal(original, restored);
                }
            }
        }

        /// <summary>
        /// Verifies a rotated cell always lands inside the swapped bounds, which is why the
        /// original never has to clamp: a world of any aspect ratio maps onto its transpose.
        /// </summary>
        [Theory]
        [InlineData(WorldRotationDirection.Left)]
        [InlineData(WorldRotationDirection.Right)]
        public void EveryRotatedCellLandsInsideTheSwappedBounds(WorldRotationDirection direction)
        {
            for (int x = 0; x < 40; x++)
            {
                for (int y = 0; y < 15; y++)
                {
                    GridPosition rotated = WorldRotation.Map(
                        new GridPosition(x, y),
                        direction,
                        width: 40,
                        height: 15);

                    Assert.InRange(rotated.X, 0, 14);
                    Assert.InRange(rotated.Y, 0, 39);
                }
            }
        }
    }
}
