using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;

using Xunit;

namespace Mooh.Core.Tests.Content
{
    /// <summary>Verifies the explicit runtime catalog for archival object types.</summary>
    public sealed class ArchivalObjectCatalogTests
    {
        /// <summary>Verifies every type placed in the authoritative corpus has one descriptor.</summary>
        [Fact]
        public void CatalogExactlyCoversPlacedCorpusTypes()
        {
            LoadedLevelPack pack = JsonLevelPackLoader.Load(Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "levels",
                "manifest.json"));
            string[] placedTypes = [.. pack.Levels
                .SelectMany(level => level.Objects!)
                .Select(levelObject => levelObject.Type!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];

            Assert.Equal(85, placedTypes.Length);
            Assert.Equal(placedTypes, ArchivalObjectCatalog.Types.Order(StringComparer.Ordinal));
            Assert.All(placedTypes, type => Assert.Equal(type, ArchivalObjectCatalog.Get(type).ArchivalType));
        }

        /// <summary>Verifies representative recovered semantics are explicit rather than inferred from names.</summary>
        [Theory]
        [MemberData(nameof(RepresentativeDescriptors))]
        public void RepresentativeTypesHaveRecoveredDescriptors(
            string archivalType,
            EntityKind expectedKind,
            InteractionMask expectedInteractions,
            bool expectedGravity,
            double expectedFallSpeedMultiplier,
            int expectedVariant)
        {
            ArchivalObjectDescriptor descriptor = ArchivalObjectCatalog.Get(archivalType);

            Assert.Equal(expectedKind, descriptor.Kind);
            Assert.Equal(expectedInteractions, descriptor.Interactions);
            Assert.Equal(expectedGravity, descriptor.HasGravity);
            Assert.Equal(expectedFallSpeedMultiplier, descriptor.FallSpeedMultiplier);
            Assert.Equal(expectedVariant, descriptor.Variant);
        }

        /// <summary>Gets representative catalog cases from each placed runtime family.</summary>
        public static IEnumerable<object[]> RepresentativeDescriptors()
        {
            yield return ["CellsMetal01", EntityKind.MetalTerrain,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked | InteractionMask.RoundSupport,
                false, 1d, 0];
            yield return ["ORock01", EntityKind.Rock,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable | InteractionMask.RoundSupport,
                true, 1d, 0];
            yield return ["OWeight", EntityKind.Weight,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable | InteractionMask.RoundSupport
                    | InteractionMask.PushesThroughOccupants,
                true, 2d, 0];
            yield return ["OCoin03", EntityKind.Coin,
                InteractionMask.PlayerEdible | InteractionMask.RoundSupport,
                true, 1d, 3];
            yield return ["OKDKey2", EntityKind.Key,
                InteractionMask.BlocksMovement | InteractionMask.PlayerEdible | InteractionMask.RoundSupport,
                true, 1d, 2];
            yield return ["WGrBox", EntityKind.GrenadeBox,
                InteractionMask.PlayerEdible | InteractionMask.RoundSupport,
                true, 1d, 5];
            yield return ["OArrowT", EntityKind.GravityArrow,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable | InteractionMask.RoundSupport,
                true, 1d, 1];
            yield return ["MBlueR", EntityKind.BlueMonster,
                InteractionMask.BlocksMovement,
                false, 1d, 2];
            yield return ["OWRotL", EntityKind.WorldRotation,
                InteractionMask.BlocksMovement | InteractionMask.RoundSupport,
                false, 1d, -1];
            yield return ["OTPort", EntityKind.Teleporter,
                InteractionMask.MonsterTransparent,
                false, 1d, 0];
            yield return ["OTable", EntityKind.MessageTable,
                InteractionMask.MonsterTransparent,
                false, 1d, 0];
            yield return ["WMine", EntityKind.Mine,
                InteractionMask.MonsterTransparent,
                false, 1d, 0];
        }

        /// <summary>Verifies only the recovered monster-shadow types are transparent to monster navigation.</summary>
        [Fact]
        public void OnlyRecoveredMonsterShadowTypesAreTransparent()
        {
            string[] transparent = [.. ArchivalObjectCatalog.Types
                .Where(type => ArchivalObjectCatalog.Get(type).Interactions
                    .HasFlag(InteractionMask.MonsterTransparent))
                .Order(StringComparer.Ordinal)];

            Assert.Equal(["OTPort", "OTable", "WMine"], transparent);
        }

        /// <summary>Verifies unsupported legacy names are rejected at the catalog boundary.</summary>
        [Fact]
        public void UnsupportedTypeIsRejected()
        {
            KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
                () => ArchivalObjectCatalog.Get("OChest"));

            Assert.Contains("OChest", exception.Message, StringComparison.Ordinal);
        }
    }
}
