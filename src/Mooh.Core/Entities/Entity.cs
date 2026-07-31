using Mooh.Core.Grid;
using Mooh.Core.Interactions;

namespace Mooh.Core.Entities
{
    /// <summary>
    /// Represents an object registered in the simulation world.
    /// </summary>
    /// <param name="id">The entity's stable identity.</param>
    /// <param name="position">The entity's initial grid position.</param>
    /// <param name="interactions">The interaction capabilities assigned to the entity.</param>
    public sealed class Entity(EntityId id, GridPosition position, InteractionMask interactions)
    {
        /// <summary>Gets the entity's stable identity.</summary>
        public EntityId Id { get; } = id;

        /// <summary>Gets the entity's authoritative grid position.</summary>
        public GridPosition Position { get; internal set; } = position;

        /// <summary>Gets or sets the entity's current interaction capabilities.</summary>
        public InteractionMask Interactions { get; set; } = interactions;
    }
}
