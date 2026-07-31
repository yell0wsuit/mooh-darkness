using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;

namespace Mooh.Core.Grid
{
    public sealed class WorldGrid
    {
        private readonly Dictionary<EntityId, Entity> _entities = [];
        private readonly Dictionary<GridPosition, List<EntityId>> _occupancy = [];

        public WorldGrid(int width, int height)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyCollection<Entity> Entities => _entities.Values;

        public bool Contains(GridPosition position)
        {
            return position.X >= 0 && position.X < Width
                && position.Y >= 0 && position.Y < Height;
        }

        public void Add(Entity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            if (!Contains(entity.Position))
            {
                throw new ArgumentOutOfRangeException(nameof(entity), entity.Position, "Entity position is outside the world.");
            }

            if (!_entities.TryAdd(entity.Id, entity))
            {
                throw new InvalidOperationException($"Entity '{entity.Id}' is already registered.");
            }

            if (!_occupancy.TryGetValue(entity.Position, out List<EntityId>? occupants))
            {
                occupants = [];
                _occupancy.Add(entity.Position, occupants);
            }

            occupants.Add(entity.Id);
        }

        public Entity GetEntity(EntityId id)
        {
            return _entities.TryGetValue(id, out Entity? entity)
                ? entity
                : throw new KeyNotFoundException($"Entity '{id}' is not registered.");
        }

        public IReadOnlyList<Entity> GetOccupants(GridPosition position)
        {
            if (!Contains(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "Position is outside the world.");
            }

            return !_occupancy.TryGetValue(position, out List<EntityId>? occupants) ? [] : [.. occupants.Select(GetEntity)];
        }
    }
}
