using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;

namespace Mooh.Core.Grid
{
    /// <summary>
    /// Owns entity registration and ordered cell occupancy within fixed bounds.
    /// </summary>
    public sealed class WorldGrid
    {
        private readonly Dictionary<EntityId, Entity> _entities = [];
        private readonly Dictionary<GridPosition, List<EntityId>> _occupancy = [];

        /// <summary>
        /// Initializes a world grid with the specified positive dimensions.
        /// </summary>
        /// <param name="width">The number of horizontal cells.</param>
        /// <param name="height">The number of vertical cells.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="width"/> or <paramref name="height"/> is not positive.
        /// </exception>
        public WorldGrid(int width, int height)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            Width = width;
            Height = height;
        }

        /// <summary>Gets the number of horizontal cells.</summary>
        public int Width { get; }

        /// <summary>Gets the number of vertical cells.</summary>
        public int Height { get; }

        /// <summary>Gets all entities registered in the world.</summary>
        public IReadOnlyCollection<Entity> Entities => _entities.Values;

        /// <summary>
        /// Determines whether a position lies within the world bounds.
        /// </summary>
        /// <param name="position">The position to inspect.</param>
        /// <returns><see langword="true"/> when the position is inside the world; otherwise, <see langword="false"/>.</returns>
        public bool Contains(GridPosition position)
        {
            return position.X >= 0 && position.X < Width
                && position.Y >= 0 && position.Y < Height;
        }

        /// <summary>
        /// Registers an entity and appends it to the occupancy order of its cell.
        /// </summary>
        /// <param name="entity">The entity to register.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The entity's position is outside the world.</exception>
        /// <exception cref="InvalidOperationException">The entity identifier is already registered.</exception>
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

        /// <summary>
        /// Gets the entity registered with the specified identifier.
        /// </summary>
        /// <param name="id">The entity identifier.</param>
        /// <returns>The registered entity.</returns>
        /// <exception cref="KeyNotFoundException">No entity with <paramref name="id"/> is registered.</exception>
        public Entity GetEntity(EntityId id)
        {
            return _entities.TryGetValue(id, out Entity? entity)
                ? entity
                : throw new KeyNotFoundException($"Entity '{id}' is not registered.");
        }

        /// <summary>
        /// Gets the occupants of a cell in insertion order.
        /// </summary>
        /// <param name="position">The cell position.</param>
        /// <returns>A snapshot of the cell's occupants.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is outside the world.</exception>
        public IReadOnlyList<Entity> GetOccupants(GridPosition position)
        {
            return !Contains(position)
                ? throw new ArgumentOutOfRangeException(nameof(position), position, "Position is outside the world.")
                : !_occupancy.TryGetValue(position, out List<EntityId>? occupants) ? [] : [.. occupants.Select(GetEntity)];
        }
    }
}
