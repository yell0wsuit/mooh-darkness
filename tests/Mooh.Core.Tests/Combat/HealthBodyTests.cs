using System;

using Mooh.Core.Combat;

using Xunit;

namespace Mooh.Core.Tests.Combat
{
    /// <summary>Verifies player-health configuration and invariants.</summary>
    public sealed class HealthBodyTests
    {
        /// <summary>Verifies valid health configuration is exposed unchanged.</summary>
        [Fact]
        public void StoresValidConfiguration()
        {
            HealthBody body = new(100, 75, TimeSpan.FromMilliseconds(500));

            Assert.Equal(100, body.Maximum);
            Assert.Equal(75, body.Current);
            Assert.Equal(TimeSpan.FromMilliseconds(500), body.ImmunityDuration);
            Assert.Equal(TimeSpan.Zero, body.ImmunityRemaining);
            Assert.False(body.IsDead);
        }

        /// <summary>Verifies maximum health must be finite and positive.</summary>
        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(0)]
        [InlineData(-1)]
        public void RejectsInvalidMaximum(double maximum)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new HealthBody(maximum, 1, TimeSpan.FromMilliseconds(500)));
        }

        /// <summary>Verifies initial health must be finite, positive, and no greater than maximum.</summary>
        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(101)]
        public void RejectsInvalidInitialHealth(double current)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new HealthBody(100, current, TimeSpan.FromMilliseconds(500)));
        }

        /// <summary>Verifies damage-immunity duration cannot be negative.</summary>
        [Fact]
        public void RejectsNegativeImmunityDuration()
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new HealthBody(100, 100, TimeSpan.FromTicks(-1)));
        }
    }
}
