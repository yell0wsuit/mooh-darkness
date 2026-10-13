using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Explosions
{
    /// <summary>
    /// Describes a fired rocket, the recovered <c>CGameWeapRocket</c>: a shadow projectile that
    /// flies in one fixed direction until the cell ahead stops it or something collides with it.
    /// </summary>
    /// <param name="direction">The fixed flight direction.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is undefined.</exception>
    public sealed class RocketBody(Direction direction)
    {
        /// <summary>Gets the fixed flight direction, the recovered <c>Type.ulFDir</c>.</summary>
        public Direction Direction
        {
            get;
        } = Enum.IsDefined(direction)
            ? direction
            : throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
    }
}
