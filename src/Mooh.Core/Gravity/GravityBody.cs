using System;

namespace Mooh.Core.Gravity
{
    /// <summary>
    /// Describes an entity's response to gravity.
    /// </summary>
    public sealed class GravityBody
    {
        /// <summary>
        /// Initializes a gravity body with the specified movement capabilities.
        /// </summary>
        /// <param name="fallSpeed">The positive speed in cells per second.</param>
        /// <param name="canSlide">Whether the body may slide around support.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="fallSpeed"/> is not finite and positive.
        /// </exception>
        public GravityBody(double fallSpeed, bool canSlide)
        {
            if (!double.IsFinite(fallSpeed) || fallSpeed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fallSpeed));
            }

            FallSpeed = fallSpeed;
            CanSlide = canSlide;
        }

        /// <summary>Gets the fall speed in cells per second.</summary>
        public double FallSpeed { get; }

        /// <summary>Gets a value indicating whether the body may slide around support.</summary>
        public bool CanSlide { get; }
    }
}
