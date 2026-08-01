using System;
using System.Buffers.Binary;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.Core.Legacy
{
    /// <summary>Translates validated archival levels through the explicit legacy object catalog.</summary>
    public static class LegacyLevelMaterializer
    {
        private static readonly TimeSpan MonsterTurnDuration = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan PlayerImmunityDuration = TimeSpan.FromMilliseconds(1000);

        /// <summary>Materializes a level with standard speeds and downward gravity.</summary>
        /// <param name="level">The validated archival level.</param>
        /// <returns>The live runtime level.</returns>
        public static MaterializedLevel Materialize(LevelDocument level)
        {
            return Materialize(level, Direction.Down, DifficultySpeeds.Standard);
        }

        /// <summary>Materializes a level with an explicit initial gravity and speed profile.</summary>
        /// <param name="level">The validated archival level.</param>
        /// <param name="gravityDirection">The initial cardinal gravity direction.</param>
        /// <param name="speeds">The recovered difficulty speed profile.</param>
        /// <returns>The live runtime level.</returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="gravityDirection"/> is undefined.</exception>
        /// <exception cref="LegacyMaterializationException">An object cannot cross the explicit catalog boundary.</exception>
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
                ?? throw new LegacyMaterializationException("The archival level has no object collection.");
            WorldGrid world = new(level.Size.Width, level.Size.Height);
            Dictionary<int, EntityId> idsByObjectId = [];

            foreach (LevelObject source in objects)
            {
                LegacyObjectDescriptor descriptor;
                try
                {
                    descriptor = LegacyObjectCatalog.Get(source.Type ?? string.Empty);
                }
                catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
                {
                    throw new LegacyMaterializationException(
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
                    throw new LegacyMaterializationException(
                        $"Archival object {source.Id} of type '{descriptor.LegacyType}' is invalid for this runtime world.",
                        exception);
                }
            }

            if (world.Player?.PlayerBody is PlayerBody playerBody)
            {
                int totalTreasure = 0;
                foreach (Entity entity in world.Entities)
                {
                    if (entity.LegacyBody?.Kind is LegacyEntityKind.Coin or LegacyEntityKind.Diamond)
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
                new Random(0));
            return new MaterializedLevel(
                level,
                world,
                simulation,
                gravityDirection,
                idsByObjectId);
        }

        private static Entity CreateEntity(
            EntityId id,
            LevelObject source,
            LegacyObjectDescriptor descriptor,
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
                        canSlide: true)
                    : null,
                descriptor.Kind == LegacyEntityKind.Player
                    ? new PlayerBody(speeds.PlayerSpeed, 1.5, Direction.Down)
                    : null,
                descriptor.Kind == LegacyEntityKind.Player
                    ? new HealthBody(100, 100, PlayerImmunityDuration)
                    : null,
                descriptor.Kind == LegacyEntityKind.GreenMonster
                    ? new GreenMonsterBody(speeds.MonsterSpeed, facing, MonsterTurnDuration)
                    : null,
                descriptor.Kind == LegacyEntityKind.BlueMonster
                    ? new BlueMonsterBody(speeds.MonsterSpeed, facing, MonsterTurnDuration)
                    : null,
                new LegacyEntityBody(source, descriptor));
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
