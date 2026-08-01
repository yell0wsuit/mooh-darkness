using System;

namespace Mooh.Core.Combat
{
    /// <summary>Stores mutable normal-mode health and post-hit immunity.</summary>
    public sealed class HealthBody
    {
        /// <summary>Initializes health state.</summary>
        /// <param name="maximum">The finite positive maximum health.</param>
        /// <param name="current">The finite initial health in the range (0, <paramref name="maximum"/>].</param>
        /// <param name="immunityDuration">The non-negative immunity duration started by applied damage.</param>
        /// <exception cref="ArgumentOutOfRangeException">A supplied value lies outside its documented range.</exception>
        public HealthBody(double maximum, double current, TimeSpan immunityDuration)
        {
            if (!double.IsFinite(maximum) || maximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }

            if (!double.IsFinite(current) || current <= 0 || current > maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(current));
            }

            ArgumentOutOfRangeException.ThrowIfLessThan(immunityDuration, TimeSpan.Zero);
            Maximum = maximum;
            Current = current;
            ImmunityDuration = immunityDuration;
        }

        /// <summary>Gets the maximum health.</summary>
        public double Maximum { get; }

        /// <summary>Gets the current health.</summary>
        public double Current { get; private set; }

        /// <summary>Gets the configured immunity duration started by applied damage.</summary>
        public TimeSpan ImmunityDuration { get; }

        /// <summary>Gets the remaining post-hit immunity duration.</summary>
        public TimeSpan ImmunityRemaining { get; internal set; }

        /// <summary>Gets whether health has reached zero.</summary>
        public bool IsDead => Current <= 0;

        internal void Apply(double amount)
        {
            Current = Math.Max(0, Current - amount);
            ImmunityRemaining = ImmunityDuration;
        }

        internal void Heal(double amount)
        {
            if (!double.IsFinite(amount) || amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Current = Math.Min(Maximum, Current + amount);
        }

        internal void AdvanceImmunity(TimeSpan elapsed)
        {
            if (ImmunityRemaining <= TimeSpan.Zero)
            {
                return;
            }

            ImmunityRemaining = elapsed >= ImmunityRemaining
                ? TimeSpan.Zero
                : ImmunityRemaining - elapsed;
        }
    }
}
