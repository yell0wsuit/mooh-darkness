using System;
using System.Collections.Generic;

using Mooh.Core.Interactions;

namespace Mooh.Core.Levels
{
    /// <summary>Provides the exhaustive runtime catalog for types placed in the authoritative level corpus.</summary>
    public static class ArchivalObjectCatalog
    {
        private const InteractionMask Collectible = InteractionMask.PlayerEdible | InteractionMask.RoundSupport;
        private const InteractionMask FixedBlock = InteractionMask.BlocksMovement | InteractionMask.MovementLocked;
        private const InteractionMask PushableRound = InteractionMask.BlocksMovement
            | InteractionMask.PlayerPushable | InteractionMask.RoundSupport;

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

            AddMany(result, EntityKind.DiggableTerrain, FixedBlock,
                "CellsEat01", "DCellsEat01", "JCellsEat01");
            AddMany(result, EntityKind.DestructibleTerrain, FixedBlock,
                "CellsSolid01", "CellsSolidPl", "CellsSolidWd", "DCellsSolid01", "JCellsSolid01");
            AddMany(result, EntityKind.MetalTerrain, FixedBlock | InteractionMask.RoundSupport,
                "CellsMetal01", "CellsMetalLeft", "CellsMetalTop", "CellsMetalRight", "CellsMetalBottom");
            AddMany(result, EntityKind.WoodTerrain, FixedBlock,
                "CellsWdLeft", "CellsWdTop", "CellsWdRight", "CellsWdBottom");

            Add(result, "OPlayer", EntityKind.Player, InteractionMask.BlocksMovement);
            AddDirectional(result, "MGr", EntityKind.GreenMonster, InteractionMask.BlocksMovement);
            AddDirectional(result, "MBlue", EntityKind.BlueMonster, InteractionMask.BlocksMovement);
            Add(result, "MMBio", EntityKind.BioMass, InteractionMask.BlocksMovement);

            Add(result, "ORock01", EntityKind.Rock, PushableRound, hasGravity: true);
            Add(result, "OWeight", EntityKind.Weight,
                PushableRound | InteractionMask.PushesThroughOccupants,
                hasGravity: true,
                fallSpeedMultiplier: 2);
            Add(result, "OBottle", EntityKind.Bottle, PushableRound, hasGravity: true);
            Add(result, "OBarel", EntityKind.Barrel, PushableRound, hasGravity: true);
            Add(result, "OBaloon", EntityKind.Balloon,
                PushableRound | InteractionMask.CanBePushedThrough);
            Add(result, "OBubble", EntityKind.Bubble,
                PushableRound | InteractionMask.CanBePushedThrough);
            AddVariants(result, EntityKind.Coin, Collectible, true, "OCoin01", "OCoin02", "OCoin03");
            AddVariants(result, EntityKind.Diamond, Collectible, true,
                "ODiam01", "ODiam02", "ODiam03", "ODiam04", "ODiam05");
            AddVariants(result, EntityKind.Food, Collectible, true, "OEat01", "OEat02", "OEat03");
            Add(result, "OMagic", EntityKind.MagicGem, PushableRound, hasGravity: true);
            Add(result, "BCola", EntityKind.Cola, Collectible, hasGravity: true);
            Add(result, "BIce", EntityKind.Ice, Collectible, hasGravity: true);
            AddVariants(result, EntityKind.Key,
                InteractionMask.BlocksMovement | Collectible,
                true,
                "OKDKey1", "OKDKey2", "OKDKey3");
            AddVariants(result, EntityKind.KeyDoor, FixedBlock | InteractionMask.RoundSupport,
                false,
                "OKDDoor1", "OKDDoor2", "OKDDoor3");
            Add(result, "OFire", EntityKind.Fire, InteractionMask.RoundSupport, hasGravity: true);
            AddDirectional(result, "OArrow", EntityKind.GravityArrow, PushableRound, hasGravity: true);
            Add(result, "OWRotL", EntityKind.WorldRotation, InteractionMask.RoundSupport, variant: -1);
            Add(result, "OWRotR", EntityKind.WorldRotation, InteractionMask.RoundSupport, variant: 1);
            Add(result, "OTPort", EntityKind.Teleporter, InteractionMask.None);
            Add(result, "OTable", EntityKind.MessageTable, InteractionMask.None);

            AddMany(result, EntityKind.Pipe, FixedBlock | InteractionMask.RoundSupport,
                "PipeHor", "PipeVert", "PipeLeftTop", "PipeRightBottom", "PipeTopRight", "PipeBottomLeft");
            AddMany(result, EntityKind.Tunnel, FixedBlock | InteractionMask.RoundSupport,
                "TonnelHor", "TonnelVert", "TonnelLeftTop", "TonnelRightBottom", "TonnelTopRight",
                "TonnelBottomLeft", "TonnelDL2R", "TonnelDR2L", "TonnelDT2B", "TonnelDB2T");

            Add(result, "WBomb", EntityKind.Grenade, Collectible, hasGravity: true, variant: 1);
            Add(result, "WGrBox", EntityKind.GrenadeBox, Collectible, hasGravity: true, variant: 5);
            Add(result, "WROne", EntityKind.Rocket, Collectible, hasGravity: true, variant: 1);
            Add(result, "WRBox", EntityKind.RocketBox, Collectible, hasGravity: true, variant: 5);
            Add(result, "WMine", EntityKind.Mine, InteractionMask.None);
            Add(result, "WMinesBox", EntityKind.MineBox, Collectible, hasGravity: true, variant: 5);
            Add(result, "WBBomb", EntityKind.BigBomb, Collectible, hasGravity: true, variant: 1);

            return result;
        }

        private static void AddDirectional(
            IDictionary<string, ArchivalObjectDescriptor> result,
            string prefix,
            EntityKind kind,
            InteractionMask interactions,
            bool hasGravity = false)
        {
            Add(result, prefix + "L", kind, interactions, hasGravity, variant: 0);
            Add(result, prefix + "T", kind, interactions, hasGravity, variant: 1);
            Add(result, prefix + "R", kind, interactions, hasGravity, variant: 2);
            Add(result, prefix + "B", kind, interactions, hasGravity, variant: 3);
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

        private static void Add(
            IDictionary<string, ArchivalObjectDescriptor> result,
            string archivalType,
            EntityKind kind,
            InteractionMask interactions,
            bool hasGravity = false,
            double fallSpeedMultiplier = 1,
            int variant = 0)
        {
            result.Add(
                archivalType,
                new ArchivalObjectDescriptor(
                    archivalType,
                    kind,
                    interactions,
                    hasGravity,
                    fallSpeedMultiplier,
                    variant));
        }
    }
}
