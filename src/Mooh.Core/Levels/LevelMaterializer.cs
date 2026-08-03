using System;
using System.Buffers.Binary;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;
using Mooh.Core.Player;
using Mooh.Core.Shakes;
using Mooh.Core.Simulation;

namespace Mooh.Core.Levels
{
    /// <summary>Translates validated archival levels through the explicit archival object catalog.</summary>
    public static class LevelMaterializer
    {
        /// <summary>
        /// The archival object identifier reported by an entity created at runtime, which has no
        /// archival source and therefore cannot share the identifier space of placed objects.
        /// </summary>
        public const int RuntimeObjectId = -1;

        private static readonly TimeSpan BlueMonsterTurnDuration = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan GreenMonsterTurnDuration = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan PlayerImmunityDuration = TimeSpan.FromMilliseconds(650);

        /// <summary>Materializes a level with standard speeds and downward gravity.</summary>
        /// <param name="level">The validated archival level.</param>
        /// <returns>The live runtime level.</returns>
        public static MaterializedLevel Materialize(LevelDocument level)
        {
            return Materialize(level, Direction.Down, DifficultySpeeds.Standard);
        }

        /// <summary>Materializes a level with an explicit initial gravity and difficulty profile.</summary>
        /// <param name="level">The validated archival level.</param>
        /// <param name="gravityDirection">The initial cardinal gravity direction.</param>
        /// <param name="speeds">The recovered difficulty gameplay values.</param>
        /// <returns>The live runtime level.</returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="gravityDirection"/> is undefined.</exception>
        /// <exception cref="LevelMaterializationException">An object cannot cross the explicit catalog boundary.</exception>
        public static MaterializedLevel Materialize(
            LevelDocument level,
            Direction gravityDirection,
            DifficultySpeeds speeds)
        {
            ArgumentNullException.ThrowIfNull(level);
            ArgumentNullException.ThrowIfNull(speeds);
            if (!Enum.IsDefined(gravityDirection))
            {
                throw new ArgumentOutOfRangeException(nameof(gravityDirection), gravityDirection, null);
            }

            IReadOnlyList<LevelObject> objects = level.Objects
                ?? throw new LevelMaterializationException("The archival level has no object collection.");
            WorldGrid world = new(level.Size.Width, level.Size.Height);
            Dictionary<int, EntityId> idsByObjectId = [];

            foreach (LevelObject source in objects)
            {
                ArchivalObjectDescriptor descriptor;
                try
                {
                    descriptor = ArchivalObjectCatalog.Get(source.Type ?? string.Empty);
                }
                catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
                {
                    throw new LevelMaterializationException(
                        $"Archival object {source.Id} of type '{source.Type ?? "<null>"}' cannot be materialized.",
                        exception);
                }

                EntityId id = CreateDeterministicId(source.Id);
                Entity entity = CreateEntity(id, source, descriptor, speeds);
                try
                {
                    world.Add(entity);
                    idsByObjectId.Add(source.Id, id);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    throw new LevelMaterializationException(
                        $"Archival object {source.Id} of type '{descriptor.ArchivalType}' is invalid for this runtime world.",
                        exception);
                }
            }

            if (world.Player?.PlayerBody is PlayerBody playerBody)
            {
                int totalTreasure = 0;
                foreach (Entity entity in world.Entities)
                {
                    if (entity.ArchivalIdentity?.Kind is EntityKind.Coin or EntityKind.Diamond)
                    {
                        totalTreasure++;
                    }
                }

                playerBody.Progress.InitializeTreasure(totalTreasure);
            }

            SimulationWorld simulation = new(
                world,
                gravityDirection,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                speeds);
            return new MaterializedLevel(
                level,
                world,
                simulation,
                gravityDirection,
                idsByObjectId);
        }

        /// <summary>
        /// Materializes an object the original creates at runtime rather than placing in a level.
        /// The recovered engine registers such an object under its own <c>ObjRep</c> name while
        /// sharing a placed type's behavior: the thrown grenade <c>WBombInt</c> is
        /// <c>GameTypeBomb</c>, exactly as the placed <c>WBomb</c> pickup is. The entity therefore
        /// carries the catalog descriptor of the named placed type, and its archival identity
        /// reports <see cref="RuntimeObjectId"/> because it has no archival source.
        /// </summary>
        /// <param name="id">The entity's stable identity.</param>
        /// <param name="position">The cell the object is created in.</param>
        /// <param name="archivalType">The placed type whose recovered behavior it shares.</param>
        /// <param name="speeds">The recovered difficulty gameplay values.</param>
        /// <returns>The runtime entity, not yet registered in a world.</returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="KeyNotFoundException">The type is not in the placed-type catalog.</exception>
        public static Entity CreateRuntimeObject(
            EntityId id,
            GridPosition position,
            string archivalType,
            DifficultySpeeds speeds)
        {
            ArgumentNullException.ThrowIfNull(speeds);
            ArchivalObjectDescriptor descriptor = ArchivalObjectCatalog.Get(archivalType);
            LevelObject source = new(
                RuntimeObjectId,
                descriptor.ArchivalType,
                0,
                new LevelPosition(position.X, position.Y),
                null,
                null);
            return CreateEntity(id, source, descriptor, speeds);
        }

        private static Entity CreateEntity(
            EntityId id,
            LevelObject source,
            ArchivalObjectDescriptor descriptor,
            DifficultySpeeds speeds)
        {
            Direction facing = descriptor.Variant is >= 0 and <= 3
                ? (Direction)descriptor.Variant
                : Direction.Down;
            return new Entity(
                id,
                new GridPosition(source.Position.X, source.Position.Y),
                descriptor.Interactions,
                descriptor.HasGravity
                    ? new GravityBody(
                        speeds.NormalSpeed * descriptor.FallSpeedMultiplier,
                        canSlide: true,
                        descriptor.ImpactStrength,
                        descriptor.TreatsShadowAsSupport)
                    : null,
                descriptor.Kind == EntityKind.Player
                    ? new PlayerBody(speeds.PlayerSpeed, 1.5, Direction.Down)
                    : null,
                descriptor.Kind == EntityKind.Player
                    ? new HealthBody(100, 100, PlayerImmunityDuration)
                    : null,
                descriptor.Kind == EntityKind.GreenMonster
                    ? new GreenMonsterBody(
                        speeds.MonsterSpeed,
                        facing,
                        GreenMonsterTurnDuration,
                        strikeDamage: speeds.ShakeDamage)
                    : null,
                descriptor.Kind == EntityKind.BlueMonster
                    ? new BlueMonsterBody(
                        speeds.MonsterSpeed,
                        facing,
                        BlueMonsterTurnDuration,
                        strikeDamage: speeds.ShakeDamage)
                    : null,
                new ArchivalObjectIdentity(source, descriptor),
                descriptor.ExplosionReaction is ExplosionReaction reaction
                    ? new ExplosionBody(reaction, descriptor.DetonatesOnLanding)
                    : null,
                smallBeetleBody: null,
                shakeBody: descriptor.ShakeReaction == ShakeReaction.None
                    && descriptor.HardShakeReaction == ShakeReaction.None
                    ? null
                    : new ShakeBody(descriptor.ShakeReaction, descriptor.HardShakeReaction),
                fireBody: descriptor.FireContact is FireContact fireContact
                    ? new FireBody(fireContact)
                    : null,
                tunnelBody: descriptor.TunnelShape is TunnelShape tunnelShape
                    ? new TunnelBody(tunnelShape)
                    : null);
        }

        private static EntityId CreateDeterministicId(int objectId)
        {
            Span<byte> bytes = stackalloc byte[16];
            bytes.Clear();
            BinaryPrimitives.WriteInt32LittleEndian(bytes, objectId);
            return new EntityId(new Guid(bytes));
        }
    }
}
