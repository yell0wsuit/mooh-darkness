using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Movement
{
    /// <summary>
    /// Requests movement of an entity in a cardinal direction.
    /// </summary>
    /// <param name="EntityId">The entity to move.</param>
    /// <param name="Direction">The requested direction.</param>
    public readonly record struct MoveRequest(EntityId EntityId, Direction Direction);
}
