using System;

namespace Mooh.Core.Grid
{
    /// <summary>
    /// Holds the quarter-turn coordinate transform a world rotation applies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A rotation rewrites more than one coordinate space — every entity's cell, every locked
    /// cell's state, every <c>CLinkCoordBase.LinkCoords</c> entry and, in the original, the
    /// particle list — so the mapping is stated once here and used by each of them rather than
    /// re-derived. The recovered code does the same: <c>sub_49DC10</c> and <c>sub_49DB30</c> each
    /// walk the cell array, the object list and the particle list with one expression.
    /// </para>
    /// <para>
    /// Only positions rotate. Gravity keeps pulling the same way, because nothing in the rotation
    /// touches <c>OList.ulDirBottom</c> — that is <c>CGameObjAntiGArrow</c>'s job, and it has no
    /// placed instances. Facings, arrow directions and tunnel routing maps are untouched too, so
    /// every object keeps its screen-space orientation and only the level moves underneath it.
    /// </para>
    /// </remarks>
    public static class WorldRotation
    {
        /// <summary>
        /// Maps a cell through a quarter turn.
        /// </summary>
        /// <param name="position">The cell to map.</param>
        /// <param name="direction">The direction of the turn.</param>
        /// <param name="width">The world width <em>before</em> the turn.</param>
        /// <param name="height">The world height <em>before</em> the turn.</param>
        /// <returns>The cell's position in the rotated world, whose size is <paramref name="height"/> by <paramref name="width"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="direction"/> is not a defined <see cref="WorldRotationDirection"/> value.
        /// </exception>
        public static GridPosition Map(
            GridPosition position,
            WorldRotationDirection direction,
            int width,
            int height)
        {
            return direction switch
            {
                WorldRotationDirection.Left => new GridPosition(height - 1 - position.Y, position.X),
                WorldRotationDirection.Right => new GridPosition(position.Y, width - 1 - position.X),
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
        }
    }
}
