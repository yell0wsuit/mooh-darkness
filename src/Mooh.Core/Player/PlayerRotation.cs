using Mooh.Core.Grid;

namespace Mooh.Core.Player
{
    /// <summary>Tracks an uncontrollable timed player-facing transition.</summary>
    public sealed class PlayerRotation
    {
        internal PlayerRotation(
            Direction from,
            Direction to,
            double remainingSeconds,
            double? suspendedProgress)
        {
            From = from;
            To = to;
            RemainingSeconds = remainingSeconds;
            SuspendedProgress = suspendedProgress;
        }

        /// <summary>Gets the facing direction before the rotation.</summary>
        public Direction From { get; }

        /// <summary>Gets the facing direction after the rotation.</summary>
        public Direction To { get; }

        /// <summary>Gets the remaining rotation duration in seconds.</summary>
        public double RemainingSeconds { get; internal set; }

        /// <summary>Gets preserved progress from a suspended mid-cell reversal, if any.</summary>
        public double? SuspendedProgress { get; }
    }
}
