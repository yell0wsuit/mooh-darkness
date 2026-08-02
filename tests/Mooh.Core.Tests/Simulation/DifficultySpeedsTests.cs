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
    }
}
