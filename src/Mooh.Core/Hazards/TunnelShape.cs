using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Hazards
{
    /// <summary>
    /// Holds one tunnel type's recovered <c>EnDirMap</c> and <c>DirMap</c> routing tables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both tables are indexed by the direction the player is travelling and give the direction
    /// to continue in, or <see langword="null"/> for the recovered <c>DIR_Last</c>, which means
    /// the tunnel has no opening on that side.
    /// </para>
    /// <para>
    /// The two differ only for the four one-way tunnels. <c>EnDirMap</c> decides what the tunnel
    /// accepts and is what <c>PlComeHere</c> and ordinary transit consult; <c>DirMap</c>
    /// describes the tunnel's physical shape and ignores the arrow, and transit switches to it
    /// permanently once the player has been turned back by a blocked exit. That is what lets a
    /// player who entered a one-way tunnel the legal way still get out again after hitting a
    /// dead end. <c>CGameCellPipes.CType</c> defaults <c>DirMap</c> to <c>EnDirMap</c>, so the
    /// six two-way types route identically before and after a turnaround.
    /// </para>
    /// </remarks>
    public sealed class TunnelShape
    {
        private readonly Direction?[] _entry;
        private readonly Direction?[] _outline;

        private TunnelShape(Direction?[] entry, Direction?[] outline)
        {
            _entry = entry;
            _outline = outline;
        }

        /// <summary>Gets the straight horizontal tunnel, <c>CellHor</c>.</summary>
        public static TunnelShape Horizontal { get; } = Create(
            [Direction.Left, null, Direction.Right, null]);

        /// <summary>Gets the straight vertical tunnel, <c>CellVert</c>.</summary>
        public static TunnelShape Vertical { get; } = Create(
            [null, Direction.Up, null, Direction.Down]);

        /// <summary>Gets the corner joining the left and top sides, <c>CellLeftTop</c>.</summary>
        public static TunnelShape LeftTop { get; } = Create(
            [null, null, Direction.Up, Direction.Left]);

        /// <summary>Gets the corner joining the top and right sides, <c>CellTopRight</c>.</summary>
        public static TunnelShape TopRight { get; } = Create(
            [Direction.Up, null, null, Direction.Right]);

        /// <summary>Gets the corner joining the right and bottom sides, <c>CellRightBottom</c>.</summary>
        public static TunnelShape RightBottom { get; } = Create(
            [Direction.Down, Direction.Right, null, null]);

        /// <summary>Gets the corner joining the bottom and left sides, <c>CellBottomLeft</c>.</summary>
        public static TunnelShape BottomLeft { get; } = Create(
            [null, Direction.Left, Direction.Down, null]);

        /// <summary>Gets the one-way rightward tunnel, <c>CellLeft2Right</c>.</summary>
        public static TunnelShape LeftToRight { get; } = Create(
            [null, null, Direction.Right, null],
            [Direction.Left, null, Direction.Right, null]);

        /// <summary>Gets the one-way leftward tunnel, <c>CellRight2Left</c>.</summary>
        public static TunnelShape RightToLeft { get; } = Create(
            [Direction.Left, null, null, null],
            [Direction.Left, null, Direction.Right, null]);

        /// <summary>Gets the one-way downward tunnel, <c>CellTop2Bottom</c>.</summary>
        public static TunnelShape TopToBottom { get; } = Create(
            [null, null, null, Direction.Down],
            [null, Direction.Up, null, Direction.Down]);

        /// <summary>Gets the one-way upward tunnel, <c>CellBottom2Top</c>.</summary>
        public static TunnelShape BottomToTop { get; } = Create(
            [null, Direction.Up, null, null],
            [null, Direction.Up, null, Direction.Down]);

        /// <summary>
        /// Gets the direction to continue in, or <see langword="null"/> when the tunnel has no
        /// opening for the arrival.
        /// </summary>
        /// <param name="arrival">The direction the player is travelling on arrival.</param>
        /// <param name="ignoreOneWay">
        /// Whether to consult <c>DirMap</c> instead of <c>EnDirMap</c>, which transit does once
        /// the player has been turned back.
        /// </param>
        /// <returns>The direction to continue in, or <see langword="null"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrival"/> is undefined.</exception>
        public Direction? Route(Direction arrival, bool ignoreOneWay = false)
        {
            return !Enum.IsDefined(arrival)
                ? throw new ArgumentOutOfRangeException(nameof(arrival), arrival, null)
                : (ignoreOneWay ? _outline : _entry)[(int)arrival];
        }

        private static TunnelShape Create(Direction?[] entry, Direction?[]? outline = null)
        {
            return new TunnelShape(entry, outline ?? entry);
        }
    }
}
