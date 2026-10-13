using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Motion;
using Mooh.Core.Player;
using Mooh.Core.Shakes;

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
    /// <param name="shakeBody">The entity's optional cell-shake behavior.</param>
    /// <param name="fireBody">The entity's optional role in the fire-contact rule.</param>
    /// <param name="tunnelBody">The entity's optional tunnel routing tables.</param>
    /// <param name="flyingArrowBody">The entity's optional directional-arrow behavior.</param>
    /// <param name="teleporterBody">The entity's optional linked-teleporter behavior.</param>
    /// <param name="worldRotationBody">The entity's optional world-rotation behavior.</param>
    /// <param name="balloonFuseBody">The entity's optional balloon fuse.</param>
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
        SmallBeetleBody? smallBeetleBody = null,
        ShakeBody? shakeBody = null,
        FireBody? fireBody = null,
        TunnelBody? tunnelBody = null,
        FlyingArrowBody? flyingArrowBody = null,
        TeleporterBody? teleporterBody = null,
        WorldRotationBody? worldRotationBody = null,
        BalloonFuseBody? balloonFuseBody = null)
    {
        /// <summary>Gets the entity's stable identity.</summary>
        public EntityId Id { get; } = id;

        /// <summary>Gets the entity's authoritative grid position.</summary>
        public GridPosition Position { get; internal set; } = position;

        /// <summary>Gets or sets the entity's current interaction capabilities.</summary>
        public InteractionMask Interactions { get; set; } = interactions;

        /// <summary>
        /// Gets the entity's optional gravity behavior. It is cleared when an entity stops
        /// participating in gravity for good, which the recovered <c>_DoFlyAll</c> does to a
        /// launched arrow by dropping its <c>WEvOnFall</c> and <c>WEvOnSlide</c> registrations.
        /// </summary>
        public GravityBody? GravityBody { get; internal set; } = gravityBody;

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

        /// <summary>Gets the entity's optional cell-shake behavior.</summary>
        public ShakeBody? ShakeBody { get; } = shakeBody;

        /// <summary>Gets the entity's optional role in the fire-contact rule.</summary>
        public FireBody? FireBody { get; } = fireBody;

        /// <summary>Gets the entity's optional tunnel routing tables.</summary>
        public TunnelBody? TunnelBody { get; } = tunnelBody;

        /// <summary>Gets the entity's optional directional-arrow behavior.</summary>
        public FlyingArrowBody? FlyingArrowBody { get; } = flyingArrowBody;

        /// <summary>Gets the entity's optional linked-teleporter behavior.</summary>
        public TeleporterBody? TeleporterBody { get; } = teleporterBody;

        /// <summary>Gets the entity's optional world-rotation behavior.</summary>
        public WorldRotationBody? WorldRotationBody { get; } = worldRotationBody;

        /// <summary>Gets the entity's optional balloon fuse.</summary>
        public BalloonFuseBody? BalloonFuseBody { get; } = balloonFuseBody;

        /// <summary>Gets the entity's current continuous motion, if any.</summary>
        public MotionState? Motion { get; internal set; }

        /// <summary>
        /// Gets whether the entity is a player a monster may select as a target.
        /// </summary>
        /// <remarks>
        /// The original selects targets by mask, and a player in an immortal state — death and
        /// tunnel transit included — has its <c>CNT_OrMaskMonStrike</c> bits cleared, so neither
        /// a dead player nor one inside a tunnel is a target. This is deliberately stricter than
        /// <see cref="IsDamageablePlayer"/>.
        /// </remarks>
        public bool IsLivingPlayer => PlayerBody is { IsImmortal: false }
            && HealthBody is { IsDead: false };

        /// <summary>
        /// Gets whether the entity is a player a damage request can be addressed to.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="IsLivingPlayer"/> this tests neither death nor immortality, because
        /// the blast, shake, fire and landing-impact paths dispatch unconditionally and let
        /// <c>DamageSystem.Apply</c> reject the request, which reports <c>IgnoredDead</c> or
        /// <c>IgnoredImmune</c>.
        /// </remarks>
        public bool IsDamageablePlayer => PlayerBody is not null && HealthBody is not null;

        /// <summary>
        /// Gets whether the entity is a monster the ice bonus has frozen.
        /// </summary>
        /// <remarks>
        /// The recovered <c>IceOn</c> is one implementation for both monster families, and it
        /// routes <c>OnShake</c>, <c>OnShakeHard</c>, <c>OnExpl</c> and <c>PlCtrl</c> alike to
        /// <c>_Ice_Ctrl</c>, which shatters the monster without its blast or its child.
        /// </remarks>
        public bool IsFrozenMonster => GreenMonsterBody is { IsFrozen: true }
            || BlueMonsterBody is { IsFrozen: true };
    }
}
