using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Player
{
    /// <summary>
    /// Configures the continuous movement behavior of the world's player.
    /// </summary>
    public sealed class PlayerBody
    {
        /// <summary>
        /// Initializes player movement configuration.
        /// </summary>
        /// <param name="moveSpeed">The player's walking speed in cells per second.</param>
        /// <param name="pushSpeedMultiplier">The multiplier applied to pushed-object speed.</param>
        /// <param name="facingDirection">The player's initial facing direction.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="moveSpeed"/> or <paramref name="pushSpeedMultiplier"/> is not finite and positive,
        /// or <paramref name="facingDirection"/> is undefined.
        /// </exception>
        public PlayerBody(double moveSpeed, double pushSpeedMultiplier, Direction facingDirection)
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

            MoveSpeed = moveSpeed;
            PushSpeedMultiplier = pushSpeedMultiplier;
            FacingDirection = facingDirection;
        }

        /// <summary>Gets the player's walking speed in cells per second.</summary>
        public double MoveSpeed { get; }

        /// <summary>Gets the multiplier applied to pushed-object speed.</summary>
        public double PushSpeedMultiplier { get; }

        /// <summary>Gets the direction the player currently faces.</summary>
        public Direction FacingDirection { get; internal set; }

        /// <summary>Gets the active uncontrollable rotation, if any.</summary>
        public PlayerRotation? Rotation { get; internal set; }
    }
}
