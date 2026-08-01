using System;
using System.Collections.Generic;

using Mooh.Core.Interactions;

namespace Mooh.Core.Legacy
{
    /// <summary>Provides the exhaustive runtime catalog for types placed in the authoritative level corpus.</summary>
    public static class LegacyObjectCatalog
    {
        private const InteractionMask Collectible = InteractionMask.PlayerEdible | InteractionMask.RoundSupport;
        private const InteractionMask FixedBlock = InteractionMask.BlocksMovement | InteractionMask.MovementLocked;
        private const InteractionMask PushableRound = InteractionMask.BlocksMovement
            | InteractionMask.PlayerPushable | InteractionMask.RoundSupport;

        private static readonly Dictionary<string, LegacyObjectDescriptor> Descriptors = CreateDescriptors();
        private static readonly IReadOnlyCollection<string> TypeNames = [.. Descriptors.Keys];

        /// <summary>Gets every supported exact archival type name.</summary>
        public static IReadOnlyCollection<string> Types => TypeNames;

        /// <summary>Gets the descriptor for an exact archival type name.</summary>
        /// <param name="legacyType">The case-sensitive archival type name.</param>
        /// <returns>The corresponding explicit descriptor.</returns>
        /// <exception cref="ArgumentException"><paramref name="legacyType"/> is blank.</exception>
        /// <exception cref="KeyNotFoundException">The type is not in the placed-type catalog.</exception>
        public static LegacyObjectDescriptor Get(string legacyType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(legacyType);
            return Descriptors.TryGetValue(legacyType, out LegacyObjectDescriptor? descriptor)
                ? descriptor
                : throw new KeyNotFoundException(
                    $"Legacy object type '{legacyType}' is not supported by the runtime catalog.");
        }

#pragma warning disable IDE0028 // Helper methods keep the 85-entry catalog grouped by runtime family.
        private static Dictionary<string, LegacyObjectDescriptor> CreateDescriptors()
        {
            Dictionary<string, LegacyObjectDescriptor> result = new(StringComparer.Ordinal);

            AddMany(result, LegacyEntityKind.DiggableTerrain, FixedBlock,
                "CellsEat01", "DCellsEat01", "JCellsEat01");
            AddMany(result, LegacyEntityKind.DestructibleTerrain, FixedBlock,
                "CellsSolid01", "CellsSolidPl", "CellsSolidWd", "DCellsSolid01", "JCellsSolid01");
            AddMany(result, LegacyEntityKind.MetalTerrain, FixedBlock | InteractionMask.RoundSupport,
                "CellsMetal01", "CellsMetalLeft", "CellsMetalTop", "CellsMetalRight", "CellsMetalBottom");
            AddMany(result, LegacyEntityKind.WoodTerrain, FixedBlock,
                "CellsWdLeft", "CellsWdTop", "CellsWdRight", "CellsWdBottom");

            Add(result, "OPlayer", LegacyEntityKind.Player, InteractionMask.BlocksMovement);
            AddDirectional(result, "MGr", LegacyEntityKind.GreenMonster, InteractionMask.BlocksMovement);
            AddDirectional(result, "MBlue", LegacyEntityKind.BlueMonster, InteractionMask.BlocksMovement);
            Add(result, "MMBio", LegacyEntityKind.BioMass, InteractionMask.BlocksMovement);

            Add(result, "ORock01", LegacyEntityKind.Rock, PushableRound, hasGravity: true);
            Add(result, "OWeight", LegacyEntityKind.Weight,
                PushableRound | InteractionMask.PushesThroughOccupants,
                hasGravity: true,
                fallSpeedMultiplier: 2);
            Add(result, "OBottle", LegacyEntityKind.Bottle, PushableRound, hasGravity: true);
            Add(result, "OBarel", LegacyEntityKind.Barrel, PushableRound, hasGravity: true);
            Add(result, "OBaloon", LegacyEntityKind.Balloon,
                PushableRound | InteractionMask.CanBePushedThrough);
            Add(result, "OBubble", LegacyEntityKind.Bubble,
                PushableRound | InteractionMask.CanBePushedThrough);
            AddVariants(result, LegacyEntityKind.Coin, Collectible, true, "OCoin01", "OCoin02", "OCoin03");
            AddVariants(result, LegacyEntityKind.Diamond, Collectible, true,
                "ODiam01", "ODiam02", "ODiam03", "ODiam04", "ODiam05");
            AddVariants(result, LegacyEntityKind.Food, Collectible, true, "OEat01", "OEat02", "OEat03");
            Add(result, "OMagic", LegacyEntityKind.MagicGem, PushableRound, hasGravity: true);
            Add(result, "BCola", LegacyEntityKind.Cola, Collectible, hasGravity: true);
            Add(result, "BIce", LegacyEntityKind.Ice, Collectible, hasGravity: true);
            AddVariants(result, LegacyEntityKind.Key,
                InteractionMask.BlocksMovement | Collectible,
                true,
                "OKDKey1", "OKDKey2", "OKDKey3");
            AddVariants(result, LegacyEntityKind.KeyDoor, FixedBlock | InteractionMask.RoundSupport,
                false,
                "OKDDoor1", "OKDDoor2", "OKDDoor3");
            Add(result, "OFire", LegacyEntityKind.Fire, InteractionMask.RoundSupport, hasGravity: true);
            AddDirectional(result, "OArrow", LegacyEntityKind.GravityArrow, PushableRound, hasGravity: true);
            Add(result, "OWRotL", LegacyEntityKind.WorldRotation, InteractionMask.RoundSupport, variant: -1);
            Add(result, "OWRotR", LegacyEntityKind.WorldRotation, InteractionMask.RoundSupport, variant: 1);
            Add(result, "OTPort", LegacyEntityKind.Teleporter, InteractionMask.None);
            Add(result, "OTable", LegacyEntityKind.MessageTable, InteractionMask.None);

            AddMany(result, LegacyEntityKind.Pipe, FixedBlock | InteractionMask.RoundSupport,
                "PipeHor", "PipeVert", "PipeLeftTop", "PipeRightBottom", "PipeTopRight", "PipeBottomLeft");
            AddMany(result, LegacyEntityKind.Tunnel, FixedBlock | InteractionMask.RoundSupport,
                "TonnelHor", "TonnelVert", "TonnelLeftTop", "TonnelRightBottom", "TonnelTopRight",
                "TonnelBottomLeft", "TonnelDL2R", "TonnelDR2L", "TonnelDT2B", "TonnelDB2T");

            Add(result, "WBomb", LegacyEntityKind.Grenade, Collectible, hasGravity: true, variant: 1);
            Add(result, "WGrBox", LegacyEntityKind.GrenadeBox, Collectible, hasGravity: true, variant: 5);
            Add(result, "WROne", LegacyEntityKind.Rocket, Collectible, hasGravity: true, variant: 1);
            Add(result, "WRBox", LegacyEntityKind.RocketBox, Collectible, hasGravity: true, variant: 5);
            Add(result, "WMine", LegacyEntityKind.Mine, InteractionMask.None);
            Add(result, "WMinesBox", LegacyEntityKind.MineBox, Collectible, hasGravity: true, variant: 5);
            Add(result, "WBBomb", LegacyEntityKind.BigBomb, Collectible, hasGravity: true, variant: 1);

            return result;
        }
#pragma warning restore IDE0028

        private static void AddDirectional(
            IDictionary<string, LegacyObjectDescriptor> result,
            string prefix,
            LegacyEntityKind kind,
            InteractionMask interactions,
            bool hasGravity = false)
        {
            Add(result, prefix + "L", kind, interactions, hasGravity, variant: 0);
            Add(result, prefix + "T", kind, interactions, hasGravity, variant: 1);
            Add(result, prefix + "R", kind, interactions, hasGravity, variant: 2);
            Add(result, prefix + "B", kind, interactions, hasGravity, variant: 3);
        }

        private static void AddVariants(
            IDictionary<string, LegacyObjectDescriptor> result,
            LegacyEntityKind kind,
            InteractionMask interactions,
            bool hasGravity,
            params string[] legacyTypes)
        {
            for (int index = 0; index < legacyTypes.Length; index++)
            {
                Add(result, legacyTypes[index], kind, interactions, hasGravity, variant: index + 1);
            }
        }

        private static void AddMany(
            IDictionary<string, LegacyObjectDescriptor> result,
            LegacyEntityKind kind,
            InteractionMask interactions,
            params string[] legacyTypes)
        {
            foreach (string legacyType in legacyTypes)
            {
                Add(result, legacyType, kind, interactions);
            }
        }

        private static void Add(
            IDictionary<string, LegacyObjectDescriptor> result,
            string legacyType,
            LegacyEntityKind kind,
            InteractionMask interactions,
            bool hasGravity = false,
            double fallSpeedMultiplier = 1,
            int variant = 0)
        {
            result.Add(
                legacyType,
                new LegacyObjectDescriptor(
                    legacyType,
                    kind,
                    interactions,
                    hasGravity,
                    fallSpeedMultiplier,
                    variant));
        }
    }
}
