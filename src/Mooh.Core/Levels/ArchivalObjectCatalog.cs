using System;
using System.Collections.Generic;

using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Interactions;
using Mooh.Core.Shakes;

namespace Mooh.Core.Levels
{
    /// <summary>Provides the exhaustive runtime catalog for types placed in the authoritative level corpus.</summary>
    public static class ArchivalObjectCatalog
    {
        private const InteractionMask Collectible = InteractionMask.PlayerEdible | InteractionMask.RoundSupport;
        private const InteractionMask FixedBlock = InteractionMask.BlocksMovement | InteractionMask.MovementLocked;
        private const InteractionMask PushableRound = InteractionMask.BlocksMovement
            | InteractionMask.PlayerPushable | InteractionMask.RoundSupport;
        private const InteractionMask WorldRotation = InteractionMask.BlocksMovement
            | InteractionMask.RoundSupport;

        private static readonly Dictionary<string, ArchivalObjectDescriptor> Descriptors = CreateDescriptors();

        /// <summary>Gets every supported exact archival type name.</summary>
        public static IReadOnlyCollection<string> Types { get; } = [.. Descriptors.Keys];

        /// <summary>Gets the descriptor for an exact archival type name.</summary>
        /// <param name="archivalType">The case-sensitive archival type name.</param>
        /// <returns>The corresponding explicit descriptor.</returns>
        /// <exception cref="ArgumentException"><paramref name="archivalType"/> is blank.</exception>
        /// <exception cref="KeyNotFoundException">The type is not in the placed-type catalog.</exception>
        public static ArchivalObjectDescriptor Get(string archivalType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(archivalType);
            return Descriptors.TryGetValue(archivalType, out ArchivalObjectDescriptor? descriptor)
                ? descriptor
                : throw new KeyNotFoundException(
                    $"Archival object type '{archivalType}' is not supported by the runtime catalog.");
        }

        private static Dictionary<string, ArchivalObjectDescriptor> CreateDescriptors()
        {
            Dictionary<string, ArchivalObjectDescriptor> result = [];

            AddExplodableMany(result, EntityKind.DiggableTerrain, FixedBlock,
                "CellsEat01", "DCellsEat01", "JCellsEat01");
            AddExplodableMany(result, EntityKind.DestructibleTerrain, FixedBlock,
                "CellsSolid01", "CellsSolidPl", "CellsSolidWd", "DCellsSolid01", "JCellsSolid01");
            AddMany(result, EntityKind.MetalTerrain, FixedBlock | InteractionMask.RoundSupport,
                "CellsMetal01", "CellsMetalLeft", "CellsMetalTop", "CellsMetalRight", "CellsMetalBottom");
            AddExplodableMany(result, EntityKind.WoodTerrain, FixedBlock,
                "CellsWdLeft", "CellsWdTop", "CellsWdRight", "CellsWdBottom");

            Add(result, "OPlayer", EntityKind.Player, InteractionMask.BlocksMovement);
            AddDirectional(result, "MGr", EntityKind.GreenMonster, InteractionMask.BlocksMovement,
                explosionReaction: ExplosionReaction.Destroy,
                shakeReaction: ShakeReaction.Detonate3x3);
            AddDirectional(result, "MBlue", EntityKind.BlueMonster, InteractionMask.BlocksMovement,
                explosionReaction: ExplosionReaction.Destroy,
                shakeReaction: ShakeReaction.Detonate3x3);
            Add(result, "MMBio", EntityKind.BioMass, InteractionMask.BlocksMovement,
                explosionReaction: ExplosionReaction.Destroy,
                shakeReaction: ShakeReaction.Detonate3x3);

            Add(result, "ORock01", EntityKind.Rock, PushableRound, hasGravity: true,
                explosionReaction: ExplosionReaction.Destroy);
            Add(result, "OWeight", EntityKind.Weight,
                PushableRound | InteractionMask.PushesThroughOccupants,
                hasGravity: true,
                fallSpeedMultiplier: 2,
                impactStrength: GravityImpactStrength.Hard,
                explosionReaction: ExplosionReaction.Destroy);
            Add(result, "OBottle", EntityKind.Bottle, PushableRound, hasGravity: true,
                explosionReaction: ExplosionReaction.Detonate3x3,
                detonatesOnLanding: true,
                shakeReaction: ShakeReaction.Detonate3x3);
            Add(result, "OBarel", EntityKind.Barrel, PushableRound, hasGravity: true,
                shakeReaction: ShakeReaction.Deferred);
            Add(result, "OBaloon", EntityKind.Balloon,
                PushableRound | InteractionMask.CanBePushedThrough,
                explosionReaction: ExplosionReaction.Detonate3x3,
                shakeReaction: ShakeReaction.Detonate3x3);
            Add(result, "OBubble", EntityKind.Bubble,
                PushableRound | InteractionMask.CanBePushedThrough,
                explosionReaction: ExplosionReaction.Destroy,
                shakeReaction: ShakeReaction.Destroy);
            AddExplodableVariants(result, EntityKind.Coin, Collectible, true,
                "OCoin01", "OCoin02", "OCoin03");
            AddExplodableVariants(result, EntityKind.Diamond, Collectible, true,
                "ODiam01", "ODiam02", "ODiam03", "ODiam04", "ODiam05");
            AddExplodableVariants(result, EntityKind.Food, Collectible, true,
                "OEat01", "OEat02", "OEat03");
            Add(result, "OMagic", EntityKind.MagicGem, PushableRound, hasGravity: true,
                explosionReaction: ExplosionReaction.Destroy,
                shakeReaction: ShakeReaction.Deferred);
            Add(result, "BCola", EntityKind.Cola, Collectible, hasGravity: true,
                explosionReaction: ExplosionReaction.Destroy,
                detonatesOnLanding: true,
                shakeReaction: ShakeReaction.Detonate3x3);
            Add(result, "BIce", EntityKind.Ice, Collectible, hasGravity: true,
                explosionReaction: ExplosionReaction.Destroy,
                detonatesOnLanding: true,
                shakeReaction: ShakeReaction.Detonate3x3);
            AddVariants(result, EntityKind.Key,
                InteractionMask.BlocksMovement | Collectible,
                true,
                "OKDKey1", "OKDKey2", "OKDKey3");
            AddVariants(result, EntityKind.KeyDoor, FixedBlock | InteractionMask.RoundSupport,
                false,
                "OKDDoor1", "OKDDoor2", "OKDDoor3");
            Add(result, "OFire", EntityKind.Fire, InteractionMask.RoundSupport, hasGravity: true);
            AddDirectional(result, "OArrow", EntityKind.GravityArrow, PushableRound, hasGravity: true,
                shakeReaction: ShakeReaction.Deferred);
            Add(result, "OWRotL", EntityKind.WorldRotation, WorldRotation, variant: -1);
            Add(result, "OWRotR", EntityKind.WorldRotation, WorldRotation, variant: 1);
            Add(result, "OTPort", EntityKind.Teleporter, InteractionMask.MonsterTransparent);
            Add(result, "OTable", EntityKind.MessageTable, InteractionMask.MonsterTransparent);

            AddExplodableMany(result, EntityKind.Pipe, FixedBlock | InteractionMask.RoundSupport,
                "PipeHor", "PipeVert", "PipeLeftTop", "PipeRightBottom", "PipeTopRight", "PipeBottomLeft");
            AddMany(result, EntityKind.Tunnel, FixedBlock | InteractionMask.RoundSupport,
                "TonnelHor", "TonnelVert", "TonnelLeftTop", "TonnelRightBottom", "TonnelTopRight",
                "TonnelBottomLeft", "TonnelDL2R", "TonnelDR2L", "TonnelDT2B", "TonnelDB2T");

            AddLandingDetonator(result, "WBomb", EntityKind.Grenade, Collectible, variant: 1);
            AddLandingDetonator(result, "WGrBox", EntityKind.GrenadeBox, Collectible, variant: 5);
            AddLandingDetonator(result, "WROne", EntityKind.Rocket, Collectible, variant: 1);
            AddLandingDetonator(result, "WRBox", EntityKind.RocketBox, Collectible, variant: 5);
            Add(result, "WMine", EntityKind.Mine, InteractionMask.MonsterTransparent,
                explosionReaction: ExplosionReaction.Detonate3x3,
                shakeReaction: ShakeReaction.Detonate3x3);
            AddLandingDetonator(result, "WMinesBox", EntityKind.MineBox, Collectible, variant: 5);
            AddLandingDetonator(result, "WBBomb", EntityKind.BigBomb, Collectible, variant: 1);

            return result;
        }

        private static void AddDirectional(
            IDictionary<string, ArchivalObjectDescriptor> result,
            string prefix,
            EntityKind kind,
            InteractionMask interactions,
            bool hasGravity = false,
            ExplosionReaction? explosionReaction = null,
            ShakeReaction shakeReaction = ShakeReaction.None)
        {
            Add(result, prefix + "L", kind, interactions, hasGravity, variant: 0,
                explosionReaction: explosionReaction, shakeReaction: shakeReaction);
            Add(result, prefix + "T", kind, interactions, hasGravity, variant: 1,
                explosionReaction: explosionReaction, shakeReaction: shakeReaction);
            Add(result, prefix + "R", kind, interactions, hasGravity, variant: 2,
                explosionReaction: explosionReaction, shakeReaction: shakeReaction);
            Add(result, prefix + "B", kind, interactions, hasGravity, variant: 3,
                explosionReaction: explosionReaction, shakeReaction: shakeReaction);
        }

        private static void AddVariants(
            IDictionary<string, ArchivalObjectDescriptor> result,
            EntityKind kind,
            InteractionMask interactions,
            bool hasGravity,
            params string[] archivalTypes)
        {
            for (int index = 0; index < archivalTypes.Length; index++)
            {
                Add(result, archivalTypes[index], kind, interactions, hasGravity, variant: index + 1);
            }
        }

        private static void AddMany(
            IDictionary<string, ArchivalObjectDescriptor> result,
            EntityKind kind,
            InteractionMask interactions,
            params string[] archivalTypes)
        {
            foreach (string archivalType in archivalTypes)
            {
                Add(result, archivalType, kind, interactions);
            }
        }

        private static void AddExplodableMany(
            IDictionary<string, ArchivalObjectDescriptor> result,
            EntityKind kind,
            InteractionMask interactions,
            params string[] archivalTypes)
        {
            foreach (string archivalType in archivalTypes)
            {
                Add(result, archivalType, kind, interactions,
                    explosionReaction: ExplosionReaction.Destroy);
            }
        }

        /// <summary>
        /// Adds numbered treasure or food variants. The recovered types define
        /// <c>OnShakeHard</c> as <c>ObjKill</c> and no ordinary <c>OnShake</c>, so only a hard
        /// landing destroys them.
        /// </summary>
        private static void AddExplodableVariants(
            IDictionary<string, ArchivalObjectDescriptor> result,
            EntityKind kind,
            InteractionMask interactions,
            bool hasGravity,
            params string[] archivalTypes)
        {
            for (int index = 0; index < archivalTypes.Length; index++)
            {
                Add(result, archivalTypes[index], kind, interactions, hasGravity,
                    variant: index + 1,
                    explosionReaction: ExplosionReaction.Destroy,
                    shakeReaction: ShakeReaction.None,
                    hardShakeReaction: ShakeReaction.Destroy);
            }
        }

        private static void AddLandingDetonator(
            IDictionary<string, ArchivalObjectDescriptor> result,
            string archivalType,
            EntityKind kind,
            InteractionMask interactions,
            int variant)
        {
            Add(result, archivalType, kind, interactions, hasGravity: true, variant: variant,
                explosionReaction: ExplosionReaction.Detonate3x3,
                detonatesOnLanding: true,
                shakeReaction: ShakeReaction.Detonate3x3);
        }

        private static void Add(
            IDictionary<string, ArchivalObjectDescriptor> result,
            string archivalType,
            EntityKind kind,
            InteractionMask interactions,
            bool hasGravity = false,
            double fallSpeedMultiplier = 1,
            int variant = 0,
            GravityImpactStrength impactStrength = GravityImpactStrength.Ordinary,
            ExplosionReaction? explosionReaction = null,
            bool detonatesOnLanding = false,
            ShakeReaction shakeReaction = ShakeReaction.None,
            ShakeReaction? hardShakeReaction = null)
        {
            result.Add(
                archivalType,
                new ArchivalObjectDescriptor(
                    archivalType,
                    kind,
                    interactions,
                    hasGravity,
                    fallSpeedMultiplier,
                    variant,
                    impactStrength,
                    explosionReaction,
                    detonatesOnLanding,
                    shakeReaction,
                    hardShakeReaction ?? shakeReaction));
        }
    }
}
