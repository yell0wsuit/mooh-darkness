using System;

using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>Verifies the gameplay values recovered from the original difficulty table.</summary>
    public sealed class DifficultySpeedsTests
    {
        /// <summary>Verifies the shared speed values used by standard difficulties.</summary>
        [Fact]
        public void StandardProfileUsesRecoveredCellsPerSecond()
        {
            DifficultySpeeds profile = DifficultySpeeds.Standard;

            Assert.Equal(2.5, profile.PlayerSpeed, precision: 12);
            Assert.Equal(1.875, profile.NormalSpeed, precision: 12);
            Assert.Equal(1.5625, profile.MonsterSpeed, precision: 12);
            Assert.Equal(20, profile.ShakeDamage);
            Assert.Equal(30, profile.HardShakeDamage);
            Assert.Equal(20, profile.ExplosionDamage);
            Assert.Equal(40, profile.FoodHeal);
        }

        /// <summary>Verifies that difficulty zero scales every recovered speed by 0.75.</summary>
        [Fact]
        public void DifficultyZeroScalesEverySpeedByThreeQuarters()
        {
            DifficultySpeeds standard = DifficultySpeeds.Standard;
            DifficultySpeeds easy = DifficultySpeeds.DifficultyZero;

            Assert.Equal(standard.PlayerSpeed * 0.75, easy.PlayerSpeed, precision: 12);
            Assert.Equal(standard.NormalSpeed * 0.75, easy.NormalSpeed, precision: 12);
            Assert.Equal(standard.MonsterSpeed * 0.75, easy.MonsterSpeed, precision: 12);
            Assert.Equal(10, easy.ShakeDamage);
            Assert.Equal(15, easy.HardShakeDamage);
            Assert.Equal(10, easy.ExplosionDamage);
            Assert.Equal(40, easy.FoodHeal);
        }

        /// <summary>Verifies timed mode retains standard impact damage but the reduced eat value.</summary>
        [Fact]
        public void TimedProfileUsesRecoveredImpactDamage()
        {
            Assert.Equal(20, DifficultySpeeds.Timed.ShakeDamage);
            Assert.Equal(30, DifficultySpeeds.Timed.HardShakeDamage);
            Assert.Equal(20, DifficultySpeeds.Timed.ExplosionDamage);
            Assert.Equal(15, DifficultySpeeds.Timed.FoodHeal);
        }

        /// <summary>
        /// Verifies the recovered <c>CNT_GPrmHealthSp</c> drains a full bar in fifteen minutes on
        /// both untimed profiles, and that timed play, which replaces health with the clock, does
        /// not drain at all.
        /// </summary>
        [Fact]
        public void HealthDrainFollowsTheRecoveredRateOutsideTimedPlay()
        {
            Assert.Equal(100.0 / 900, DifficultySpeeds.Standard.HealthDrainPerSecond, precision: 12);
            Assert.Equal(100.0 / 900, DifficultySpeeds.DifficultyZero.HealthDrainPerSecond, precision: 12);
            Assert.Equal(0, DifficultySpeeds.Timed.HealthDrainPerSecond);
            Assert.Equal(0, (DifficultySpeeds.Standard with { HealthDrainPerSecond = 0 }).HealthDrainPerSecond);
        }

        /// <summary>Verifies a drain rate must be finite and non-negative.</summary>
        /// <param name="rate">The candidate rate.</param>
        [Theory]
        [InlineData(-0.1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void HealthDrainRejectsInvalidRate(double rate)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => DifficultySpeeds.Standard with { HealthDrainPerSecond = rate });
        }
    }
}
