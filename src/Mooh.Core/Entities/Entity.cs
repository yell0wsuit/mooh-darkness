using Mooh.Core.Grid;
using Mooh.Core.Interactions;

namespace Mooh.Core.Entities
{
    public sealed class Entity(EntityId id, GridPosition position, InteractionMask interactions)
    {
        public EntityId Id { get; } = id;

        public GridPosition Position { get; internal set; } = position;

        public InteractionMask Interactions { get; set; } = interactions;
    }
}
