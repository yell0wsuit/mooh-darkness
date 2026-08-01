using System;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
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

        /// <summary>Verifies every byte outside the declaration index is deterministically zero.</summary>
        [Fact]
        public void GeneratesExactIndexBasedEntityIdentifier()
        {
            MaterializedScenario result = ScenarioMaterializer.Materialize(ScenarioTestData.CreateValid());

            Assert.Equal(
                new Guid("00000001-0000-0000-0000-000000000000"),
                result.IdsByAlias["player"].Value);
        }

        /// <summary>Verifies health and green-monster declarations create their runtime components.</summary>
        [Fact]
        public void MaterializesHealthAndGreenMonsterBodies()
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            ScenarioEntity monster = new()
            {
                Alias = "monster",
                Position = new ScenarioPosition { X = 2, Y = 1 },
                Interactions = ["BlocksMovement"],
                HealthBody = new ScenarioHealthBody
                {
                    Maximum = 60,
                    Current = 45,
                    ImmunityMilliseconds = 500,
                },
                GreenMonsterBody = new ScenarioGreenMonsterBody
                {
                    MoveSpeed = 1.5625,
                    FacingDirection = "Left",
                    TurnMilliseconds = 250,
                    AttackWindupMilliseconds = 625,
                    AttackRecoveryMilliseconds = 500,
                    StrikeDamage = 20,
                },
            };
            scenario = ScenarioTestData.WithWorld(
                scenario,
                entities: [scenario.World.Entities[0], monster]);
            ScenarioValidator.Validate(scenario, "monster.json");

            MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);

            HealthBody health = Assert.IsType<HealthBody>(materialized.World.Entities[1].HealthBody);
            GreenMonsterBody body = Assert.IsType<GreenMonsterBody>(
                materialized.World.Entities[1].GreenMonsterBody);
            Assert.Equal(45, health.Current);
            Assert.Equal(TimeSpan.FromMilliseconds(500), health.ImmunityDuration);
            Assert.Equal(Direction.Left, body.FacingDirection);
            Assert.Equal(TimeSpan.FromMilliseconds(625), body.AttackWindupDuration);
            Assert.Equal(20, body.StrikeDamage);
        }

        /// <summary>Verifies blue-monster declarations create recovered runtime configuration.</summary>
        [Fact]
        public void MaterializesBlueMonsterBody()
        {
            ScenarioDocument scenario = ScenarioTestData.CreateValid();
            ScenarioEntity monster = new()
            {
                Alias = "blue",
                Position = new ScenarioPosition { X = 2, Y = 1 },
                Interactions = ["BlocksMovement"],
                BlueMonsterBody = new ScenarioBlueMonsterBody
                {
                    MoveSpeed = 1.5625,
                    FacingDirection = "Left",
                    TurnMilliseconds = 300,
                    AttackWindupMilliseconds = 600,
                    LeftAttackRecoveryMilliseconds = 985,
                    AttackRecoveryMilliseconds = 1150,
                    StrikeDamage = 20,
                },
            };
            scenario = ScenarioTestData.WithWorld(
                scenario,
                entities: [scenario.World.Entities[0], monster]);
            ScenarioValidator.Validate(scenario, "blue.json");

            MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);

            BlueMonsterBody body = Assert.IsType<BlueMonsterBody>(
                materialized.World.Entities[1].BlueMonsterBody);
            Assert.Equal(Direction.Left, body.FacingDirection);
            Assert.Equal(TimeSpan.FromMilliseconds(300), body.TurnDuration);
            Assert.Equal(TimeSpan.FromMilliseconds(600), body.AttackWindupDuration);
            Assert.Equal(TimeSpan.FromMilliseconds(985), body.LeftAttackRecoveryDuration);
            Assert.Equal(TimeSpan.FromMilliseconds(1150), body.AttackRecoveryDuration);
            Assert.Equal(20, body.StrikeDamage);
        }
    }
}
