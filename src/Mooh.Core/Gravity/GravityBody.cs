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
        /// <param name="impactStrength">The shake strength produced when the body lands.</param>
        /// <param name="treatsShadowAsSupport">
        /// Whether a shadow occupant supports the body. This is the recovered
        /// <c>bSecondSlideMask</c> flag, which swaps the support test from
        /// <c>CNT_OrMaskNotEmpty</c> to <c>CNT_OrMaskNotEmpty2</c> and so adds the two shadow
        /// bits to it. Fire is the only placed type that sets it, which is what makes fire rest
        /// on another fire instead of falling straight through it.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="fallSpeed"/> is not finite and positive.
        /// </exception>
        public GravityBody(
            double fallSpeed,
            bool canSlide,
            GravityImpactStrength impactStrength = GravityImpactStrength.Ordinary,
            bool treatsShadowAsSupport = false)
        {
            if (!double.IsFinite(fallSpeed) || fallSpeed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fallSpeed));
            }

            if (!Enum.IsDefined(impactStrength))
            {
                throw new ArgumentOutOfRangeException(nameof(impactStrength), impactStrength, null);
            }

            FallSpeed = fallSpeed;
            CanSlide = canSlide;
            ImpactStrength = impactStrength;
            TreatsShadowAsSupport = treatsShadowAsSupport;
        }

        /// <summary>
        /// Remembers a completed falling segment whose landing callback waits for a stable,
        /// unlocked support cell.
        /// </summary>
        internal bool AwaitingLanding
        {
            get; set;
        }

        /// <summary>Gets the fall speed in cells per second.</summary>
        public double FallSpeed
        {
            get;
        }

        /// <summary>Gets a value indicating whether the body may slide around support.</summary>
        public bool CanSlide
        {
            get;
        }

        /// <summary>Gets the shake strength produced when this body lands.</summary>
        public GravityImpactStrength ImpactStrength
        {
            get;
        }

        /// <summary>
        /// Gets whether a shadow occupant supports this body, the recovered
        /// <c>bSecondSlideMask</c> flag.
        /// </summary>
        public bool TreatsShadowAsSupport
        {
            get;
        }
    }
}
