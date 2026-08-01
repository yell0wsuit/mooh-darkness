using System;

using Mooh.Core.Grid;
using Mooh.Core.Player;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies player movement configuration.
    /// </summary>
    public sealed class PlayerBodyTests
    {
        /// <summary>Verifies that valid player configuration is retained.</summary>
        [Fact]
        public void ConstructorRetainsConfiguration()
        {
            PlayerBody body = new(moveSpeed: 2, pushSpeedMultiplier: 1.5, Direction.Down);

            Assert.Equal(2, body.MoveSpeed);
            Assert.Equal(1.5, body.PushSpeedMultiplier);
            Assert.Equal(Direction.Down, body.FacingDirection);
        }

        /// <summary>Verifies that movement speed must be finite and positive.</summary>
        /// <param name="moveSpeed">The invalid movement speed.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void ConstructorRejectsInvalidMoveSpeed(double moveSpeed)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerBody(moveSpeed, pushSpeedMultiplier: 1.5, Direction.Down));
        }

        /// <summary>Verifies that the push multiplier must be finite and positive.</summary>
        /// <param name="pushSpeedMultiplier">The invalid push speed multiplier.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void ConstructorRejectsInvalidPushSpeedMultiplier(double pushSpeedMultiplier)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerBody(moveSpeed: 2, pushSpeedMultiplier, Direction.Down));
        }
    }
}
