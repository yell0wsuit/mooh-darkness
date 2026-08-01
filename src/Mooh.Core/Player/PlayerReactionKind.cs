namespace Mooh.Core.Player
{
    /// <summary>Identifies the player's current damage reaction.</summary>
    public enum PlayerReactionKind
    {
        /// <summary>The player can accept input and move normally.</summary>
        Normal,

        /// <summary>The player is temporarily shocked and uncontrollable.</summary>
        Shocked,

        /// <summary>The player is dead and permanently uncontrollable.</summary>
        Dead,
    }
}
