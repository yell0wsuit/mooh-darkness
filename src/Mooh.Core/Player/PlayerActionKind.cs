namespace Mooh.Core.Player
{
    /// <summary>
    /// Identifies the uncontrollable action state the player is currently in. The recovered
    /// <c>GamePlayer.DoCtrl</c> fire branch reaches exactly two of them: the weapon states
    /// (<c>ST_Bomb*</c>) and the two-phase hand strike (<c>ST_StrIn*</c> then <c>ST_StrOut*</c>).
    /// </summary>
    public enum PlayerActionKind
    {
        /// <summary>The player is winding up a hand strike and has not applied it yet.</summary>
        StrikeWindup,

        /// <summary>The player has applied a hand strike and is recovering from it.</summary>
        StrikeRecovery,

        /// <summary>The player is placing a grenade in the faced cell.</summary>
        GrenadeThrow,
    }
}
