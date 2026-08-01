using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies validation before a scenario mutates simulation state.</summary>
    public sealed class ScenarioValidatorTests
    {
        /// <summary>Verifies only the documented schema version is accepted.</summary>
        [Fact]
        public void RejectsUnsupportedFormatVersion()
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            scenario = new ScenarioDocument
            {
                FormatVersion = 2,
                Name = scenario.Name,
                World = scenario.World,
                Steps = scenario.Steps,
                ExpectFinal = scenario.ExpectFinal,
            };

            ScenarioValidationException exception = Assert.Throws<ScenarioValidationException>(
                () => ScenarioValidator.Validate(scenario, "version.json"));

            Assert.Contains("formatVersion", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies invalid world, entity, step, and expectation shapes are rejected.</summary>
        [Fact]
        public void RejectsInvalidScenarioShapes()
        {
            List<ScenarioDocument> invalid = CreateInvalidScenarios();

            Exception?[] exceptions =
            [
                .. invalid.Select(
                    scenario => Record.Exception(
                        () => ScenarioValidator.Validate(scenario, "invalid.json"))),
            ];

            Assert.All(exceptions, exception => Assert.IsType<ScenarioValidationException>(exception));
        }

        private static List<ScenarioDocument> CreateInvalidScenarios()
        {
            ScenarioDocument valid = ScenarioTestData.CreateValid();
            ScenarioEntity player = valid.World.Entities[0];
            ScenarioEntity duplicatePlayer = new()
            {
                Alias = "second-player",
                Position = new ScenarioPosition { X = 2, Y = 1 },
                Interactions = [],
                PlayerBody = player.PlayerBody,
            };
            ScenarioEntity badEntity = new()
            {
                Alias = " ",
                Position = new ScenarioPosition { X = 9, Y = 9 },
                Interactions = ["Unknown", "Unknown"],
                GravityBody = new ScenarioGravityBody { FallSpeed = 0, CanSlide = false },
            };
            ScenarioStep badStep = new()
            {
                Input = ["Diagonal"],
                AdvanceMilliseconds = 0,
                ExpectEvents =
                [
                    new ExpectedEvent { Type = "Unknown", Entity = "missing" },
                ],
            };
            ExpectedEntityState badFinal = new()
            {
                Alias = "missing",
                Position = new ScenarioPosition { X = 9, Y = 9 },
            };

            return
            [
                ScenarioTestData.WithName(valid, " "),
                ScenarioTestData.WithWorld(valid, width: 0),
                ScenarioTestData.WithWorld(valid, height: 0),
                ScenarioTestData.WithWorld(valid, gravity: "Diagonal"),
                ScenarioTestData.WithWorld(valid, entities: []),
                ScenarioTestData.WithWorld(valid, entities: [player, player]),
                ScenarioTestData.WithWorld(valid, entities: [badEntity]),
                ScenarioTestData.WithWorld(valid, entities: [player, duplicatePlayer]),
                ScenarioTestData.WithSteps(valid, []),
                ScenarioTestData.WithSteps(valid, [badStep]),
                ScenarioTestData.WithFinal(valid, [badFinal]),
                ScenarioTestData.WithFinal(valid, [valid.ExpectFinal.Entities[0], valid.ExpectFinal.Entities[0]]),
            ];
        }
    }
}
