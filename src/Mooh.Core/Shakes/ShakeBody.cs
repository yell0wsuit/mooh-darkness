using System;

namespace Mooh.Core.Shakes
{
    /// <summary>Describes an entity's recovered reactions to ordinary and hard cell shakes.</summary>
    public sealed class ShakeBody
    {
        /// <summary>Initializes shake behavior.</summary>
        /// <param name="ordinaryReaction">The reaction to an ordinary shake.</param>
        /// <param name="hardReaction">The reaction to a hard shake.</param>
        /// <exception cref="ArgumentOutOfRangeException">A reaction is undefined.</exception>
        public ShakeBody(ShakeReaction ordinaryReaction, ShakeReaction hardReaction)
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
        }

        /// <summary>Gets the reaction to an ordinary shake.</summary>
        public ShakeReaction OrdinaryReaction { get; }

        /// <summary>Gets the reaction to a hard shake.</summary>
        public ShakeReaction HardReaction { get; }
    }
}
