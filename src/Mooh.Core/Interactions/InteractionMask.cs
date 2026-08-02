using System;

namespace Mooh.Core.Interactions
{
    /// <summary>
    /// Defines composable interaction capabilities for an entity.
    /// </summary>
    [Flags]
    public enum InteractionMask
    {
        /// <summary>The entity has no special interaction behavior.</summary>
        None = 0,

        /// <summary>The entity prevents another entity from entering its cell.</summary>
        BlocksMovement = 1 << 0,

        /// <summary>The entity can be pushed by the player.</summary>
        PlayerPushable = 1 << 1,

        /// <summary>The entity can pass through an otherwise push-blocking occupant.</summary>
        CanBePushedThrough = 1 << 2,

        /// <summary>The entity can be consumed by the player.</summary>
        PlayerEdible = 1 << 3,

        /// <summary>The entity cannot move or be pushed.</summary>
        MovementLocked = 1 << 4,

        /// <summary>The entity gives its cell a rounded cap that can support sliding objects.</summary>
        RoundSupport = 1 << 5,

        /// <summary>The entity permits player pushing along the current gravity axis.</summary>
        GravityAxisPushable = 1 << 6,

        /// <summary>The pushed entity can consume eligible occupants in its destination cell.</summary>
        PushesThroughOccupants = 1 << 7,

        /// <summary>Monster navigation ignores the entity instead of treating its cell as occupied.</summary>
        MonsterTransparent = 1 << 8,

        /// <summary>A blue monster treats the entity as a strike target.</summary>
        BlueMonsterTarget = 1 << 9,

        /// <summary>
        /// The entity holds the level open. A level is completed once no registered entity carries
        /// this capability, provided at least one carrier existed when the level started.
        /// </summary>
        LevelEndLock = 1 << 10,

        /// <summary>
        /// The entity stops a flying entity from entering its cell.
        /// Only terrain cells and tunnels carry it, so a beetle flies
        /// over rocks, monsters and collectibles but not through a wall.
        /// </summary>
        BlocksFlight = 1 << 11,
    }
}
