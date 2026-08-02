namespace Mooh.Core.Explosions
{
    /// <summary>Identifies an entity's recovered reaction when reached by an explosion.</summary>
    public enum ExplosionReaction
    {
        /// <summary>The entity is destroyed without producing another blast.</summary>
        Destroy,

        /// <summary>The entity is destroyed and starts its own delayed 3x3 blast.</summary>
        Detonate3x3,

        /// <summary>
        /// The entity carries the recovered <c>CNT_CMaskCellCanExplode</c> bit but reacts through a
        /// specialization that is not implemented yet, so a blast reaching it does nothing. The
        /// recovered handlers are the barrel's cross blast and the gravity arrow's launch.
        /// </summary>
        Deferred,
    }
}
