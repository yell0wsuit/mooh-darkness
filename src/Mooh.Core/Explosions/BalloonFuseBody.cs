using System;

namespace Mooh.Core.Explosions
{
    /// <summary>
    /// Tracks the recovered balloon fuse. <c>PlPush_Ballon</c> enters <c>ST_Work</c> and then runs
    /// <c>del self.PlPush</c>, so the fuse can be lit once and never restarted or extended.
    /// </summary>
    /// <remarks>
    /// The chained states are <c>ST_Work</c> (3 frames at 75 ms), three pairs of
    /// <c>ST_TickN1</c> (<c>CNT_BaloonTickTime</c>, 250 ms) and <c>ST_TickN2</c>
    /// (<c>CNT_BaloonTickTime2</c>, 2000 ms), and <c>ST_Expl</c> (8 frames at 50 ms), whose
    /// <c>AEnd</c> is <c>Game.ObjExplRange3x3</c>. Only the end of the chain is observable to
    /// gameplay; the intermediate states differ in their frames and sounds alone. A shake or a
    /// blast still detonates the balloon at once at any point, because its <c>OnShake</c>,
    /// <c>OnShakeHard</c> and <c>OnExpl</c> are unchanged by the fuse.
    /// </remarks>
    public sealed class BalloonFuseBody
    {
        /// <summary>Gets the recovered fuse duration, from ignition to detonation.</summary>
        public static TimeSpan Duration { get; } = TimeSpan.FromMilliseconds(225 + (3 * (250 + 2000)) + 400);

        /// <summary>Gets whether the fuse has ever been lit.</summary>
        public bool HasBeenLit
        {
            get; private set;
        }

        /// <summary>
        /// Gets the time left until the balloon detonates, or <see langword="null"/> when the fuse
        /// is not burning.
        /// </summary>
        public double? RemainingSeconds
        {
            get; internal set;
        }

        /// <summary>Lights the fuse unless it has already been lit.</summary>
        /// <returns><see langword="true"/> when this call lit the fuse.</returns>
        internal bool TryLight()
        {
            if (HasBeenLit)
            {
                return false;
            }

            HasBeenLit = true;
            RemainingSeconds = Duration.TotalSeconds;
            return true;
        }
    }
}
