using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Enemies
{
    /// <summary>Configures and tracks autonomous blue-monster behavior.</summary>
    public sealed class BlueMonsterBody
    {
        private static readonly TimeSpan DefaultAttackRecoveryDuration = TimeSpan.FromMilliseconds(1150);
        private static readonly TimeSpan DefaultAttackWindupDuration = TimeSpan.FromMilliseconds(600);
        private static readonly TimeSpan DefaultLeftAttackRecoveryDuration = TimeSpan.FromMilliseconds(985);

        /// <summary>Initializes blue-monster movement configuration.</summary>
        /// <param name="moveSpeed">The walking speed in cells per second.</param>
        /// <param name="facingDirection">The initial facing direction.</param>
        /// <param name="turnDuration">The duration of one quarter-turn.</param>
        /// <param name="attackWindupDuration">The optional delay between attack selection and its strike.</param>
        /// <param name="leftAttackRecoveryDuration">The optional post-strike turnaround duration when facing left.</param>
        /// <param name="attackRecoveryDuration">The optional post-strike turnaround duration for other directions.</param>
        /// <param name="strikeDamage">The health damage dealt by a successful strike.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="moveSpeed"/> is not finite and positive,
        /// <paramref name="facingDirection"/> is undefined, or
        /// a duration is not positive, an attack recovery does not exceed
        /// <paramref name="turnDuration"/>, or <paramref name="strikeDamage"/> is not finite and positive.
        /// </exception>
        public BlueMonsterBody(
            double moveSpeed,
            Direction facingDirection,
            TimeSpan turnDuration,
            TimeSpan? attackWindupDuration = null,
            TimeSpan? leftAttackRecoveryDuration = null,
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

            TimeSpan resolvedLeftAttackRecoveryDuration = leftAttackRecoveryDuration
                ?? DefaultLeftAttackRecoveryDuration;
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                resolvedLeftAttackRecoveryDuration,
                TimeSpan.Zero,
                nameof(leftAttackRecoveryDuration));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                turnDuration,
                resolvedLeftAttackRecoveryDuration,
                nameof(leftAttackRecoveryDuration));

            TimeSpan resolvedAttackRecoveryDuration = attackRecoveryDuration
                ?? DefaultAttackRecoveryDuration;
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                resolvedAttackRecoveryDuration,
                TimeSpan.Zero,
                nameof(attackRecoveryDuration));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                turnDuration,
                resolvedAttackRecoveryDuration,
                nameof(attackRecoveryDuration));

            if (!double.IsFinite(strikeDamage) || strikeDamage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(strikeDamage));
            }

            MoveSpeed = moveSpeed;
            FacingDirection = facingDirection;
            TurnDuration = turnDuration;
            AttackWindupDuration = resolvedAttackWindupDuration;
            LeftAttackRecoveryDuration = resolvedLeftAttackRecoveryDuration;
            AttackRecoveryDuration = resolvedAttackRecoveryDuration;
            StrikeDamage = strikeDamage;
        }

        /// <summary>Gets the walking speed in cells per second.</summary>
        public double MoveSpeed { get; }

        /// <summary>Gets the direction the monster currently faces.</summary>
        public Direction FacingDirection { get; internal set; }

        /// <summary>Gets the duration of one quarter-turn.</summary>
        public TimeSpan TurnDuration { get; }

        /// <summary>Gets the delay between attack selection and its strike.</summary>
        public TimeSpan AttackWindupDuration { get; }

        /// <summary>Gets the left-facing post-strike turnaround duration.</summary>
        public TimeSpan LeftAttackRecoveryDuration { get; }

        /// <summary>Gets the post-strike turnaround duration for non-left directions.</summary>
        public TimeSpan AttackRecoveryDuration { get; }

        /// <summary>Gets the health damage dealt by a successful strike.</summary>
        public double StrikeDamage { get; }

        /// <summary>Gets the monster's current behavior phase.</summary>
        public BlueMonsterState State { get; internal set; }

        /// <summary>Gets the time remaining in the current timed phase.</summary>
        public TimeSpan Remaining { get; internal set; }
    }
}
