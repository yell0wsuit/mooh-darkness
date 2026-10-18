using System;

namespace Mooh.Core.Explosions
{
    /// <summary>Describes an entity's recovered participation in explosion propagation.</summary>
    public sealed class ExplosionBody
    {
        /// <summary>Initializes explosion behavior.</summary>
        /// <param name="reaction">The reaction when an explosion reaches the entity.</param>
        /// <param name="detonatesOnLanding">Whether a completed fall starts a 3x3 explosion.</param>
        public ExplosionBody(ExplosionReaction reaction, bool detonatesOnLanding)
        {
            if (!Enum.IsDefined(reaction))
            {
                throw new ArgumentOutOfRangeException(nameof(reaction), reaction, null);
            }

            Reaction = reaction;
            DetonatesOnLanding = detonatesOnLanding;
        }

        /// <summary>Gets the reaction when an explosion reaches the entity.</summary>
        public ExplosionReaction Reaction
        {
            get;
        }

        /// <summary>Gets whether a completed fall starts a 3x3 explosion.</summary>
        public bool DetonatesOnLanding
        {
            get;
        }
    }
}
