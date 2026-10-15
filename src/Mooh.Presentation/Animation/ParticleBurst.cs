using System.Collections.Generic;

namespace Mooh.Presentation.Animation
{
    /// <summary>How a recovered <c>CSfxPrtInfo</c> emits its particles.</summary>
    public enum ParticleBurstKind
    {
        /// <summary><c>SfxEPartsObj</c>: pieces strewn over the cell that fall under a little gravity.</summary>
        Ground,

        /// <summary><c>SfxEPartsObjAir</c>: pieces strewn over the cell that float, <c>bAir</c>.</summary>
        Air,

        /// <summary><c>SfxGlowKill</c>: sparks over the object's visible pixels, flying outward.</summary>
        Stars,
    }

    /// <summary>
    /// A recovered particle burst, <c>CSfxPrtInfo(Func, Num, Page, Lst, ATime)</c>: an object's
    /// <c>EParts</c> or <c>PrtKill</c>.
    /// </summary>
    /// <param name="Kind">How the burst emits.</param>
    /// <param name="Page">The sprite page the particles draw from.</param>
    /// <param name="Variants">The frame sequences a particle picks one of.</param>
    /// <param name="MinCount">The fewest particles, <c>Num[0]</c>.</param>
    /// <param name="MaxCount">One more than the most particles, <c>Num[1]</c>, as <c>RandRange</c> excludes it.</param>
    /// <param name="MinFrameMilliseconds">The shortest frame time, <c>ATime[0]</c>.</param>
    /// <param name="MaxFrameMilliseconds">The longest frame time, <c>ATime[1]</c>.</param>
    public sealed record ParticleBurst(
        ParticleBurstKind Kind,
        string Page,
        IReadOnlyList<IReadOnlyList<string>> Variants,
        int MinCount,
        int MaxCount,
        double MinFrameMilliseconds,
        double MaxFrameMilliseconds);

    /// <summary>The puff an object raises when it lands or is pushed: its <c>PrtLand</c> or <c>PrtMove</c>.</summary>
    public enum ParticlePuff
    {
        /// <summary><c>SfxLandPuff</c> or <c>SfxMovePuff</c>: walking smoke.</summary>
        Smoke,

        /// <summary><c>SfxLandFire</c> or <c>SfxMoveFire</c>: sparks thrown up under gravity.</summary>
        Fire,
    }

    /// <summary>A page and the frame sequences a particle picks one of.</summary>
    /// <param name="Page">The sprite page.</param>
    /// <param name="Variants">The frame sequences.</param>
    public sealed record ParticleFrames(string Page, IReadOnlyList<IReadOnlyList<string>> Variants)
    {
        /// <summary>Gets an empty set, for frames the data does not hold.</summary>
        public static ParticleFrames None { get; } = new(string.Empty, []);
    }

    /// <summary>
    /// The glints a resting pickup gives off, its type's <c>PFunc</c>, which <c>GameInit</c> starts
    /// and each run reschedules.
    /// </summary>
    public enum IdleSparkle
    {
        /// <summary><c>_Prt_Diam1</c>: a <c>SfxGem1</c> glint, or a flash.</summary>
        Gem1,

        /// <summary><c>_Prt_Diam2</c>: a <c>SfxGem2</c> glint, or a flash.</summary>
        Gem2,

        /// <summary><c>_Prt_Diam3</c>: a <c>SfxGem3</c> glint, or a flash.</summary>
        Gem3,

        /// <summary><c>_Prt_Diam5</c> and <c>_Prt_Coin13</c>: a <c>SfxGem5</c> glint, or a flash.</summary>
        Gem5,

        /// <summary><c>_Prt_Coin2</c>, <c>_Prt_DoorKey</c>, <c>_Prt_Bons</c> and <c>_Prt_WeapMine</c>: a flash, <c>_Ptr_Flash</c>.</summary>
        Flash,

        /// <summary><c>_Prt_Eat3</c>: steam rising, <c>SfxEat3</c>.</summary>
        Steam,

        /// <summary><c>_Prt_WeapGr</c>: a <c>SfxWeapGr</c> glint, or a flash.</summary>
        Grenade,

        /// <summary><c>_Prt_WeapRock</c>: a <c>SfxWeapRock</c> glint, or a flash.</summary>
        Rocket,

        /// <summary><c>_Prt_BBombGet</c>: two glints circling, <c>SfxWeapBBombStay</c>, or a flash.</summary>
        BigBomb,
    }

    /// <summary>The game module's shared particle frames, from <c>CGameType.ModInit</c>.</summary>
    /// <param name="WalkSmoke"><c>SfxWSmoke</c>: the puffs of steps, landings and pushes.</param>
    /// <param name="Smoke"><c>SfxSmoke</c>: the smoke a blast leaves.</param>
    /// <param name="Hearts"><c>SfxHealth</c>: the hearts a hurt player sheds.</param>
    /// <param name="Fire"><c>_SfxLandFire</c>: the sparks of a hard landing or push.</param>
    /// <param name="GlowLandHorizontal"><c>SfxGlowLandH</c>, on <c>SpriteGlow</c>: a landing along a vertical gravity.</param>
    /// <param name="GlowLandVertical"><c>SfxGlowLandV</c>, on <c>SpriteGlow</c>: a stop against a side.</param>
    /// <param name="PickupStars"><c>SfxPlGet</c>: the sparks of an eaten pickup.</param>
    public sealed record ParticleEffects(
        IReadOnlyList<IReadOnlyList<string>> WalkSmoke,
        IReadOnlyList<IReadOnlyList<string>> Smoke,
        IReadOnlyList<IReadOnlyList<string>> Hearts,
        IReadOnlyList<IReadOnlyList<string>> Fire,
        IReadOnlyList<string> GlowLandHorizontal,
        IReadOnlyList<string> GlowLandVertical,
        ParticleBurst? PickupStars)
    {
        /// <summary>Gets the glints of each resting pickup's <see cref="IdleSparkle"/>, by kind.</summary>
        public IReadOnlyDictionary<IdleSparkle, ParticleFrames> Sparkles { get; init; } = new Dictionary<IdleSparkle, ParticleFrames>();

        /// <summary>Gets the flash every glinting pickup may give instead, <c>SfxWeapMine</c>.</summary>
        public ParticleFrames Flash { get; init; } = ParticleFrames.None;

        /// <summary>Gets the cola's bubbles, <c>SfxCola</c>.</summary>
        public ParticleFrames ColaBubbles { get; init; } = ParticleFrames.None;

        /// <summary>The page the shared particles draw from, <c>PgParts</c>.</summary>
        public const string PartsPage = "SpriteParts";

        /// <summary>The page the glows draw from, <c>PgGlow</c>.</summary>
        public const string GlowPage = "SpriteGlow";
    }
}
