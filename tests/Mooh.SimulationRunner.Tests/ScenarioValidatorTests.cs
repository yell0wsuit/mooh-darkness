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

        /// <summary>Verifies event discriminators require exactly their own fields.</summary>
        [Fact]
        public void RejectsInvalidEventDiscriminatorShapes()
        {
            ExpectedEvent[] invalidEvents =
            [
                new ExpectedEvent { Type = "CellEntered", Entity = "player" },
                new ExpectedEvent { Type = "PlayerAte", Entity = "player" },
                new ExpectedEvent { Type = "PlayerStopped", Entity = "player", Direction = "Down" },
            ];
            Exception?[] exceptions =
            [
                .. invalidEvents.Select(
                    expected => Record.Exception(
                        () => ScenarioValidator.Validate(
                            WithExpectedEvent(expected),
                            "event-shape.json"))),
            ];

            Assert.All(exceptions, exception => Assert.IsType<ScenarioValidationException>(exception));
        }

        /// <summary>Verifies enum values must use names rather than numeric spellings.</summary>
        [Fact]
        public void RejectsNumericEnumSpelling()
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            scenario = ScenarioTestData.WithWorld(scenario, gravity: "3");

            ScenarioValidationException exception = Assert.Throws<ScenarioValidationException>(
                () => ScenarioValidator.Validate(scenario, "numeric.json"));

            Assert.Contains("world.gravity", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies invalid health and green-monster configuration is rejected.</summary>
        [Fact]
        public void RejectsInvalidGameplayComponents()
        {
            ScenarioDocument valid = ScenarioTestData.CreateValid();
            ScenarioEntity invalidMonster = new()
            {
                Alias = "monster",
                Position = new ScenarioPosition { X = 2, Y = 2 },
                Interactions = [],
                HealthBody = new ScenarioHealthBody
                {
                    Maximum = 0,
                    Current = 0,
                    ImmunityMilliseconds = 500,
                },
                GreenMonsterBody = new ScenarioGreenMonsterBody
                {
                    MoveSpeed = 1.5625,
                    FacingDirection = "Right",
                    TurnMilliseconds = 0,
                    AttackWindupMilliseconds = 625,
                    AttackRecoveryMilliseconds = 500,
                    StrikeDamage = 20,
                },
            };
            ScenarioDocument scenario = ScenarioTestData.WithWorld(
                valid,
                entities: [valid.World.Entities[0], invalidMonster]);

            ScenarioValidationException exception = Assert.Throws<ScenarioValidationException>(
                () => ScenarioValidator.Validate(scenario, "components.json"));

            Assert.Contains("healthBody.maximum", exception.Message, StringComparison.Ordinal);
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

        private static ScenarioDocument WithExpectedEvent(ExpectedEvent expected)
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            return ScenarioTestData.WithSteps(
                scenario,
                [
                    new ScenarioStep
                    {
                        AdvanceMilliseconds = 1,
                        ExpectEvents = [expected],
                    },
                ]);
        }
    }
}
