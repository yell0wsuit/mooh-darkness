using System;

using Mooh.Core.Grid;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// A world rotation in flight, as the native <c>ListRenderSort</c> draws it: every position
    /// turns around the rotator's cell while the sprites stay upright.
    /// </summary>
    /// <remarks>
    /// <c>WorldRotStart</c> keeps the owner's cell as the pivot and accumulates the angle in
    /// quarter turns, positive for <c>WorldRotLeft</c>. The sort lays positions out at cell steps
    /// easing from 80 by 75 to 75 by 80 with the angle, moves them by the pivot at the original
    /// steps, turns them and moves them back by the pivot at the eased steps.
    /// </remarks>
    /// <param name="Pivot">The rotator's cell.</param>
    /// <param name="Angle">The turn so far in quarter turns, positive for a left turn.</param>
    public readonly record struct WorldSpin(GridPosition Pivot, double Angle)
    {
        /// <summary>Gets the turn so far in radians.</summary>
        public double Radians => Angle * Math.PI / 2;

        /// <summary>Gets the spin of a turn in flight.</summary>
        /// <param name="pivot">The rotator's cell.</param>
        /// <param name="direction">The turn's direction.</param>
        /// <param name="progress">How far the turn has come, from zero to one.</param>
        /// <returns>The spin.</returns>
        public static WorldSpin Of(GridPosition pivot, WorldRotationDirection direction, double progress)
        {
            return new WorldSpin(pivot, direction == WorldRotationDirection.Left ? progress : -progress);
        }

        /// <summary>Turns a drawn position.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>The turned position, in cells of the unturned steps.</returns>
        public (double X, double Y) Transform(double x, double y)
        {
            double turned = Math.Abs(Angle);
            double stepX = SceneCamera.CellWidth + ((SceneCamera.CellHeight - SceneCamera.CellWidth) * turned);
            double stepY = SceneCamera.CellHeight + ((SceneCamera.CellWidth - SceneCamera.CellHeight) * turned);
            double fromX = (x * stepX) - (Pivot.X * SceneCamera.CellWidth);
            double fromY = (y * stepY) - (Pivot.Y * SceneCamera.CellHeight);
            double cos = Math.Cos(Radians);
            double sin = Math.Sin(Radians);
            double toX = (fromX * cos) - (fromY * sin) + (Pivot.X * stepX);
            double toY = (fromX * sin) + (fromY * cos) + (Pivot.Y * stepY);
            return (toX / SceneCamera.CellWidth, toY / SceneCamera.CellHeight);
        }
    }
}
