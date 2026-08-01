using System;
using System.Collections.Generic;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Simulation;

namespace Mooh.Core.Legacy
{
    /// <summary>Contains a live runtime world and its archival object identity mapping.</summary>
    public sealed class MaterializedLevel(
        LevelDocument source,
        WorldGrid world,
        SimulationWorld simulation,
        Direction gravityDirection,
        IReadOnlyDictionary<int, EntityId> entityIdsByObjectId)
    {
        /// <summary>Gets the validated archival source.</summary>
        public LevelDocument Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

        /// <summary>Gets the authoritative runtime grid.</summary>
        public WorldGrid World { get; } = world ?? throw new ArgumentNullException(nameof(world));

        /// <summary>Gets the live deterministic simulation.</summary>
        public SimulationWorld Simulation { get; } = simulation
            ?? throw new ArgumentNullException(nameof(simulation));

        /// <summary>Gets the initial gravity direction.</summary>
        public Direction GravityDirection { get; } = gravityDirection;

        /// <summary>Gets runtime identities keyed by archival object identifier.</summary>
        public IReadOnlyDictionary<int, EntityId> EntityIdsByObjectId { get; } = entityIdsByObjectId
            ?? throw new ArgumentNullException(nameof(entityIdsByObjectId));

        /// <summary>Gets a runtime entity by its archival object identifier.</summary>
        /// <param name="objectId">The archival object identifier.</param>
        /// <returns>The registered runtime entity.</returns>
        /// <exception cref="KeyNotFoundException">The object identifier was not materialized.</exception>
        public Entity GetEntity(int objectId)
        {
            return World.GetEntity(EntityIdsByObjectId[objectId]);
        }
    }
}
