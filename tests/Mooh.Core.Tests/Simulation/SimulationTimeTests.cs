using System;

using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>Verifies elapsed seconds convert to tick timers without losing a tick.</summary>
    public sealed class SimulationTimeTests
    {
        /// <summary>
        /// Verifies a substep sized from a timer's own seconds converts back to that timer exactly,
        /// where <see cref="TimeSpan.FromSeconds(double)"/> would truncate a tick away.
        /// </summary>
        [Theory]
        [InlineData(10_002)]
        [InlineData(3)]
        [InlineData(299_999_997)]
        public void ATimersOwnSecondsConvertBackToItsTicks(long ticks)
        {
            TimeSpan timer = TimeSpan.FromTicks(ticks);

            Assert.Equal(timer, SimulationTime.Elapsed(timer.TotalSeconds));
        }
    }
}
