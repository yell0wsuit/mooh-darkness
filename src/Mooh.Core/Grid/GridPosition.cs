using System;

namespace Mooh.Core.Grid
{
    /// <summary>
    /// Represents an immutable integer coordinate on a world grid.
    /// </summary>
    /// <param name="X">The horizontal coordinate.</param>
    /// <param name="Y">The vertical coordinate.</param>
    public readonly record struct GridPosition(int X, int Y)
    {
        /// <summary>
        /// Returns the adjacent position in the specified cardinal direction.
        /// </summary>
        /// <param name="direction">The direction in which to offset this position.</param>
        /// <returns>The adjacent grid position.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="direction"/> is not a defined <see cref="Direction"/> value.
        /// </exception>
        public GridPosition Offset(Direction direction)
        {
            return direction switch
            {
                Direction.Left => this with { X = X - 1 },
                Direction.Up => this with { Y = Y - 1 },
                Direction.Right => this with { X = X + 1 },
                Direction.Down => this with { Y = Y + 1 },
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
        }
    }
}
