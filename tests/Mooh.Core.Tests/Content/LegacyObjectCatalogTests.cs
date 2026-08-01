using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Interactions;
using Mooh.Core.Legacy;

using Xunit;

namespace Mooh.Core.Tests.Content
{
    /// <summary>Verifies the explicit runtime catalog for archival object types.</summary>
    public sealed class LegacyObjectCatalogTests
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
            Assert.Equal(placedTypes, LegacyObjectCatalog.Types.Order(StringComparer.Ordinal));
            Assert.All(placedTypes, type => Assert.Equal(type, LegacyObjectCatalog.Get(type).LegacyType));
        }

        /// <summary>Verifies representative recovered semantics are explicit rather than inferred from names.</summary>
        [Theory]
        [MemberData(nameof(RepresentativeDescriptors))]
        public void RepresentativeTypesHaveRecoveredDescriptors(
            string legacyType,
            LegacyEntityKind expectedKind,
            InteractionMask expectedInteractions,
            bool expectedGravity,
            double expectedFallSpeedMultiplier,
            int expectedVariant)
        {
            LegacyObjectDescriptor descriptor = LegacyObjectCatalog.Get(legacyType);

            Assert.Equal(expectedKind, descriptor.Kind);
            Assert.Equal(expectedInteractions, descriptor.Interactions);
            Assert.Equal(expectedGravity, descriptor.HasGravity);
            Assert.Equal(expectedFallSpeedMultiplier, descriptor.FallSpeedMultiplier);
            Assert.Equal(expectedVariant, descriptor.Variant);
        }

        /// <summary>Gets representative catalog cases from each placed runtime family.</summary>
        public static IEnumerable<object[]> RepresentativeDescriptors()
        {
            yield return ["CellsMetal01", LegacyEntityKind.MetalTerrain,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked | InteractionMask.RoundSupport,
                false, 1d, 0];
            yield return ["ORock01", LegacyEntityKind.Rock,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable | InteractionMask.RoundSupport,
                true, 1d, 0];
            yield return ["OWeight", LegacyEntityKind.Weight,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable | InteractionMask.RoundSupport
                    | InteractionMask.PushesThroughOccupants,
                true, 2d, 0];
            yield return ["OCoin03", LegacyEntityKind.Coin,
                InteractionMask.PlayerEdible | InteractionMask.RoundSupport,
                true, 1d, 3];
            yield return ["OKDKey2", LegacyEntityKind.Key,
                InteractionMask.BlocksMovement | InteractionMask.PlayerEdible | InteractionMask.RoundSupport,
                true, 1d, 2];
            yield return ["WGrBox", LegacyEntityKind.GrenadeBox,
                InteractionMask.PlayerEdible | InteractionMask.RoundSupport,
                true, 1d, 5];
            yield return ["OArrowT", LegacyEntityKind.GravityArrow,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable | InteractionMask.RoundSupport,
                true, 1d, 1];
            yield return ["MBlueR", LegacyEntityKind.BlueMonster,
                InteractionMask.BlocksMovement,
                false, 1d, 2];
        }

        /// <summary>Verifies unsupported legacy names are rejected at the catalog boundary.</summary>
        [Fact]
        public void UnsupportedTypeIsRejected()
        {
            KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
                () => LegacyObjectCatalog.Get("OChest"));

            Assert.Contains("OChest", exception.Message, StringComparison.Ordinal);
        }
    }
}
