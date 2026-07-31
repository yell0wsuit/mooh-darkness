using System;

using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

using Xunit;

namespace Mooh.Core.Tests.Gravity
{
    /// <summary>
    /// Verifies gravity component validation and entity composition.
    /// </summary>
    public sealed class GravityBodyTests
    {
        /// <summary>Verifies that gravity requires a finite positive fall speed.</summary>
        /// <param name="speed">The invalid fall speed.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void GravityBodyRejectsInvalidFallSpeed(double speed)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => new GravityBody(speed, canSlide: false));
        }

        /// <summary>Verifies that gravity capability does not imply active motion.</summary>
        [Fact]
        public void EntityMayOwnGravityBodyWithoutStartingInMotion()
        {
            GravityBody body = new(2.5, canSlide: true);
            Entity entity = new(EntityId.New(), new GridPosition(1, 1), InteractionMask.None, body);

            Assert.Same(body, entity.GravityBody);
            Assert.Null(entity.Motion);
        }
    }
}
