namespace Mooh.Core.Combat
{
    /// <summary>Identifies the outcome of a damage request.</summary>
    public enum DamageResult
    {
        /// <summary>The request reduced target health.</summary>
        Applied,

        /// <summary>The target was protected by active post-hit immunity.</summary>
        IgnoredImmune,

        /// <summary>The target was already dead.</summary>
        IgnoredDead,
    }
}
