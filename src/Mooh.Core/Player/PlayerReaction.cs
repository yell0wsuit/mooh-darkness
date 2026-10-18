using System;

namespace Mooh.Core.Player
{
    /// <summary>Exposes the player's current logical damage reaction.</summary>
    public sealed class PlayerReaction
    {
        /// <summary>Gets the current reaction kind.</summary>
        public PlayerReactionKind Kind
        {
            get; internal set;
        }

        /// <summary>Gets the remaining duration for a temporary reaction.</summary>
        public TimeSpan Remaining
        {
            get; internal set;
        }
    }
}
