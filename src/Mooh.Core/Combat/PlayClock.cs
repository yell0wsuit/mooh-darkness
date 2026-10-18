using System;

namespace Mooh.Core.Combat
{
    /// <summary>
    /// Tracks the recovered timed-play clock, <c>Game.PlayTime</c>. On the timed profile it
    /// replaces health: <c>GameHealthDec</c> and <c>GameHealthAdd</c> move it by one second per
    /// health unit in either direction, and running out loses the level.
    /// </summary>
    /// <remarks>
    /// The clock is not clamped to its starting value, so healing can raise it above 400 s, and it
    /// may go below zero while the timeout settles.
    /// </remarks>
    public sealed class PlayClock
    {
        /// <summary>Gets the recovered <c>CNT_GPrmTimedTime</c> every timed level starts with.</summary>
        public static TimeSpan StartingTime { get; } = TimeSpan.FromSeconds(400);

        /// <summary>Gets the play time left.</summary>
        public TimeSpan Remaining { get; private set; } = StartingTime;

        /// <summary>Gets whether the clock has run out.</summary>
        public bool HasRunOut => Remaining <= TimeSpan.Zero;

        /// <summary>Adds the seconds a heal of <paramref name="amount"/> health is worth.</summary>
        internal void Add(double amount)
        {
            Remaining += TimeSpan.FromSeconds(amount);
        }

        /// <summary>Spends the seconds a hit of <paramref name="amount"/> health is worth.</summary>
        internal void Subtract(double amount)
        {
            Remaining -= TimeSpan.FromSeconds(amount);
        }

        /// <summary>Counts the clock down by elapsed play time.</summary>
        internal void Advance(TimeSpan elapsed)
        {
            Remaining -= elapsed;
        }
    }
}
