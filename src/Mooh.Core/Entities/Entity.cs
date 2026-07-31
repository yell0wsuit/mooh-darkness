using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Player;

namespace Mooh.Core.Entities
{
    /// <summary>
    /// Represents an object registered in the simulation world.
    /// </summary>
    /// <param name="id">The entity's stable identity.</param>
    /// <param name="position">The entity's initial grid position.</param>
    /// <param name="interactions">The interaction capabilities assigned to the entity.</param>
    /// <param name="gravityBody">The entity's optional gravity behavior.</param>
    /// <param name="playerBody">The entity's optional player behavior.</param>
    public sealed class Entity(
        EntityId id,
        GridPosition position,
        InteractionMask interactions,
        GravityBody? gravityBody = null,
        PlayerBody? playerBody = null)
    {
        /// <summary>Gets the entity's stable identity.</summary>
        public EntityId Id { get; } = id;

        /// <summary>Gets the entity's authoritative grid position.</summary>
        public GridPosition Position { get; internal set; } = position;

        /// <summary>Gets or sets the entity's current interaction capabilities.</summary>
        public InteractionMask Interactions { get; set; } = interactions;

        /// <summary>Gets the entity's optional gravity behavior.</summary>
        public GravityBody? GravityBody { get; } = gravityBody;

        /// <summary>Gets the entity's optional player behavior.</summary>
        public PlayerBody? PlayerBody { get; } = playerBody;

        /// <summary>Gets the entity's current gravity-driven motion, if any.</summary>
        public MotionState? Motion { get; internal set; }
    }
}
