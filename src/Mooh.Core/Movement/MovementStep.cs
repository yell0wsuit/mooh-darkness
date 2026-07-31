using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Movement
{
    /// <summary>
    /// Describes one entity's cell transition in a movement plan.
    /// </summary>
    /// <param name="EntityId">The entity to move.</param>
    /// <param name="From">The authoritative source cell.</param>
    /// <param name="To">The proposed destination cell.</param>
    public readonly record struct MovementStep(EntityId EntityId, GridPosition From, GridPosition To);
}
