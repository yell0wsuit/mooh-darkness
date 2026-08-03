namespace Mooh.Core.Player
{
    /// <summary>
    /// Identifies the uncontrollable action state the player is currently in. The recovered
    /// <c>GamePlayer.DoCtrl</c> fire branch reaches exactly two of them: the weapon states
    /// (<c>ST_Bomb*</c>) and the two-phase hand strike (<c>ST_StrIn*</c> then <c>ST_StrOut*</c>).
    /// The two tunnel animations are reached from <c>CtrlMove</c> instead, through the
    /// <c>PlComeHere</c> gate a tunnel answers.
    /// </summary>
    public enum PlayerActionKind
    {
        /// <summary>The player is winding up a hand strike and has not applied it yet.</summary>
        StrikeWindup,

        /// <summary>The player has applied a hand strike and is recovering from it.</summary>
        StrikeRecovery,

        /// <summary>The player is placing a grenade in the faced cell.</summary>
        GrenadeThrow,

        /// <summary>
        /// The player is climbing into a tunnel. The player does
        /// not move during it; transit starts when the animation ends.
        /// </summary>
        TunnelEntry,

        /// <summary>
        /// The player is climbing back out of a tunnel. Unlike
        /// every other tunnel state it is not <c>bShadow</c>, so the player is solid again for
        /// its duration while still being immortal and holding the level end open.
        /// </summary>
        TunnelExit,
    }
}
