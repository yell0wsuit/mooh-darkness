using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Simulation;

namespace Mooh.SimulationRunner
{
    /// <summary>Holds a scenario translated into live core simulation objects.</summary>
    public sealed class MaterializedScenario
    {
        internal MaterializedScenario(
            ScenarioDocument document,
            WorldGrid world,
            SimulationWorld simulation,
            IReadOnlyDictionary<string, EntityId> idsByAlias,
            IReadOnlyDictionary<EntityId, string> aliasesById)
        {
            Document = document;
            World = world;
            Simulation = simulation;
            IdsByAlias = idsByAlias;
            AliasesById = aliasesById;
        }

        /// <summary>Gets the validated source document.</summary>
        public ScenarioDocument Document { get; }

        /// <summary>Gets the authoritative runtime world.</summary>
        public WorldGrid World { get; }

        /// <summary>Gets the live simulation coordinator.</summary>
        public SimulationWorld Simulation { get; }

        /// <summary>Gets deterministic runtime identifiers by scenario alias.</summary>
        public IReadOnlyDictionary<string, EntityId> IdsByAlias { get; }

        /// <summary>Gets scenario aliases by deterministic runtime identifier.</summary>
        public IReadOnlyDictionary<EntityId, string> AliasesById { get; }
    }
}
