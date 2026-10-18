using System;

namespace Mooh.Core.Simulation
{
    /// <summary>Converts the simulation's elapsed seconds into the tick-based timers some systems keep.</summary>
    public static class SimulationTime
    {
        /// <summary>
        /// Converts elapsed seconds to the nearest whole tick. <see cref="TimeSpan.FromSeconds(double)"/>
        /// truncates, so a substep sized to end a <see cref="TimeSpan"/> timer exactly would come
        /// back one tick short, and the shortfall would build up over many substeps until a timer
        /// missed its deadline.
        /// </summary>
        /// <param name="seconds">The elapsed seconds.</param>
        /// <returns>The elapsed time, rounded to the nearest tick.</returns>
        public static TimeSpan Elapsed(double seconds)
        {
            return TimeSpan.FromTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));
        }
    }
}
