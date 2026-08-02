using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Player
{
    /// <summary>
    /// Captures which player inputs are currently held. The recovered <c>CCtrl.CState</c> holds
    /// exactly this: one flag per cardinal direction plus <c>bFire</c>, which the keyboard
    /// controller fills from <c>App.CtrlKeyFire1</c>.
    /// </summary>
    /// <param name="Left">Whether left is held.</param>
    /// <param name="Up">Whether up is held.</param>
    /// <param name="Right">Whether right is held.</param>
    /// <param name="Down">Whether down is held.</param>
    /// <param name="Fire">Whether the action control is held.</param>
    public readonly record struct PlayerInputState(
        bool Left,
        bool Up,
        bool Right,
        bool Down,
        bool Fire = false)
    {
        /// <summary>
        /// Determines whether a direction is held.
        /// </summary>
        /// <param name="direction">The cardinal direction to inspect.</param>
        /// <returns><see langword="true"/> when the direction is held.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is invalid.</exception>
        public bool IsHeld(Direction direction)
        {
            return direction switch
            {
                Direction.Left => Left,
                Direction.Up => Up,
                Direction.Right => Right,
                Direction.Down => Down,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction."),
            };
        }

        /// <summary>
        /// Selects the first held direction using the fixed left, up, right, down priority.
        /// </summary>
        /// <returns>The first held direction, or <see langword="null"/> when none are held.</returns>
        public Direction? FirstHeldDirection()
        {
            return Left ? Direction.Left
                : Up ? Direction.Up
                : Right ? Direction.Right
                : Down ? Direction.Down
                : null;
        }
    }
}
