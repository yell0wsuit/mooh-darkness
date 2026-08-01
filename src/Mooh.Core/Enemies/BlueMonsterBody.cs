using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Enemies
{
    /// <summary>Configures and tracks autonomous blue-monster behavior.</summary>
    public sealed class BlueMonsterBody
    {
        /// <summary>Initializes blue-monster movement configuration.</summary>
        /// <param name="moveSpeed">The walking speed in cells per second.</param>
        /// <param name="facingDirection">The initial facing direction.</param>
        /// <param name="turnDuration">The duration of one quarter-turn.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="moveSpeed"/> is not finite and positive,
        /// <paramref name="facingDirection"/> is undefined, or
        /// <paramref name="turnDuration"/> is not positive.
        /// </exception>
        public BlueMonsterBody(
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

        /// <summary>Gets the duration of one quarter-turn.</summary>
        public TimeSpan TurnDuration { get; }

        /// <summary>Gets the monster's current behavior phase.</summary>
        public BlueMonsterState State { get; internal set; }

        /// <summary>Gets the time remaining in the current timed phase.</summary>
        public TimeSpan Remaining { get; internal set; }
    }
}
