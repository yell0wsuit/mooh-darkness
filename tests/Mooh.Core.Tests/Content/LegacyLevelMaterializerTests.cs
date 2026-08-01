using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Grid;
using Mooh.Core.Legacy;

using Xunit;

namespace Mooh.Core.Tests.Content
{
    /// <summary>Verifies translation from archival level objects to live runtime entities.</summary>
    public sealed class LegacyLevelMaterializerTests
    {
        /// <summary>Verifies representative placed types receive their recovered runtime components.</summary>
        [Fact]
        public void MaterializesRecoveredComponents()
        {
            LevelDocument level = CreateLevel(
                Object(1, "CellsMetal01", 0, 3),
                Object(2, "OPlayer", 1, 1),
                Object(3, "ORock01", 2, 1),
                Object(4, "OWeight", 3, 1),
                Object(5, "OCoin01", 4, 1),
                Object(6, "MGrT", 5, 1),
                Object(7, "MBlueR", 6, 1));

            MaterializedLevel result = LegacyLevelMaterializer.Materialize(level);

            Assert.Equal(7, result.World.Entities.Count);
            Assert.NotNull(result.World.Player?.PlayerBody);
            Assert.NotNull(result.World.Player?.HealthBody);
            Assert.Equal(Direction.Down, result.GravityDirection);
            Assert.NotNull(result.GetEntity(3).GravityBody);
            Assert.Equal(1.875, result.GetEntity(3).GravityBody!.FallSpeed);
            Assert.Equal(3.75, result.GetEntity(4).GravityBody!.FallSpeed);
            Assert.Equal(LegacyEntityKind.Coin, result.GetEntity(5).LegacyBody!.Kind);
            Assert.Equal(Direction.Up, result.GetEntity(6).GreenMonsterBody!.FacingDirection);
            Assert.Equal(Direction.Right, result.GetEntity(7).BlueMonsterBody!.FacingDirection);
        }

        /// <summary>Verifies archival registration order produces original newest-first same-cell occupancy.</summary>
        [Fact]
        public void PreservesArchivalRegistrationAndCellOrder()
        {
            LevelDocument level = CreateLevel(
                Object(10, "CellsEat01", 2, 2),
                Object(11, "OCoin01", 2, 2),
                Object(12, "OPlayer", 1, 1));

            MaterializedLevel result = LegacyLevelMaterializer.Materialize(level);

            Assert.Equal([10, 11, 12], result.World.Entities.Select(entity => entity.LegacyBody!.ObjectId));
            Assert.Equal(
                [11, 10],
                result.World.GetOccupants(new GridPosition(2, 2))
                    .Select(entity => entity.LegacyBody!.ObjectId));
            Assert.Equal(result.GetEntity(11).Id, result.EntityIdsByObjectId[11]);
        }

        /// <summary>Verifies every committed level can cross the explicit runtime materialization boundary.</summary>
        [Fact]
        public void CompleteAuthoritativeCorpusMaterializes()
        {
            LoadedLevelPack pack = JsonLevelPackLoader.Load(Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "levels",
                "manifest.json"));

            MaterializedLevel[] levels = [.. pack.Levels.Select(LegacyLevelMaterializer.Materialize)];

            Assert.Equal(150, levels.Length);
            Assert.Equal(81_211, levels.Sum(level => level.World.Entities.Count));
            Assert.All(levels, level => Assert.NotNull(level.World.Player));
        }

        /// <summary>Verifies materialization does not guess behavior for unsupported types.</summary>
        [Fact]
        public void UnknownTypeIsRejectedWithObjectContext()
        {
            LevelDocument level = CreateLevel(Object(42, "OChest", 1, 1));

            LegacyMaterializationException exception = Assert.Throws<LegacyMaterializationException>(
                () => LegacyLevelMaterializer.Materialize(level));

            Assert.Contains("OChest", exception.Message, StringComparison.Ordinal);
            Assert.Contains("42", exception.Message, StringComparison.Ordinal);
        }

        private static LevelDocument CreateLevel(params LevelObject[] objects)
        {
            return new LevelDocument(
                1,
                null,
                new LevelSize(8, 6),
                new EditorPreview(0, 0, 8, 6),
                objects);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(
                id,
                type,
                0,
                new LevelPosition(x, y),
                Array.Empty<LevelSavedState>(),
                new Dictionary<string, System.Text.Json.JsonElement>());
        }
    }
}
