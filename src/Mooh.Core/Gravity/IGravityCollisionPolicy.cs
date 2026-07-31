using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Gravity
{
    /// <summary>
    /// Determines whether gravity-driven movement may enter a world cell.
    /// </summary>
    public interface IGravityCollisionPolicy
    {
        /// <summary>
        /// Determines whether an entity may enter the specified destination.
        /// </summary>
        /// <param name="world">The world containing current occupancy.</param>
        /// <param name="mover">The entity attempting to move.</param>
        /// <param name="destination">The destination cell.</param>
        /// <returns><see langword="true"/> when entry is permitted; otherwise, <see langword="false"/>.</returns>
        bool CanEnter(WorldGrid world, Entity mover, GridPosition destination);
    }
}
