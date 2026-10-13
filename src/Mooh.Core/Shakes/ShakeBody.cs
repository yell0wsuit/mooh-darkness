using System;

namespace Mooh.Core.Shakes
{
    /// <summary>Describes an entity's recovered reactions to ordinary and hard cell shakes.</summary>
    public sealed class ShakeBody
    {
        /// <summary>Initializes shake behavior.</summary>
        /// <param name="ordinaryReaction">The reaction to an ordinary shake.</param>
        /// <param name="hardReaction">The reaction to a hard shake.</param>
        /// <param name="reactsToLanding">
        /// Whether a completed fall runs the ordinary reaction, the recovered <c>OnLand</c> naming
        /// the same handler as <c>OnShake</c>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">A reaction is undefined.</exception>
        public ShakeBody(ShakeReaction ordinaryReaction, ShakeReaction hardReaction, bool reactsToLanding = false)
        {
            if (!Enum.IsDefined(ordinaryReaction))
            {
                throw new ArgumentOutOfRangeException(nameof(ordinaryReaction), ordinaryReaction, null);
            }

            if (!Enum.IsDefined(hardReaction))
            {
                throw new ArgumentOutOfRangeException(nameof(hardReaction), hardReaction, null);
            }

            OrdinaryReaction = ordinaryReaction;
            HardReaction = hardReaction;
            ReactsToLanding = reactsToLanding;
        }

        /// <summary>Gets the reaction to an ordinary shake.</summary>
        public ShakeReaction OrdinaryReaction
        {
            get;
        }

        /// <summary>Gets the reaction to a hard shake.</summary>
        public ShakeReaction HardReaction
        {
            get;
        }

        /// <summary>
        /// Gets whether a completed fall runs the ordinary reaction. The barrel, the magic gem and
        /// the four arrows name their shake handler as <c>OnLand</c> too.
        /// </summary>
        public bool ReactsToLanding
        {
            get;
        }
    }
}
