namespace Mooh.Core.Levels
{
    /// <summary>
    /// Tracks a colored key door's recovered opening. <c>KeyDoorDoor1</c>-<c>3</c> only enter
    /// <c>ST_Work</c>, thirteen frames at 35 ms, and drop <c>PlComeHere</c>; the door stays solid
    /// until <c>KeyDoorKill</c> removes it at the end of that animation.
    /// </summary>
    public sealed class KeyDoorBody
    {
        /// <summary>The recovered opening time, 13 frames at 35 ms.</summary>
        internal const double OpeningSeconds = 0.455;

        /// <summary>
        /// Gets the time left until the opening door is gone, or <see langword="null"/> while it
        /// is shut.
        /// </summary>
        public double? OpeningRemainingSeconds
        {
            get; internal set;
        }
    }
}
