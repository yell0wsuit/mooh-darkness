using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Enemies
{
    /// <summary>Configures and tracks autonomous green-monster behavior.</summary>
    public sealed class GreenMonsterBody
    {
        private static readonly TimeSpan DefaultAttackRecoveryDuration = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan DefaultAttackWindupDuration = TimeSpan.FromMilliseconds(625);

        /// <summary>Initializes green-monster movement configuration.</summary>
        /// <param name="moveSpeed">The walking speed in cells per second.</param>
        /// <param name="facingDirection">The initial facing direction.</param>
        /// <param name="turnDuration">The duration of one clockwise quarter-turn.</param>
        /// <param name="attackWindupDuration">The optional delay between attack selection and its strike.</param>
        /// <param name="attackRecoveryDuration">The optional post-strike recovery duration.</param>
        /// <param name="strikeDamage">The health damage dealt by a successful strike.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="moveSpeed"/> is not finite and positive,
        /// <paramref name="facingDirection"/> is undefined, a duration is not positive,
        /// or <paramref name="strikeDamage"/> is not finite and positive.
        /// </exception>
        public GreenMonsterBody(
            double moveSpeed,
            Direction facingDirection,
            TimeSpan turnDuration,
            TimeSpan? attackWindupDuration = null,
            TimeSpan? attackRecoveryDuration = null,
            double strikeDamage = 20)
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

            TimeSpan resolvedAttackWindupDuration = attackWindupDuration
                ?? DefaultAttackWindupDuration;
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                resolvedAttackWindupDuration,
                TimeSpan.Zero,
                nameof(attackWindupDuration));

            TimeSpan resolvedAttackRecoveryDuration = attackRecoveryDuration
                ?? DefaultAttackRecoveryDuration;
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                resolvedAttackRecoveryDuration,
                TimeSpan.Zero,
                nameof(attackRecoveryDuration));

            if (!double.IsFinite(strikeDamage) || strikeDamage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(strikeDamage));
            }

            MoveSpeed = moveSpeed;
            FacingDirection = facingDirection;
            TurnDuration = turnDuration;
            AttackWindupDuration = resolvedAttackWindupDuration;
            AttackRecoveryDuration = resolvedAttackRecoveryDuration;
            StrikeDamage = strikeDamage;
        }

        /// <summary>Gets the walking speed in cells per second.</summary>
        public double MoveSpeed { get; }

        /// <summary>Gets the direction the monster currently faces.</summary>
        public Direction FacingDirection { get; internal set; }

        /// <summary>Gets the duration of one clockwise quarter-turn.</summary>
        public TimeSpan TurnDuration { get; }

        /// <summary>Gets the delay between attack selection and its strike.</summary>
        public TimeSpan AttackWindupDuration { get; }

        /// <summary>Gets the post-strike recovery duration.</summary>
        public TimeSpan AttackRecoveryDuration { get; }

        /// <summary>Gets the health damage dealt by a successful strike.</summary>
        public double StrikeDamage { get; }

        /// <summary>Gets the monster's current behavior phase.</summary>
        public GreenMonsterState State { get; internal set; }

        /// <summary>Gets whether the active ice bonus has frozen the monster.</summary>
        public bool IsFrozen { get; internal set; }

        /// <summary>Gets the time remaining in the current timed phase.</summary>
        public TimeSpan Remaining { get; internal set; }
    }
}
