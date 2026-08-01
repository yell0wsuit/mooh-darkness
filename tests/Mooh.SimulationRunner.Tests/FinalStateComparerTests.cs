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

        /// <summary>Verifies exact health, reaction, and monster state are observable.</summary>
        [Fact]
        public void AcceptsExactGameplayComponentState()
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            ScenarioEntity player = scenario.World.Entities[0];
            player = new ScenarioEntity
            {
                Alias = player.Alias,
                Position = player.Position,
                Interactions = player.Interactions,
                PlayerBody = player.PlayerBody,
                HealthBody = new ScenarioHealthBody
                {
                    Maximum = 100,
                    Current = 80,
                    ImmunityMilliseconds = 500,
                },
            };
            ScenarioEntity monster = new()
            {
                Alias = "monster",
                Position = new ScenarioPosition { X = 3, Y = 3 },
                Interactions = ["BlocksMovement"],
                GreenMonsterBody = new ScenarioGreenMonsterBody
                {
                    MoveSpeed = 1.5625,
                    FacingDirection = "Right",
                    TurnMilliseconds = 250,
                    AttackWindupMilliseconds = 625,
                    AttackRecoveryMilliseconds = 500,
                    StrikeDamage = 20,
                },
            };
            scenario = ScenarioTestData.WithWorld(scenario, entities: [player, monster]);
            scenario = ScenarioTestData.WithFinal(
                scenario,
                [
                    new ExpectedEntityState
                    {
                        Alias = "player",
                        Position = player.Position,
                        FacingDirection = "Right",
                        CurrentHealth = 80,
                        ImmunityMilliseconds = 0,
                        PlayerReaction = "Normal",
                        ReactionMilliseconds = 0,
                    },
                    new ExpectedEntityState
                    {
                        Alias = "monster",
                        Position = monster.Position,
                        GreenMonsterState = "Deciding",
                        GreenMonsterFacingDirection = "Right",
                        GreenMonsterRemainingMilliseconds = 0,
                    },
                ]);
            MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);

            ScenarioComparer.CompareFinalState(scenario.ExpectFinal, materialized, "components.json");
        }
    }
}
