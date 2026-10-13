namespace Mooh.Core.Enemies
{
    /// <summary>
    /// Tracks a bio mass's recovered animation-driven state. <c>ST_Stay</c> plays 20 frames at a
    /// per-entry random choice of 45 or 55 ms, the spawn states 10 frames at 50 ms and
    /// <c>ST_Grow</c> 20 frames at 50 ms.
    /// </summary>
    public sealed class BioMassBody
    {
        /// <summary>Gets the current state.</summary>
        public BioMassState State { get; internal set; }

        /// <summary>Gets the time left in the current state's animation.</summary>
        public double RemainingSeconds { get; internal set; }

        /// <summary>
        /// Gets the <c>ST_Stay</c> frame time chosen when the state was last entered. Re-entering
        /// the state it is already in changes nothing in the original, so the loop keeps it.
        /// </summary>
        public double StayFrameSeconds { get; internal set; }

        /// <summary>Gets whether the opening state has been given its random speed and phase.</summary>
        internal bool IsStarted { get; set; }
    }
}
