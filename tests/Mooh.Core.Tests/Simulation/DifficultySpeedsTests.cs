using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>Verifies the movement speeds recovered from the original difficulty table.</summary>
    public sealed class DifficultySpeedsTests
    {
        /// <summary>Verifies the shared speed values used by standard difficulties.</summary>
        [Fact]
        public void StandardProfileUsesRecoveredCellsPerSecond()
        {
            DifficultySpeeds speeds = DifficultySpeeds.Standard;

            Assert.Equal(2.5, speeds.PlayerSpeed, precision: 12);
            Assert.Equal(1.875, speeds.NormalSpeed, precision: 12);
            Assert.Equal(1.5625, speeds.MonsterSpeed, precision: 12);
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
        }
    }
}
