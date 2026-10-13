using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Explosions;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Shakes;

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
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked
                    | InteractionMask.RoundSupport | InteractionMask.BlocksFlight,
                false, 1d, 0];
            yield return ["CellsEat01", EntityKind.DiggableTerrain,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked
                    | InteractionMask.PlayerEdible | InteractionMask.CanBePushedThrough
                    | InteractionMask.BlocksFlight | InteractionMask.DestroyedByFlyingArrow,
                false, 1d, 0];
            yield return ["CellsSolid01", EntityKind.DestructibleTerrain,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked
                    | InteractionMask.BlocksFlight | InteractionMask.DestroyedByFlyingArrow,
                false, 1d, 0];
            yield return ["ORock01", EntityKind.Rock,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable | InteractionMask.RoundSupport,
                true, 1d, 0];
            yield return ["OWeight", EntityKind.Weight,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable | InteractionMask.RoundSupport
                    | InteractionMask.PushesThroughOccupants,
                true, 2d, 0];
            yield return ["OCoin03", EntityKind.Coin,
                InteractionMask.BlocksMovement | InteractionMask.PlayerEdible
                    | InteractionMask.RoundSupport,
                true, 1d, 3];
            yield return ["OKDKey2", EntityKind.Key,
                InteractionMask.BlocksMovement | InteractionMask.PlayerEdible | InteractionMask.RoundSupport,
                true, 1d, 2];
            yield return ["WGrBox", EntityKind.GrenadeBox,
                InteractionMask.BlocksMovement | InteractionMask.PlayerEdible
                    | InteractionMask.RoundSupport | InteractionMask.BlueMonsterTarget,
                true, 1d, 5];
            yield return ["OBaloon", EntityKind.Balloon,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable
                    | InteractionMask.RoundSupport | InteractionMask.PushesThroughOccupants
                    | InteractionMask.GravityAxisPushable | InteractionMask.BlueMonsterTarget,
                false, 1d, 0];
            yield return ["OBubble", EntityKind.Bubble,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable
                    | InteractionMask.RoundSupport | InteractionMask.PushesThroughOccupants
                    | InteractionMask.GravityAxisPushable,
                false, 1d, 0];
            yield return ["OBarel", EntityKind.Barrel,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable
                    | InteractionMask.RoundSupport | InteractionMask.GravityAxisPushable
                    | InteractionMask.BlueMonsterTarget,
                true, 1d, 0];
            yield return ["OArrowT", EntityKind.FlyingArrow,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable | InteractionMask.RoundSupport
                    | InteractionMask.BlueMonsterTarget,
                true, 1d, 1];
            yield return ["MBlueR", EntityKind.BlueMonster,
                InteractionMask.BlocksMovement | InteractionMask.LevelEndLock,
                false, 1d, 2];
            yield return ["OWRotL", EntityKind.WorldRotation,
                InteractionMask.BlocksMovement | InteractionMask.RoundSupport,
                false, 1d, -1];
            yield return ["OTPort", EntityKind.Teleporter,
                InteractionMask.MonsterTransparent | InteractionMask.OccupancyTransparent,
                false, 1d, 0];
            yield return ["OTable", EntityKind.MessageTable,
                InteractionMask.MonsterTransparent | InteractionMask.OccupancyTransparent,
                false, 1d, 0];
            yield return ["WMine", EntityKind.Mine,
                InteractionMask.MonsterTransparent | InteractionMask.OccupancyTransparent,
                false, 1d, 0];
        }

        /// <summary>
        /// Verifies only the recovered eat-cell families carry the player-eat bit. The original
        /// places that bit and the push-through bit on those three families alone, so every other
        /// terrain family must remain solid.
        /// </summary>
        [Fact]
        public void OnlyRecoveredEatCellFamiliesAreDiggable()
        {
            string[] diggableTerrain = [.. ArchivalObjectCatalog.Types
                .Where(type => ArchivalObjectCatalog.Get(type).Kind is EntityKind.DiggableTerrain
                    or EntityKind.DestructibleTerrain
                    or EntityKind.MetalTerrain
                    or EntityKind.WoodTerrain)
                .Where(type => ArchivalObjectCatalog.Get(type).Interactions
                    .HasFlag(InteractionMask.PlayerEdible))
                .Order(StringComparer.Ordinal)];

            Assert.Equal(["CellsEat01", "DCellsEat01", "JCellsEat01"], diggableTerrain);
            Assert.All(
                diggableTerrain,
                type => Assert.True(ArchivalObjectCatalog.Get(type).Interactions
                    .HasFlag(InteractionMask.CanBePushedThrough)));
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

        /// <summary>
        /// Verifies exactly the recovered <c>CNT_OrMaskShadow</c> carriers hide from ordinary cell
        /// queries. The world list is built with
        /// <c>CNT_CMaskShadowObj | CNT_CMaskShadowWeap | CNT_CMaskMonShadow</c>, so the set is the
        /// monster-shadow types plus fire, which carries <c>CNT_CMaskShadowObj</c> alone.
        /// </summary>
        [Fact]
        public void OnlyRecoveredShadowTypesAreOccupancyTransparent()
        {
            string[] transparent = [.. ArchivalObjectCatalog.Types
                .Where(type => ArchivalObjectCatalog.Get(type).Interactions
                    .HasFlag(InteractionMask.OccupancyTransparent))
                .Order(StringComparer.Ordinal)];

            Assert.Equal(["OFire", "OTPort", "OTable", "WMine"], transparent);
        }

        /// <summary>Verifies exactly the recovered blue-strike mask carriers are strike targets.</summary>
        [Fact]
        public void OnlyRecoveredBlueStrikeTypesAreTargets()
        {
            string[] targets = [.. ArchivalObjectCatalog.Types
                .Where(type => ArchivalObjectCatalog.Get(type).Interactions
                    .HasFlag(InteractionMask.BlueMonsterTarget))
                .Order(StringComparer.Ordinal)];

            Assert.Equal(
                [
                    "MMBio", "OArrowB", "OArrowL", "OArrowR", "OArrowT", "OBaloon", "OBarel",
                    "OBottle", "OMagic", "WBBomb", "WBomb", "WGrBox", "WMinesBox", "WRBox",
                    "WROne",
                ],
                targets);
        }

        /// <summary>
        /// Verifies exactly the recovered <c>CNT_CMaskLvlEndLock</c> carriers hold the level open.
        /// Among placed types only the two monsters carry it; the small beetle carries it too but
        /// is spawned at runtime rather than placed.
        /// </summary>
        [Fact]
        public void OnlyRecoveredMonstersCarryTheLevelEndLock()
        {
            string[] carriers = [.. ArchivalObjectCatalog.Types
                .Where(type => ArchivalObjectCatalog.Get(type).Interactions
                    .HasFlag(InteractionMask.LevelEndLock))
                .Order(StringComparer.Ordinal)];

            Assert.Equal(
                ["MBlueB", "MBlueL", "MBlueR", "MBlueT", "MGrB", "MGrL", "MGrR", "MGrT"],
                carriers);
        }

        /// <summary>Verifies recovered ordinary and hard shake handlers are explicit per type.</summary>
        /// <param name="archivalType">The archival type under test.</param>
        /// <param name="expectedOrdinary">The recovered <c>OnShake</c> reaction.</param>
        /// <param name="expectedHard">The recovered <c>OnShakeHard</c> reaction.</param>
        [Theory]
        [InlineData("OBottle", ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3)]
        [InlineData("OBaloon", ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3)]
        [InlineData("WMine", ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3)]
        [InlineData("WBBomb", ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3)]
        [InlineData("BCola", ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3)]
        [InlineData("MMBio", ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3)]
        [InlineData("MGrT", ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3)]
        [InlineData("MBlueR", ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3)]
        [InlineData("OBubble", ShakeReaction.Destroy, ShakeReaction.Destroy)]
        [InlineData("OCoin02", ShakeReaction.None, ShakeReaction.Destroy)]
        [InlineData("ODiam05", ShakeReaction.None, ShakeReaction.Destroy)]
        [InlineData("OEat01", ShakeReaction.None, ShakeReaction.Destroy)]
        [InlineData("OBarel", ShakeReaction.Deferred, ShakeReaction.Deferred)]
        [InlineData("OMagic", ShakeReaction.Deferred, ShakeReaction.Deferred)]
        [InlineData("OArrowT", ShakeReaction.LaunchArrow, ShakeReaction.LaunchArrow)]
        [InlineData("ORock01", ShakeReaction.None, ShakeReaction.None)]
        [InlineData("OWeight", ShakeReaction.None, ShakeReaction.None)]
        [InlineData("OFire", ShakeReaction.None, ShakeReaction.None)]
        [InlineData("OKDKey1", ShakeReaction.None, ShakeReaction.None)]
        [InlineData("CellsMetal01", ShakeReaction.None, ShakeReaction.None)]
        public void RecoveredShakeReactionsAreExplicit(
            string archivalType,
            ShakeReaction expectedOrdinary,
            ShakeReaction expectedHard)
        {
            ArchivalObjectDescriptor descriptor = ArchivalObjectCatalog.Get(archivalType);

            Assert.Equal(expectedOrdinary, descriptor.ShakeReaction);
            Assert.Equal(expectedHard, descriptor.HardShakeReaction);
        }

        /// <summary>Verifies the deferred shake handlers are exactly the recovered specializations.</summary>
        [Fact]
        public void DeferredShakeReactionsAreTheKnownSpecializations()
        {
            string[] deferred = [.. ArchivalObjectCatalog.Types
                .Where(type => ArchivalObjectCatalog.Get(type).ShakeReaction == ShakeReaction.Deferred
                    || ArchivalObjectCatalog.Get(type).HardShakeReaction == ShakeReaction.Deferred)
                .Order(StringComparer.Ordinal)];

            Assert.Equal(
                ["OBarel", "OMagic"],
                deferred);
        }

        /// <summary>
        /// Verifies exactly the recovered <c>CNT_CMaskCallCanPushTh</c> carriers can be consumed by
        /// a push. Among placed types the bit reaches only the three eat-cell families, through
        /// <c>CNT_OrMaskCellSimpleEat</c>.
        /// </summary>
        [Fact]
        public void OnlyRecoveredEatCellsCanBePushedThrough()
        {
            Assert.Equal(
                ["CellsEat01", "DCellsEat01", "JCellsEat01"],
                CarriersOf(InteractionMask.CanBePushedThrough));
        }

        /// <summary>
        /// Verifies exactly the recovered <c>CNT_CMaskPlCanPushThEat</c> carriers consume eligible
        /// occupants of the cell they are pushed into.
        /// </summary>
        [Fact]
        public void OnlyRecoveredPushThroughEatTypesConsumeOccupants()
        {
            Assert.Equal(
                ["OBaloon", "OBubble", "OWeight"],
                CarriersOf(InteractionMask.PushesThroughOccupants));
        }

        /// <summary>
        /// Verifies exactly the recovered <c>CNT_CMaskObjVPush</c> carriers clear the gravity-axis
        /// push gate. The barrel carries the bit without the push-through-eat bit, so the recovered
        /// second gate still refuses it; the flag records the mask rather than the outcome.
        /// </summary>
        [Fact]
        public void OnlyRecoveredObjVPushTypesClearTheGravityAxisGate()
        {
            Assert.Equal(
                ["OBaloon", "OBarel", "OBubble"],
                CarriersOf(InteractionMask.GravityAxisPushable));
        }

        /// <summary>
        /// Verifies every placed collectible also locks its cell. The recovered
        /// <c>CNT_OrMaskObjEat</c> is <c>CellLockMotion | CellCanExplode | PlCanEat</c>, so a
        /// collectible supports a falling object rather than letting it pass.
        /// </summary>
        [Fact]
        public void EveryRecoveredCollectibleLocksItsCell()
        {
            string[] unlocked = [.. ArchivalObjectCatalog.Types
                .Where(type => ArchivalObjectCatalog.Get(type).Interactions
                    .HasFlag(InteractionMask.PlayerEdible))
                .Where(type => !ArchivalObjectCatalog.Get(type).Interactions
                    .HasFlag(InteractionMask.BlocksMovement))
                .Order(StringComparer.Ordinal)];

            Assert.Empty(unlocked);
        }

        /// <summary>Verifies recovered explosion reactions are explicit per type.</summary>
        /// <param name="archivalType">The archival type under test.</param>
        /// <param name="expected">The recovered <c>OnExpl</c> reaction, or <see langword="null"/>
        /// when the type does not carry <c>CNT_CMaskCellCanExplode</c>.</param>
        [Theory]
        [InlineData("ORock01", ExplosionReaction.Destroy)]
        [InlineData("OBubble", ExplosionReaction.Destroy)]
        [InlineData("OMagic", ExplosionReaction.Destroy)]
        [InlineData("CellsSolid01", ExplosionReaction.Destroy)]
        [InlineData("PipeHor", ExplosionReaction.Destroy)]
        [InlineData("OBottle", ExplosionReaction.Detonate3x3)]
        [InlineData("OBaloon", ExplosionReaction.Detonate3x3)]
        [InlineData("WBomb", ExplosionReaction.Detonate3x3)]
        [InlineData("OBarel", ExplosionReaction.Deferred)]
        [InlineData("OArrowL", ExplosionReaction.LaunchArrow)]
        [InlineData("CellsMetal01", null)]
        [InlineData("TonnelHor", null)]
        [InlineData("OKDDoor1", null)]
        [InlineData("OKDKey1", null)]
        [InlineData("OFire", null)]
        [InlineData("OWRotL", null)]
        [InlineData("OTable", null)]
        [InlineData("OTPort", null)]
        public void RecoveredExplosionReactionsAreExplicit(
            string archivalType,
            ExplosionReaction? expected)
        {
            Assert.Equal(expected, ArchivalObjectCatalog.Get(archivalType).ExplosionReaction);
        }

        /// <summary>
        /// Verifies the deferred explosion handlers are exactly the recovered specializations
        /// that remain: the barrel's cross blast.
        /// </summary>
        [Fact]
        public void DeferredExplosionReactionsAreTheKnownSpecializations()
        {
            string[] deferred = [.. ArchivalObjectCatalog.Types
                .Where(type => ArchivalObjectCatalog.Get(type).ExplosionReaction
                    == ExplosionReaction.Deferred)
                .Order(StringComparer.Ordinal)];

            Assert.Equal(
                ["OBarel"],
                deferred);
        }

        /// <summary>Gets every placed type carrying a capability, in ordinal order.</summary>
        private static string[] CarriersOf(InteractionMask capability)
        {
            return [.. ArchivalObjectCatalog.Types
                .Where(type => ArchivalObjectCatalog.Get(type).Interactions.HasFlag(capability))
                .Order(StringComparer.Ordinal)];
        }

        /// <summary>
        /// Verifies exactly the recovered <c>CNT_CMaskPartsCollide</c> carriers stop a flying
        /// entity. Every terrain cell family and every tunnel registers the bit; the pipes, which
        /// look like tunnels and are equally solid, do not.
        /// </summary>
        [Fact]
        public void OnlyRecoveredPartsCollideTypesBlockFlight()
        {
            Assert.Equal(
                [
                    "CellsEat01", "CellsMetal01", "CellsMetalBottom", "CellsMetalLeft",
                    "CellsMetalRight", "CellsMetalTop", "CellsSolid01", "CellsSolidPl",
                    "CellsSolidWd", "CellsWdBottom", "CellsWdLeft", "CellsWdRight", "CellsWdTop",
                    "DCellsEat01", "DCellsSolid01", "JCellsEat01", "JCellsSolid01",
                    "TonnelBottomLeft", "TonnelDB2T", "TonnelDL2R", "TonnelDR2L", "TonnelDT2B",
                    "TonnelHor", "TonnelLeftTop", "TonnelRightBottom", "TonnelTopRight",
                    "TonnelVert",
                ],
                CarriersOf(InteractionMask.BlocksFlight));
        }

        /// <summary>
        /// Pins the whole recovered bubble. <c>GameObjBubble</c> is a bare
        /// <c>CGameObjFallBase.CType</c> with no behavior class: <c>OnShake</c>,
        /// <c>OnShakeHard</c> and <c>OnExpl</c> are all <c>Game.ObjKill</c>, <c>bFly</c> keeps it
        /// from falling and <c>bFireKill</c> makes it destroy the fire it touches. Its descriptor
        /// is therefore complete by construction, and this keeps any one part from being dropped.
        /// </summary>
        [Fact]
        public void BubbleDescriptorIsTheWholeRecoveredType()
        {
            ArchivalObjectDescriptor bubble = ArchivalObjectCatalog.Get("OBubble");

            Assert.Equal(
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable
                    | InteractionMask.RoundSupport | InteractionMask.PushesThroughOccupants
                    | InteractionMask.GravityAxisPushable,
                bubble.Interactions);
            Assert.False(bubble.HasGravity);
            Assert.Equal(ShakeReaction.Destroy, bubble.ShakeReaction);
            Assert.Equal(ShakeReaction.Destroy, bubble.HardShakeReaction);
            Assert.Equal(ExplosionReaction.Destroy, bubble.ExplosionReaction);
            Assert.Equal(FireContact.MutualDestruction, bubble.FireContact);
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
