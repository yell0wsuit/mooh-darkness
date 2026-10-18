using Mooh.Core.Grid;

namespace Mooh.Core.Player
{
    /// <summary>Tracks a player travelling through a tunnel network.</summary>
    /// <remarks>
    /// The recovered transit keeps two pieces of state on the player, <c>ulTonDir</c> and
    /// <c>bTonFlip</c>. Every tunnel state is <c>bImmort</c>, <c>bLockLvlEnd</c> and — except the
    /// exit animation — <c>bShadow</c>, so a travelling player takes no damage, holds the level
    /// end open, and is invisible to monsters and to ordinary cell queries alike.
    /// </remarks>
    /// <param name="direction">The direction the player is currently travelling.</param>
    public sealed class PlayerTunnelTransit(Direction direction)
    {
        /// <summary>Gets the direction the player is currently travelling, <c>ulTonDir</c>.</summary>
        public Direction Direction { get; internal set; } = direction;

        /// <summary>
        /// Gets whether the player has been turned back by a blocked exit, <c>bTonFlip</c>. Once
        /// set it stays set, and routing consults <c>DirMap</c> from then on so a one-way tunnel
        /// lets the player back out the way they came.
        /// </summary>
        public bool HasReversed
        {
            get; internal set;
        }
    }
}
