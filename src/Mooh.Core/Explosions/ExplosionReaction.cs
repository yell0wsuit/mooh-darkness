namespace Mooh.Core.Explosions
{
    /// <summary>Identifies an entity's recovered reaction when reached by an explosion.</summary>
    public enum ExplosionReaction
    {
        /// <summary>The entity is destroyed without producing another blast.</summary>
        Destroy,

        /// <summary>The entity is destroyed and starts its own delayed 3x3 blast.</summary>
        Detonate3x3,
    }
}
