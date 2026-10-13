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
        /// The entity is a directional arrow and is launched as a projectile. The recovered
        /// <c>OArrow*</c> types name <c>_DoFly_&lt;dir&gt;</c> for <c>OnExpl</c> as well, so a
        /// blast fires the arrow rather than destroying it.
        /// </summary>
        LaunchArrow,

        /// <summary>
        /// The entity is destroyed and starts the recovered <c>Game.ObjExplCross</c>: its own cell
        /// at once, then four straight arms. The barrel is the only placed carrier.
        /// </summary>
        CrossBlast,

        /// <summary>
        /// The entity is a placed big bomb and runs the recovered <c>BBombWork</c> flood at once.
        /// </summary>
        BigBomb,
    }
}
