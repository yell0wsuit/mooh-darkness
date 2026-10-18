namespace Mooh.Core.Shakes
{
    /// <summary>Identifies an entity's recovered reaction to a cell shake.</summary>
    /// <remarks>
    /// The original calls <c>OnShake</c> or <c>OnShakeHard</c> on every occupant of a shaken
    /// cell and only nudges occupants that define neither, so an absent handler destroys
    /// nothing. Landing falling objects and monster strikes share this same dispatch.
    /// </remarks>
    public enum ShakeReaction
    {
        /// <summary>The entity defines no handler and is left in place.</summary>
        None,

        /// <summary>The entity is destroyed without producing a blast.</summary>
        Destroy,

        /// <summary>The entity is destroyed and starts its own delayed 3x3 blast.</summary>
        Detonate3x3,

        /// <summary>
        /// The entity is a directional arrow and is launched as a projectile. The recovered
        /// <c>OArrow*</c> types name <c>_DoFly_&lt;dir&gt;</c> for both shake strengths.
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

        /// <summary>
        /// The entity is a magic gem and runs the recovered <c>_DoMagic</c>: it dies and turns the
        /// emptied cells around it into diamonds.
        /// </summary>
        Magic,
    }
}
