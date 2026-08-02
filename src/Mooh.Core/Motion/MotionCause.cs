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
        /// An object is sliding away from a player hand strike. The recovered
        /// <c>CPlPushBase.PlPush_OnCell</c> re-registers itself as the arrival handler, so a
        /// struck object continues cell after cell until it is blocked or starts to fall.
        /// </summary>
        StrikePushed,

        /// <summary>A monster is walking under autonomous control.</summary>
        MonsterWalking,
    }
}
