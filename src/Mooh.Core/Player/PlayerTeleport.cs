using Mooh.Core.Grid;

namespace Mooh.Core.Player
{
    /// <summary>Tracks a player being carried between two teleporters.</summary>
    /// <remarks>
    /// The recovered <c>_TPortCFunc</c> flies the player along the straight line between the two
    /// cells, ignoring the grid entirely: it shifts by <c>CNT_TPortSp</c> per millisecond until
    /// the remaining distance is smaller than one step and then snaps to the destination. The
    /// player is <c>bShadow</c> and <c>bImmort</c> for the whole flight and no arrival handler is
    /// registered, so nothing along the way is touched and only the destination cell matters.
    /// The origin and the elapsed fraction are kept so a presentation layer can draw the line.
    /// </remarks>
    /// <param name="origin">The cell the flight started from.</param>
    /// <param name="destination">The cell the flight ends in.</param>
    /// <param name="totalSeconds">The whole flight duration.</param>
    public sealed class PlayerTeleport(
        GridPosition origin,
        GridPosition destination,
        double totalSeconds)
    {
        /// <summary>Gets the cell the flight started from.</summary>
        public GridPosition Origin { get; } = origin;

        /// <summary>Gets the cell the flight ends in.</summary>
        public GridPosition Destination { get; } = destination;

        /// <summary>Gets the whole flight duration in seconds.</summary>
        public double TotalSeconds { get; } = totalSeconds;

        /// <summary>Gets the fraction of the flight already covered, from 0 to 1.</summary>
        public double Progress
        {
            get; internal set;
        }
    }
}
