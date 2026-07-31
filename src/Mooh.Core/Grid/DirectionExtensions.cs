using System;

namespace Mooh.Core.Grid
{
    /// <summary>
    /// Provides cardinal direction geometry relative to a direction of travel.
    /// </summary>
    public static class DirectionExtensions
    {
        /// <summary>Returns the direction opposite the specified direction.</summary>
        /// <param name="direction">The direction to reverse.</param>
        /// <returns>The opposite cardinal direction.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="direction"/> is not a defined <see cref="Direction"/> value.
        /// </exception>
        public static Direction Opposite(this Direction direction)
        {
            return direction switch
            {
                Direction.Left => Direction.Right,
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
        }

        /// <summary>Returns the direction to the left of the specified direction.</summary>
        /// <param name="direction">The direction of travel.</param>
        /// <returns>The cardinal direction one quarter-turn to the left.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="direction"/> is not a defined <see cref="Direction"/> value.
        /// </exception>
        public static Direction TurnLeft(this Direction direction)
        {
            return direction switch
            {
                Direction.Left => Direction.Down,
                Direction.Up => Direction.Left,
                Direction.Right => Direction.Up,
                Direction.Down => Direction.Right,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
        }

        /// <summary>Returns the direction to the right of the specified direction.</summary>
        /// <param name="direction">The direction of travel.</param>
        /// <returns>The cardinal direction one quarter-turn to the right.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="direction"/> is not a defined <see cref="Direction"/> value.
        /// </exception>
        public static Direction TurnRight(this Direction direction)
        {
            return direction switch
            {
                Direction.Left => Direction.Up,
                Direction.Up => Direction.Right,
                Direction.Right => Direction.Down,
                Direction.Down => Direction.Left,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
        }
    }
}
