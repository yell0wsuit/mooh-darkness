using Mooh.Core.Simulation;

namespace Mooh.Core.Tests
{
    /// <summary>
    /// Supplies recovered difficulty profiles with the passive health drain switched off, for tests
    /// that assert exact health values while exercising some other mechanic.
    /// </summary>
    internal static class Undrained
    {
        /// <summary>Gets the standard profile without the passive drain.</summary>
        public static DifficultySpeeds Standard { get; } = Of(DifficultySpeeds.Standard);

        /// <summary>Returns a profile identical to <paramref name="profile"/> but without the drain.</summary>
        /// <param name="profile">The recovered profile.</param>
        /// <returns>The undrained profile.</returns>
        public static DifficultySpeeds Of(DifficultySpeeds profile)
        {
            return profile with
            {
                HealthDrainPerSecond = 0
            };
        }
    }
}
