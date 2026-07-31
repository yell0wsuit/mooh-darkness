using System;

namespace Mooh.Core.Entities
{
    /// <summary>
    /// Uniquely identifies an entity registered in a world.
    /// </summary>
    /// <param name="Value">The underlying globally unique value.</param>
    public readonly record struct EntityId(Guid Value)
    {
        /// <summary>
        /// Creates a new unique entity identifier.
        /// </summary>
        /// <returns>A newly generated identifier.</returns>
        public static EntityId New()
        {
            return new EntityId(Guid.NewGuid());
        }
    }
}
