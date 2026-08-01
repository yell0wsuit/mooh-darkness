using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies deterministic translation into runtime simulation types.</summary>
    public sealed class ScenarioMaterializerTests
    {
        /// <summary>Verifies aliases, order, masks, and components are preserved.</summary>
        [Fact]
        public void MaterializesOrderedRuntimeWorld()
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            ScenarioEntity rock = new()
            {
                Alias = "rock",
                Position = new ScenarioPosition { X = 2, Y = 1 },
                Interactions = ["PlayerPushable", "RoundSupport"],
                GravityBody = new ScenarioGravityBody { FallSpeed = 1.875, CanSlide = true },
            };
            scenario = ScenarioTestData.WithWorld(
                scenario,
                gravity: "Left",
                entities: [scenario.World.Entities[0], rock]);
            ScenarioValidator.Validate(scenario, "ordered.json");

            MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);

            Assert.Equal(["player", "rock"], materialized.AliasesById.Values);
            Assert.Equal(2, materialized.World.Entities.Count);
            Assert.Equal(materialized.IdsByAlias["player"], materialized.World.Entities[0].Id);
            Assert.Equal(materialized.IdsByAlias["rock"], materialized.World.Entities[1].Id);
            Assert.Equal(new GridPosition(2, 1), materialized.World.Entities[1].Position);
            Assert.Equal(
                InteractionMask.PlayerPushable | InteractionMask.RoundSupport,
                materialized.World.Entities[1].Interactions);
            GravityBody gravityBody = Assert.IsType<GravityBody>(materialized.World.Entities[1].GravityBody);
            Assert.Equal(1.875, gravityBody.FallSpeed);
            Assert.True(gravityBody.CanSlide);
            Assert.NotNull(materialized.Simulation);
        }

        /// <summary>Verifies the generated identifiers repeat for identical declaration order.</summary>
        [Fact]
        public void GeneratesRepeatableEntityIdentifiers()
        {
            ScenarioDocument first = ScenarioTestData.CreateValid();
            ScenarioDocument second = ScenarioTestData.CreateValid();

            MaterializedScenario firstResult = ScenarioMaterializer.Materialize(first);
            MaterializedScenario secondResult = ScenarioMaterializer.Materialize(second);

            Assert.Equal(firstResult.IdsByAlias["player"], secondResult.IdsByAlias["player"]);
        }
    }
}
