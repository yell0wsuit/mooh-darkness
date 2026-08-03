using System;
using System.Collections.Generic;

using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Shakes;

namespace Mooh.Core.Levels
{
    /// <summary>Provides the exhaustive runtime catalog for types placed in the authoritative level corpus.</summary>
    public static class ArchivalObjectCatalog
    {
        /// <summary>
        /// Matches the recovered collectible mask. <c>CNT_OrMaskObjEat</c> is
        /// <c>CellLockMotion | CellCanExplode | PlCanEat</c>, so a collectible locks its cell as
        /// well as being edible: a falling object rests on a coin instead of passing through it.
        /// The player never notices the lock, because the eat pass consumes the occupant before
        /// movement is planned.
        /// </summary>
        private const InteractionMask Collectible = InteractionMask.BlocksMovement
            | InteractionMask.PlayerEdible | InteractionMask.RoundSupport;

        private const InteractionMask FixedBlock = InteractionMask.BlocksMovement | InteractionMask.MovementLocked;

        /// <summary>
        /// Matches the recovered <c>CNT_CMaskPartsCollide</c> bit. Among placed types only the
        /// terrain cell families and the tunnels register it; the visually similar pipes do not,
        /// so a small beetle can fly through a pipe cell but not through a wall.
        /// </summary>
        private const InteractionMask Flightproof = InteractionMask.BlocksFlight;

        /// <summary>
        /// Matches the recovered <c>CNT_CMaskFlyThrought</c> bit. Exactly the twelve breakable
        /// terrain types carry it, so a flying arrow bores through earth, rock and wood trim and
        /// is stopped by immortal metal, by a pipe and by a tunnel alike.
        /// </summary>
        private const InteractionMask ArrowPermeable = InteractionMask.DestroyedByFlyingArrow;

        /// <summary>
        /// Matches the recovered edible-cell mask, which adds the player-eat and push-through bits
        /// to a motion-locked cell. The player consumes such a cell instead of being blocked by it,
        /// and a weight pushed into the cell in front consumes it as well.
        /// </summary>
        private const InteractionMask EdibleBlock = FixedBlock
            | InteractionMask.PlayerEdible | InteractionMask.CanBePushedThrough;

        private const InteractionMask PushableRound = InteractionMask.BlocksMovement
            | InteractionMask.PlayerPushable | InteractionMask.RoundSupport;

        /// <summary>
        /// Matches the recovered <c>CNT_CMaskObjVPush | CNT_CMaskPlCanPushThEat</c> pair. Only the
        /// balloon and the bubble carry both, which is what clears the two gravity-axis push gates
        /// in <c>GamePlayer.CtrlMove</c>.
        /// </summary>
        private const InteractionMask PushableAlongGravity = PushableRound
            | InteractionMask.GravityAxisPushable | InteractionMask.PushesThroughOccupants;

        private const InteractionMask BlueTarget = InteractionMask.BlueMonsterTarget;

        /// <summary>
        /// Matches the recovered monster mask, whose <c>CNT_CMaskLvlEndLock</c> bit keeps the level
        /// from completing while the monster is alive.
        /// </summary>
        private const InteractionMask LevelEndLockCarrier = InteractionMask.BlocksMovement
            | InteractionMask.LevelEndLock;

        private const InteractionMask WorldRotation = InteractionMask.BlocksMovement
            | InteractionMask.RoundSupport;

        /// <summary>
        /// Matches the recovered <c>CNT_CMaskShadowObj | CNT_CMaskMonShadow</c> pair the message
        /// table and the teleporter carry. Both bits are in <c>CNT_OrMaskShadow</c>, the world
        /// list's query filter, so such an occupant hides from monster navigation and from every
        /// ordinary cell-occupancy query alike.
        /// </summary>
        private const InteractionMask Shadow = InteractionMask.MonsterTransparent
            | InteractionMask.OccupancyTransparent;

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

            AddExplodableMany(result, EntityKind.DiggableTerrain,
                EdibleBlock | Flightproof | ArrowPermeable,
                "CellsEat01", "DCellsEat01", "JCellsEat01");
            AddExplodableMany(result, EntityKind.DestructibleTerrain,
                FixedBlock | Flightproof | ArrowPermeable,
                "CellsSolid01", "CellsSolidPl", "CellsSolidWd", "DCellsSolid01", "JCellsSolid01");
            AddMany(result, EntityKind.MetalTerrain,
                FixedBlock | InteractionMask.RoundSupport | Flightproof,
                "CellsMetal01", "CellsMetalLeft", "CellsMetalTop", "CellsMetalRight", "CellsMetalBottom");
            AddExplodableMany(result, EntityKind.WoodTerrain,
                FixedBlock | Flightproof | ArrowPermeable,
                "CellsWdLeft", "CellsWdTop", "CellsWdRight", "CellsWdBottom");

            Add(result, "OPlayer", EntityKind.Player, InteractionMask.BlocksMovement);
            AddDirectional(result, "MGr", EntityKind.GreenMonster, LevelEndLockCarrier,
                explosionReaction: ExplosionReaction.Destroy,
                shakeReaction: ShakeReaction.Detonate3x3);
            AddDirectional(result, "MBlue", EntityKind.BlueMonster, LevelEndLockCarrier,
                explosionReaction: ExplosionReaction.Destroy,
                shakeReaction: ShakeReaction.Detonate3x3);
            Add(result, "MMBio", EntityKind.BioMass, InteractionMask.BlocksMovement | BlueTarget,
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
            Add(result, "OBottle", EntityKind.Bottle, PushableRound | BlueTarget, hasGravity: true,
                explosionReaction: ExplosionReaction.Detonate3x3,
                detonatesOnLanding: true,
                shakeReaction: ShakeReaction.Detonate3x3);
            Add(result, "OBarel", EntityKind.Barrel,
                PushableRound | InteractionMask.GravityAxisPushable | BlueTarget,
                hasGravity: true,
                explosionReaction: ExplosionReaction.Deferred,
                shakeReaction: ShakeReaction.Deferred);
            Add(result, "OBaloon", EntityKind.Balloon, PushableAlongGravity | BlueTarget,
                explosionReaction: ExplosionReaction.Detonate3x3,
                shakeReaction: ShakeReaction.Detonate3x3);
            // The bubble is the only placed carrier of the recovered bFireKill flag, so it is the
            // only object that destroys the fire it touches instead of merely being consumed.
            Add(result, "OBubble", EntityKind.Bubble, PushableAlongGravity,
                explosionReaction: ExplosionReaction.Destroy,
                shakeReaction: ShakeReaction.Destroy,
                fireContact: FireContact.MutualDestruction);
            AddExplodableVariants(result, EntityKind.Coin, Collectible, true,
                "OCoin01", "OCoin02", "OCoin03");
            AddExplodableVariants(result, EntityKind.Diamond, Collectible, true,
                "ODiam01", "ODiam02", "ODiam03", "ODiam04", "ODiam05");
            AddExplodableVariants(result, EntityKind.Food, Collectible, true,
                "OEat01", "OEat02", "OEat03");
            Add(result, "OMagic", EntityKind.MagicGem, PushableRound | BlueTarget, hasGravity: true,
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
            AddVariants(result, EntityKind.Key, Collectible, true,
                "OKDKey1", "OKDKey2", "OKDKey3");
            AddVariants(result, EntityKind.KeyDoor, FixedBlock | InteractionMask.RoundSupport,
                false,
                "OKDDoor1", "OKDDoor2", "OKDDoor3");
            // Fire carries CNT_CMaskShadowObj | CNT_CMaskRoundCap, so it hides from ordinary cell
            // queries but keeps CNT_CMaskMonShadow clear and still blocks monster navigation.
            // Fire is also the only placed type setting bSecondSlideMask, which is what lets it
            // rest on another shadow occupant instead of falling straight through it.
            Add(result, "OFire", EntityKind.Fire,
                InteractionMask.RoundSupport | InteractionMask.OccupancyTransparent,
                hasGravity: true,
                fireContact: FireContact.Source,
                treatsShadowAsSupport: true);
            // The four arrows name one handler for five events, so a landing, either shake
            // strength, a blast and a hand strike all launch them in their own fixed direction.
            AddDirectional(result, "OArrow", EntityKind.FlyingArrow, PushableRound | BlueTarget,
                hasGravity: true,
                explosionReaction: ExplosionReaction.LaunchArrow,
                shakeReaction: ShakeReaction.LaunchArrow);
            Add(result, "OWRotL", EntityKind.WorldRotation, WorldRotation, variant: -1);
            Add(result, "OWRotR", EntityKind.WorldRotation, WorldRotation, variant: 1);
            Add(result, "OTPort", EntityKind.Teleporter, Shadow);
            Add(result, "OTable", EntityKind.MessageTable, Shadow);

            AddExplodableMany(result, EntityKind.Pipe, FixedBlock | InteractionMask.RoundSupport,
                "PipeHor", "PipeVert", "PipeLeftTop", "PipeRightBottom", "PipeTopRight", "PipeBottomLeft");
            AddTunnel(result, "TonnelHor", TunnelShape.Horizontal);
            AddTunnel(result, "TonnelVert", TunnelShape.Vertical);
            AddTunnel(result, "TonnelLeftTop", TunnelShape.LeftTop);
            AddTunnel(result, "TonnelRightBottom", TunnelShape.RightBottom);
            AddTunnel(result, "TonnelTopRight", TunnelShape.TopRight);
            AddTunnel(result, "TonnelBottomLeft", TunnelShape.BottomLeft);
            AddTunnel(result, "TonnelDL2R", TunnelShape.LeftToRight);
            AddTunnel(result, "TonnelDR2L", TunnelShape.RightToLeft);
            AddTunnel(result, "TonnelDT2B", TunnelShape.TopToBottom);
            AddTunnel(result, "TonnelDB2T", TunnelShape.BottomToTop);

            AddLandingDetonator(result, "WBomb", EntityKind.Grenade, Collectible | BlueTarget, variant: 1);
            AddLandingDetonator(result, "WGrBox", EntityKind.GrenadeBox, Collectible | BlueTarget, variant: 5);
            AddLandingDetonator(result, "WROne", EntityKind.Rocket, Collectible | BlueTarget, variant: 1);
            AddLandingDetonator(result, "WRBox", EntityKind.RocketBox, Collectible | BlueTarget, variant: 5);
            // The deployed mine carries CNT_CMaskShadowWeap | CNT_CMaskMonShadow instead of
            // CNT_CMaskShadowObj, but both bits are in CNT_OrMaskShadow all the same.
            Add(result, "WMine", EntityKind.Mine, Shadow,
                explosionReaction: ExplosionReaction.Detonate3x3,
                shakeReaction: ShakeReaction.Detonate3x3);
            AddLandingDetonator(result, "WMinesBox", EntityKind.MineBox, Collectible | BlueTarget, variant: 5);
            AddLandingDetonator(result, "WBBomb", EntityKind.BigBomb, Collectible | BlueTarget, variant: 1);

            return result;
        }

        /// <summary>
        /// Adds one tunnel cell. Every <c>Tonnel*</c> type shares the mask
        /// <c>CGameCellPipes.__init__</c> assigns and differs only in its routing tables.
        /// </summary>
        private static void AddTunnel(
            IDictionary<string, ArchivalObjectDescriptor> result,
            string archivalType,
            TunnelShape shape)
        {
            Add(result, archivalType, EntityKind.Tunnel,
                FixedBlock | InteractionMask.RoundSupport | Flightproof,
                tunnelShape: shape);
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
            ShakeReaction? hardShakeReaction = null,
            FireContact? fireContact = null,
            bool treatsShadowAsSupport = false,
            TunnelShape? tunnelShape = null)
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
                    hardShakeReaction ?? shakeReaction,
                    fireContact,
                    treatsShadowAsSupport,
                    tunnelShape));
        }
    }
}
