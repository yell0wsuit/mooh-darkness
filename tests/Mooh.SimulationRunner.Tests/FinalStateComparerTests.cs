using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies exact final simulation state comparison.</summary>
    public sealed class FinalStateComparerTests
    {
        /// <summary>Runtime additions remain part of the exact roster and have stable aliases.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ComparesRuntimeSpawnedEntities(bool includeSpawn)
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);
            EntityId id = new(new Guid("00000000-0000-0000-0000-000000000099"));
            materialized.World.Add(new Entity(id, new GridPosition(2, 2), InteractionMask.None));
            ExpectedFinalState expected = new()
            {
                Entities = includeSpawn
                    ? [scenario.ExpectFinal.Entities[0], new ExpectedEntityState
                    {
                        Alias = "spawned:00000000-0000-0000-0000-000000000099",
                        Position = new ScenarioPosition { X = 2, Y = 2 },
                    }]
                    : scenario.ExpectFinal.Entities,
            };

            if (includeSpawn)
            {
                scenario = ScenarioTestData.WithFinal(scenario, expected.Entities);
                ScenarioValidator.Validate(scenario, "spawn.json");
                ScenarioComparer.CompareFinalState(expected, materialized, "spawn.json");
            }
            else
            {
                _ = Assert.Throws<ScenarioMismatchException>(() => ScenarioComparer.CompareFinalState(expected, materialized, "spawn.json"));
            }
        }

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
                        GreenMonsterState = "TransformingSmall",
                        GreenMonsterFacingDirection = "Right",
                        GreenMonsterRemainingMilliseconds = 375,
                        GreenMonsterIsFrozen = false,
                        GreenMonsterIsSmall = true,
                    },
                ]);
            MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);
            _ = materialized.Simulation.SetGreenMonsterGlasses(active: true);

            ScenarioComparer.CompareFinalState(scenario.ExpectFinal, materialized, "components.json");
        }

        /// <summary>Verifies exact blue-monster state is observable.</summary>
        [Fact]
        public void AcceptsExactBlueMonsterState()
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            ScenarioEntity monster = new()
            {
                Alias = "blue",
                Position = new ScenarioPosition { X = 3, Y = 3 },
                Interactions = ["BlocksMovement"],
                BlueMonsterBody = new ScenarioBlueMonsterBody
                {
                    MoveSpeed = 1.5625,
                    FacingDirection = "Right",
                    TurnMilliseconds = 300,
                    AttackWindupMilliseconds = 600,
                    LeftAttackRecoveryMilliseconds = 985,
                    AttackRecoveryMilliseconds = 1150,
                    StrikeDamage = 20,
                },
            };
            scenario = ScenarioTestData.WithWorld(scenario, entities: [scenario.World.Entities[0], monster]);
            scenario = ScenarioTestData.WithFinal(
                scenario,
                [
                    scenario.ExpectFinal.Entities[0],
                    new ExpectedEntityState
                    {
                        Alias = "blue",
                        Position = monster.Position,
                        BlueMonsterState = "Deciding",
                        BlueMonsterFacingDirection = "Right",
                        BlueMonsterRemainingMilliseconds = 0,
                    },
                ]);
            MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);

            ScenarioComparer.CompareFinalState(scenario.ExpectFinal, materialized, "blue-state.json");
        }
    }
}
