namespace Mooh.Core.Motion
{
    /// <summary>
    /// Identifies the gravity behavior responsible for active motion.
    /// </summary>
    public enum MotionCause
    {
        /// <summary>The entity is moving in the direction of gravity.</summary>
        Falling,

        /// <summary>The entity is moving laterally around support.</summary>
        Sliding,
    }
}
