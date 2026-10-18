using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the world finished turning. Every entity, every locked cell and every
    /// teleporter link has already been remapped, and the world's dimensions have swapped, so an
    /// observer that caches either must re-read them here.
    /// </summary>
    /// <param name="EntityId">The rotator that owned the turn.</param>
    /// <param name="Direction">The direction the world turned.</param>
    /// <param name="Width">The world's width after the turn.</param>
    /// <param name="Height">The world's height after the turn.</param>
    public sealed record WorldRotationCompleted(
        EntityId EntityId,
        WorldRotationDirection Direction,
        int Width,
        int Height) : SimulationEvent(EntityId);
}
