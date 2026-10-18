namespace Mooh.Core.Enemies
{
    /// <summary>Identifies the recovered bio-mass animation state that drives its behavior.</summary>
    public enum BioMassState
    {
        /// <summary>The idle <c>ST_Stay</c> loop, whose every end may start a spawn.</summary>
        Stay,

        /// <summary><c>ST_SpawnVert</c> or <c>ST_SpawnHor</c>, which end in <c>_AEnd_SWork</c>.</summary>
        Spawn,

        /// <summary><c>ST_Grow</c>, which a newly spread bio mass plays before it idles.</summary>
        Grow,
    }
}
