using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Player
{
    /// <summary>
    /// Configures the continuous movement behavior of the world's player.
    /// </summary>
    public sealed class PlayerBody
    {
        private static readonly TimeSpan DefaultShockDuration = TimeSpan.FromMilliseconds(700);

        /// <summary>
        /// Initializes player movement configuration.
        /// </summary>
        /// <param name="moveSpeed">The player's walking speed in cells per second.</param>
        /// <param name="pushSpeedMultiplier">The multiplier applied to pushed-object speed.</param>
        /// <param name="facingDirection">The player's initial facing direction.</param>
        /// <param name="shockDuration">
        /// The optional duration of the player's nonlethal-damage reaction.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="moveSpeed"/> or <paramref name="pushSpeedMultiplier"/> is not finite and positive,
        /// <paramref name="facingDirection"/> is undefined, or <paramref name="shockDuration"/> is not positive.
        /// </exception>
        public PlayerBody(
            double moveSpeed,
            double pushSpeedMultiplier,
            Direction facingDirection,
            TimeSpan? shockDuration = null)
        {
            if (!double.IsFinite(moveSpeed) || moveSpeed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed));
            }

            if (!double.IsFinite(pushSpeedMultiplier) || pushSpeedMultiplier <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pushSpeedMultiplier));
            }

            if (!Enum.IsDefined(facingDirection))
            {
                throw new ArgumentOutOfRangeException(nameof(facingDirection), facingDirection, null);
            }

            TimeSpan resolvedShockDuration = shockDuration ?? DefaultShockDuration;
            if (resolvedShockDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(shockDuration));
            }

            MoveSpeed = moveSpeed;
            PushSpeedMultiplier = pushSpeedMultiplier;
            FacingDirection = facingDirection;
            ShockDuration = resolvedShockDuration;
        }

        /// <summary>Gets the player's walking speed in cells per second.</summary>
        public double MoveSpeed { get; }

        /// <summary>Gets the multiplier applied to pushed-object speed.</summary>
        public double PushSpeedMultiplier { get; }

        /// <summary>Gets the direction the player currently faces.</summary>
        public Direction FacingDirection { get; internal set; }

        /// <summary>Gets the duration of the uncontrollable nonlethal-damage reaction.</summary>
        public TimeSpan ShockDuration { get; }

        /// <summary>Gets the player's current damage reaction.</summary>
        public PlayerReaction Reaction { get; } = new();

        /// <summary>Gets the player's score, treasure, inventory, keys, and timed bonuses.</summary>
        public PlayerProgress Progress { get; } = new();

        /// <summary>Gets the active uncontrollable rotation, if any.</summary>
        public PlayerRotation? Rotation { get; internal set; }

        /// <summary>Gets the active uncontrollable fire-control action, if any.</summary>
        public PlayerAction? Action { get; internal set; }

        /// <summary>Gets the active tunnel transit, if the player is inside a tunnel.</summary>
        public PlayerTunnelTransit? TunnelTransit { get; internal set; }

        /// <summary>Gets the active teleport, if the player is being carried between teleporters.</summary>
        public PlayerTeleport? Teleport { get; internal set; }

        /// <summary>
        /// Gets whether the player is in a recovered <c>bImmort</c> state. Such a state takes no
        /// damage at all, suspends the passive health drain and clears the player's
        /// <c>CNT_OrMaskMonStrike</c> bits, so a monster does not select it as a target either.
        /// The three shock states and every tunnel and teleport state carry the flag; the death
        /// states do not.
        /// </summary>
        public bool IsImmortal => Reaction.Kind == PlayerReactionKind.Shocked
            || TunnelTransit is not null
            || Action?.Kind is PlayerActionKind.TunnelEntry
                or PlayerActionKind.TunnelExit
                or PlayerActionKind.TeleportEntry
                or PlayerActionKind.TeleportFlight
                or PlayerActionKind.TeleportExit;

        /// <summary>
        /// Gets whether the player is in a recovered <c>bLockLvlEnd</c> state, which holds the
        /// win settle open. Among implemented states it is exactly the <c>bImmort</c> set: the
        /// shock reaction and every tunnel and teleport state carry both flags, and the death
        /// state carries neither.
        /// </summary>
        public bool HoldsLevelEndOpen => IsImmortal;

        /// <summary>
        /// Gets the simulation time at which the player last used the fire control, or
        /// <see langword="null"/> before the first use. The recovered <c>ulWeapTime</c> is one
        /// shared timestamp: a weapon use and a hand strike delay each other, and each branch
        /// compares it against its own repeat time.
        /// </summary>
        public double? LastActionSeconds { get; internal set; }

        /// <summary>
        /// Gets whether a collected cola is waiting to be drunk. The recovered
        /// <c>ColaDrinkDo</c> only records <c>ST_BonCola</c> through <c>OnCellStateSet</c>, and
        /// <c>_OnCell</c> applies it at the next cell arrival in place of the state's own handler.
        /// </summary>
        public bool HasPendingColaDrink { get; internal set; }
    }
}
