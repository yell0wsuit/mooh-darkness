namespace Mooh.Core.Levels
{
    /// <summary>Identifies the runtime family of an explicitly supported archival object.</summary>
    public enum EntityKind
    {
        /// <summary>The player character.</summary>
        Player,
        /// <summary>A green monster.</summary>
        GreenMonster,
        /// <summary>A blue monster.</summary>
        BlueMonster,
        /// <summary>A bio-mass enemy.</summary>
        BioMass,
        /// <summary>Diggable terrain.</summary>
        DiggableTerrain,
        /// <summary>Destructible solid terrain.</summary>
        DestructibleTerrain,
        /// <summary>Immortal metal terrain.</summary>
        MetalTerrain,
        /// <summary>Destructible wood trim terrain.</summary>
        WoodTerrain,
        /// <summary>A pushable rock.</summary>
        Rock,
        /// <summary>A fast-falling hard weight.</summary>
        Weight,
        /// <summary>An explosive bottle.</summary>
        Bottle,
        /// <summary>An explosive barrel.</summary>
        Barrel,
        /// <summary>A balloon mine.</summary>
        Balloon,
        /// <summary>A floating bubble.</summary>
        Bubble,
        /// <summary>A score coin.</summary>
        Coin,
        /// <summary>A treasure diamond.</summary>
        Diamond,
        /// <summary>A healing food pickup.</summary>
        Food,
        /// <summary>A magic gem.</summary>
        MagicGem,
        /// <summary>A cola bonus.</summary>
        Cola,
        /// <summary>An ice bonus.</summary>
        Ice,
        /// <summary>A colored key.</summary>
        Key,
        /// <summary>A colored key door.</summary>
        KeyDoor,
        /// <summary>A fire hazard.</summary>
        Fire,
        /// <summary>A directional flying gravity arrow.</summary>
        GravityArrow,
        /// <summary>A world-rotation trigger.</summary>
        WorldRotation,
        /// <summary>A linked teleporter.</summary>
        Teleporter,
        /// <summary>A contextual message table.</summary>
        MessageTable,
        /// <summary>A bidirectional pipe.</summary>
        Pipe,
        /// <summary>A tunnel, optionally restricted to one direction.</summary>
        Tunnel,
        /// <summary>A single grenade pickup.</summary>
        Grenade,
        /// <summary>A grenade ammunition box.</summary>
        GrenadeBox,
        /// <summary>A single rocket pickup.</summary>
        Rocket,
        /// <summary>A rocket ammunition box.</summary>
        RocketBox,
        /// <summary>An installed mine.</summary>
        Mine,
        /// <summary>A mine ammunition box.</summary>
        MineBox,
        /// <summary>A big-bomb pickup.</summary>
        BigBomb,
    }
}
