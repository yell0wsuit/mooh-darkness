using System;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies exact final simulation state comparison.</summary>
    public sealed class FinalStateComparerTests
    {
        /// <summary>Verifies an exact stationary player state passes.</summary>
        [Fact]
        public void AcceptsExactFinalState()
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            ScenarioValidator.Validate(scenario, "exact-final.json");
            MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);

            ScenarioComparer.CompareFinalState(
                scenario.ExpectFinal,
                materialized,
                "exact-final.json");
        }

        /// <summary>Verifies the exact surviving entity roster is enforced.</summary>
        [Fact]
        public void ReportsFinalRosterMismatch()
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            scenario = ScenarioTestData.WithFinal(scenario, []);
            MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);

            ScenarioMismatchException exception = Assert.Throws<ScenarioMismatchException>(
                () => ScenarioComparer.CompareFinalState(
                    scenario.ExpectFinal,
                    materialized,
                    "roster.json"));

            Assert.Contains("expectFinal.entities", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies an entity state difference names its alias and field.</summary>
        [Fact]
        public void ReportsFinalEntityFieldMismatch()
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            scenario = ScenarioTestData.WithFinal(
                scenario,
                [
                    new ExpectedEntityState
                    {
                        Alias = "player",
                        Position = new ScenarioPosition { X = 2, Y = 1 },
                        FacingDirection = "Left",
                    },
                ]);
            MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);

            ScenarioMismatchException exception = Assert.Throws<ScenarioMismatchException>(
                () => ScenarioComparer.CompareFinalState(
                    scenario.ExpectFinal,
                    materialized,
                    "field.json"));

            Assert.Contains("expectFinal.entities[0].position", exception.Message, StringComparison.Ordinal);
        }
    }
}
