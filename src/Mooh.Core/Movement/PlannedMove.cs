using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Movement
{
    internal readonly record struct PlannedMove(EntityId EntityId, GridPosition From, GridPosition To);
}
