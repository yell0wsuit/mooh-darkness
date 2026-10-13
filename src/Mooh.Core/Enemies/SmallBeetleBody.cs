using System;

namespace Mooh.Core.Enemies
{
    /// <summary>Identifies and tracks a small beetle spawned by a defeated monster.</summary>
    /// <remarks>
    /// The recovered beetle in <c>GameObjSmBeetle.py</c> flies toward the player rather than
    /// walking a fixed route, is absorbed on contact instead of being eaten out of a blocking
    /// cell, and removes itself after <c>CNT_GPrm_SmallTime</c>. Because it also carries
    /// <c>CNT_CMaskLvlEndLock</c>, that lifetime is what guarantees a level can still be won when
    /// the beetle is unreachable.
    /// </remarks>
    public sealed class SmallBeetleBody
    {
        /// <summary>The recovered <c>CNT_GPrm_SmallTime</c> lifetime.</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

        /// <summary>The recovered <c>CNT_OnCellReCheck</c> delay after a blocked decision.</summary>
        public static readonly TimeSpan RecheckDelay = TimeSpan.FromMilliseconds(500);

        /// <summary>The recovered <c>CNT_ImTime</c> window during which blasts do not kill it.</summary>
        public static readonly TimeSpan ExplosionImmunity = TimeSpan.FromMilliseconds(1500);

        /// <summary>Initializes a spawned small beetle.</summary>
        /// <param name="color">The originating monster family.</param>
        /// <param name="moveSpeed">The flight speed in cells per second.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="color"/> is undefined, or <paramref name="moveSpeed"/> is not positive.
        /// </exception>
        public SmallBeetleBody(SmallBeetleColor color, double moveSpeed)
        {
            if (!Enum.IsDefined(color))
            {
                throw new ArgumentOutOfRangeException(nameof(color), color, null);
            }

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(moveSpeed);
            Color = color;
            MoveSpeed = moveSpeed;
        }

        /// <summary>Gets the originating monster family.</summary>
        public SmallBeetleColor Color
        {
            get;
        }

        /// <summary>Gets the flight speed in cells per second.</summary>
        public double MoveSpeed
        {
            get;
        }

        /// <summary>Gets the current flight state.</summary>
        public SmallBeetleState State { get; internal set; } = SmallBeetleState.Deciding;

        /// <summary>Gets the time left before the beetle removes itself.</summary>
        public TimeSpan RemainingLifetime { get; internal set; } = Lifetime;

        /// <summary>Gets the time left before a blast can destroy the beetle.</summary>
        public TimeSpan RemainingImmunity { get; internal set; } = ExplosionImmunity;

        /// <summary>Gets the time left in a blocked-decision wait, or zero when not waiting.</summary>
        public TimeSpan RemainingRecheck
        {
            get; internal set;
        }
    }
}
