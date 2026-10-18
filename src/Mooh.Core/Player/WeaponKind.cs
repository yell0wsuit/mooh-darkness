namespace Mooh.Core.Player
{
    /// <summary>
    /// Identifies one entry of the recovered weapon list, in its <c>WeapInit</c> order, which is
    /// also the order <c>WeapNext</c> cycles through and the order of the <c>1</c>-<c>4</c> keys.
    /// </summary>
    public enum WeaponKind
    {
        /// <summary>The grenade, <c>WeapGr</c>, thrown into the faced cell.</summary>
        Grenade,

        /// <summary>The rocket, <c>WeapRock</c>, launched left or right.</summary>
        Rocket,

        /// <summary>The mine, <c>WeapMine</c>, laid in the faced cell.</summary>
        Mine,

        /// <summary>The big bomb, <c>WeapBBomb</c>, placed in the player's own cell.</summary>
        BigBomb,
    }
}
