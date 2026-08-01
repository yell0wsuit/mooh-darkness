using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Enemies
{
    /// <summary>Configures and tracks autonomous green-monster behavior.</summary>
    public sealed class GreenMonsterBody
    {
        /// <summary>Initializes green-monster movement configuration.</summary>
        /// <param name="moveSpeed">The walking speed in cells per second.</param>
        /// <param name="facingDirection">The initial facing direction.</param>
        /// <param name="turnDuration">The duration of one clockwise quarter-turn.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="moveSpeed"/> is not finite and positive,
        /// <paramref name="facingDirection"/> is undefined, or <paramref name="turnDuration"/> is not positive.
        /// </exception>
        public GreenMonsterBody(
            double moveSpeed,
            Direction facingDirection,
            TimeSpan turnDuration)
        {
            if (!double.IsFinite(moveSpeed) || moveSpeed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed));
            }

            if (!Enum.IsDefined(facingDirection))
            {
                throw new ArgumentOutOfRangeException(nameof(facingDirection), facingDirection, null);
            }

            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(turnDuration, TimeSpan.Zero);

            MoveSpeed = moveSpeed;
            FacingDirection = facingDirection;
            TurnDuration = turnDuration;
        }

        /// <summary>Gets the walking speed in cells per second.</summary>
        public double MoveSpeed { get; }

        /// <summary>Gets the direction the monster currently faces.</summary>
        public Direction FacingDirection { get; internal set; }

        /// <summary>Gets the duration of one clockwise quarter-turn.</summary>
        public TimeSpan TurnDuration { get; }

        /// <summary>Gets the monster's current behavior phase.</summary>
        public GreenMonsterState State { get; internal set; }

        /// <summary>Gets the time remaining in the current timed phase.</summary>
        public TimeSpan Remaining { get; internal set; }
    }
}
