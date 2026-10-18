namespace Mooh.Core.Explosions
{
    /// <summary>
    /// Tracks a placed big bomb's recovered fuse. <c>ST_Stay</c> runs 14 frames at 50 ms, then
    /// <c>ST_Work</c> loops at 3 frames of 75 ms and counts <c>CNT_BBombTickNum</c> = 30 ticks down
    /// before the 31st loop end runs <c>BBombWork</c>: 700 + 31 x 225 = 7675 ms.
    /// </summary>
    public sealed class BigBombBody
    {
        /// <summary>The recovered time from placement to <c>BBombWork</c>.</summary>
        internal const double FuseSeconds = 7.675;

        /// <summary>Gets the time left until the bomb works on its own.</summary>
        public double FuseRemainingSeconds { get; internal set; } = FuseSeconds;
    }
}
