using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Movement;

namespace Mooh.Core.Grid
{
    /// <summary>
    /// Owns entity registration and ordered cell occupancy within fixed bounds.
    /// </summary>
    public sealed class WorldGrid
    {
        private const double VacatedCellLockSeconds = 0.150;

        private readonly Dictionary<GridPosition, CellState> _cellStates = [];
        private readonly Dictionary<EntityId, Entity> _entities = [];
        private readonly List<EntityId> _entityOrder = [];
        private readonly Dictionary<GridPosition, List<EntityId>> _occupancy = [];
        private EntityId? _playerId;

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
        public IReadOnlyList<Entity> Entities => [.. _entityOrder.Select(GetEntity)];

        /// <summary>Gets the world's player entity, if one is registered.</summary>
        public Entity? Player => _playerId is EntityId id ? GetEntity(id) : null;

        internal double CurrentTimeSeconds { get; private set; }

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
        /// Registers an entity at the head of its cell's occupancy order.
        /// </summary>
        /// <param name="entity">The entity to register.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The entity's position is outside the world.</exception>
        /// <exception cref="InvalidOperationException">
        /// The entity identifier is already registered, or the world already has a player.
        /// </exception>
        public void Add(Entity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            if (!Contains(entity.Position))
            {
                throw new ArgumentOutOfRangeException(nameof(entity), entity.Position, "Entity position is outside the world.");
            }

            if (entity.PlayerBody is not null && _playerId is not null)
            {
                throw new InvalidOperationException("The world already has a player.");
            }

            if (!_entities.TryAdd(entity.Id, entity))
            {
                throw new InvalidOperationException($"Entity '{entity.Id}' is already registered.");
            }

            if (entity.PlayerBody is not null)
            {
                _playerId = entity.Id;
            }

            _entityOrder.Add(entity.Id);

            if (!_occupancy.TryGetValue(entity.Position, out List<EntityId>? occupants))
            {
                occupants = [];
                _occupancy.Add(entity.Position, occupants);
            }

            occupants.Insert(0, entity.Id);
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

        /// <summary>Removes a registered entity from the world.</summary>
        /// <param name="id">The identifier of the entity to remove.</param>
        /// <returns><see langword="true"/> when the entity was removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(EntityId id)
        {
            if (!_entities.Remove(id, out Entity? entity))
            {
                return false;
            }

            _ = _entityOrder.Remove(id);
            List<EntityId> occupants = _occupancy[entity.Position];
            _ = occupants.Remove(id);
            if (occupants.Count == 0)
            {
                _ = _occupancy.Remove(entity.Position);
            }

            if (_playerId == id)
            {
                _playerId = null;
            }

            entity.Motion = null;
            return true;
        }

        /// <summary>
        /// Gets the occupants of a cell in newest-arrival-first order.
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

        /// <summary>
        /// Prevents gravity from using a cell for at least the specified simulation duration.
        /// </summary>
        /// <param name="position">The cell to lock.</param>
        /// <param name="duration">The positive lock duration.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="position"/> is outside the world or <paramref name="duration"/> is not positive.
        /// </exception>
        public void LockCell(GridPosition position, TimeSpan duration)
        {
            if (!Contains(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "Position is outside the world.");
            }

            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
            double lockedUntil = CurrentTimeSeconds + duration.TotalSeconds;
            CellState state = GetOrCreateCellState(position);
            state.LockedUntilSeconds = Math.Max(state.LockedUntilSeconds, lockedUntil);
        }

        internal void AdvanceTime(double elapsedSeconds)
        {
            CurrentTimeSeconds += elapsedSeconds;
        }

        internal bool IsCellUnlocked(GridPosition position)
        {
            return !_cellStates.TryGetValue(position, out CellState? state)
                || state.LockedUntilSeconds <= CurrentTimeSeconds;
        }

        internal double? SecondsUntilNextCellUnlock()
        {
            double? result = null;
            foreach (CellState state in _cellStates.Values)
            {
                double remaining = state.LockedUntilSeconds - CurrentTimeSeconds;
                if (remaining > 0 && (result is null || remaining < result))
                {
                    result = remaining;
                }
            }

            return result;
        }

        internal void CommitMoves(IReadOnlyList<PlannedMove> moves)
        {
            ArgumentNullException.ThrowIfNull(moves);
            if (moves.Select(move => move.EntityId).Distinct().Count() != moves.Count)
            {
                throw new InvalidOperationException("A transaction cannot move one entity more than once.");
            }

            foreach (PlannedMove move in moves)
            {
                Entity entity = GetEntity(move.EntityId);
                if (entity.Position != move.From || !Contains(move.To)
                    || !_occupancy.TryGetValue(move.From, out List<EntityId>? source)
                    || !source.Contains(move.EntityId))
                {
                    throw new InvalidOperationException("Movement transaction does not match world state.");
                }
            }

            foreach (PlannedMove move in moves)
            {
                List<EntityId> source = _occupancy[move.From];
                _ = source.Remove(move.EntityId);
                LockCellForVacatingMove(move.From);
                if (source.Count == 0)
                {
                    _ = _occupancy.Remove(move.From);
                }
            }

            foreach (PlannedMove move in moves)
            {
                Entity entity = _entities[move.EntityId];
                entity.Position = move.To;
                if (!_occupancy.TryGetValue(move.To, out List<EntityId>? destination))
                {
                    destination = [];
                    _occupancy.Add(move.To, destination);
                }

                destination.Insert(0, move.EntityId);
            }
        }

        internal void Relocate(EntityId id, GridPosition destination)
        {
            Entity entity = GetEntity(id);
            if (!Contains(destination))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(destination),
                    destination,
                    "Destination is outside the world.");
            }

            if (entity.Position == destination)
            {
                return;
            }

            List<EntityId> source = _occupancy[entity.Position];
            _ = source.Remove(id);
            if (source.Count == 0)
            {
                _ = _occupancy.Remove(entity.Position);
            }

            entity.Position = destination;
            if (!_occupancy.TryGetValue(destination, out List<EntityId>? occupants))
            {
                occupants = [];
                _occupancy.Add(destination, occupants);
            }

            occupants.Insert(0, id);
        }

        private CellState GetOrCreateCellState(GridPosition position)
        {
            if (!_cellStates.TryGetValue(position, out CellState? state))
            {
                state = new CellState();
                _cellStates.Add(position, state);
            }

            return state;
        }

        private void LockCellForVacatingMove(GridPosition position)
        {
            CellState state = GetOrCreateCellState(position);
            state.LockedUntilSeconds = Math.Max(
                state.LockedUntilSeconds,
                CurrentTimeSeconds + VacatedCellLockSeconds);
        }
    }
}
