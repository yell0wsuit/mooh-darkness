using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
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
    /// <param name="healthBody">The entity's optional health behavior.</param>
    /// <param name="greenMonsterBody">The entity's optional green-monster behavior.</param>
    /// <param name="blueMonsterBody">The entity's optional blue-monster behavior.</param>
    /// <param name="archivalIdentity">The entity's optional archival type metadata.</param>
    /// <param name="explosionBody">The entity's optional explosion behavior.</param>
    /// <param name="smallBeetleBody">The entity's optional spawned small-beetle behavior.</param>
    public sealed class Entity(
        EntityId id,
        GridPosition position,
        InteractionMask interactions,
        GravityBody? gravityBody = null,
        PlayerBody? playerBody = null,
        HealthBody? healthBody = null,
        GreenMonsterBody? greenMonsterBody = null,
        BlueMonsterBody? blueMonsterBody = null,
        ArchivalObjectIdentity? archivalIdentity = null,
        ExplosionBody? explosionBody = null,
        SmallBeetleBody? smallBeetleBody = null)
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

        /// <summary>Gets the entity's optional health behavior.</summary>
        public HealthBody? HealthBody { get; } = healthBody;

        /// <summary>Gets the entity's optional green-monster behavior.</summary>
        public GreenMonsterBody? GreenMonsterBody { get; } = greenMonsterBody;

        /// <summary>Gets the entity's optional blue-monster behavior.</summary>
        public BlueMonsterBody? BlueMonsterBody { get; } = blueMonsterBody;

        /// <summary>Gets the entity's optional archival type metadata.</summary>
        public ArchivalObjectIdentity? ArchivalIdentity { get; } = archivalIdentity;

        /// <summary>Gets the entity's optional explosion behavior.</summary>
        public ExplosionBody? ExplosionBody { get; } = explosionBody;

        /// <summary>Gets the entity's optional spawned small-beetle behavior.</summary>
        public SmallBeetleBody? SmallBeetleBody { get; } = smallBeetleBody;

        /// <summary>Gets the entity's current continuous motion, if any.</summary>
        public MotionState? Motion { get; internal set; }
    }
}
