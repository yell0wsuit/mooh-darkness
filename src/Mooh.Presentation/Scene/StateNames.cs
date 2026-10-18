using System;

using Mooh.Core.Grid;

namespace Mooh.Presentation.Scene
{
    /// <summary>Builds the recovered state names that carry a direction.</summary>
    internal static class StateNames
    {
        /// <summary>Gets a direction's state suffix, such as <c>bottom</c> in <c>st_wlkbottom</c>.</summary>
        /// <param name="direction">The direction.</param>
        /// <returns>The suffix.</returns>
        internal static string Name(Direction direction)
        {
            return direction switch
            {
                Direction.Left => "left",
                Direction.Up => "top",
                Direction.Right => "right",
                Direction.Down => "bottom",
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
        }

        /// <summary>Gets a turn state's name, such as <c>st_rotb2l</c>.</summary>
        /// <param name="prefix">The state prefix, such as <c>st_</c> or <c>st_sm</c>.</param>
        /// <param name="from">The direction the turn starts facing.</param>
        /// <param name="to">The direction the turn ends facing.</param>
        /// <returns>The state name.</returns>
        internal static string Turn(string prefix, Direction from, Direction to)
        {
            return $"{prefix}rot{Name(from)[0]}2{Name(to)[0]}";
        }
    }
}
