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
        /// The player is climbing into a tunnel, the recovered <c>ST_TonIn*</c>. The player does
        /// not move during it; transit starts when the animation ends.
        /// </summary>
        TunnelEntry,

        /// <summary>
        /// The player is climbing back out of a tunnel, the recovered <c>ST_TonOut*</c>. Unlike
        /// every other tunnel state it is not <c>bShadow</c>, so the player is solid again for
        /// its duration while still being immortal and holding the level end open.
        /// </summary>
        TunnelExit,

        /// <summary>
        /// The player is dissolving into a teleporter, the recovered <c>ST_TPortIn</c>. It runs
        /// twenty frames at 50 ms, and <c>TPortMe</c> makes the player a shadow for it.
        /// </summary>
        TeleportEntry,

        /// <summary>
        /// The player is in flight between two teleporters, the recovered <c>ST_TPortMove</c>.
        /// Its duration is not an animation length but the straight-line distance divided by
        /// <c>CNT_TPortSp</c>.
        /// </summary>
        TeleportFlight,

        /// <summary>
        /// The player is reappearing at the destination, the recovered <c>ST_TPortOut</c>. It is
        /// not <c>bShadow</c>, so entering it makes the player solid again.
        /// </summary>
        TeleportExit,
    }
}
