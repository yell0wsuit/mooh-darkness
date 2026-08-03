namespace Mooh.Core.Motion
{
    /// <summary>
    /// Identifies the behavior responsible for active motion.
    /// </summary>
    public enum MotionCause
    {
        /// <summary>The entity is moving in the direction of gravity.</summary>
        Falling,

        /// <summary>The entity is moving laterally around support.</summary>
        Sliding,

        /// <summary>The player is walking under held-input control.</summary>
        PlayerWalking,

        /// <summary>An object is moving after being pushed by the player.</summary>
        PlayerPushed,

        /// <summary>
        /// An object is sliding away from a player hand strike.
        /// </summary>
        StrikePushed,

        /// <summary>A monster is walking under autonomous control.</summary>
        MonsterWalking,

        /// <summary>
        /// The player is being carried along a tunnel.
        /// </summary>
        TunnelTransit,
    }
}
