using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>Verifies the recovered player damage caused by landing gravity bodies.</summary>
    public sealed class FallingImpactDamageTests
    {
        /// <summary>Verifies ordinary falling objects shake the player on landing.</summary>
        [Theory]
        [InlineData("ORock01")]
        [InlineData("OCoin01")]
        [InlineData("ODiam01")]
        public void OrdinaryFallingObjectDamagesPlayer(string archivalType)
        {
            MaterializedLevel level = Materialize(archivalType, DifficultySpeeds.Standard, objectY: 1);
            EntityId source = level.GetEntity(2).Id;
            EntityId player = level.GetEntity(1).Id;

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(535));

            Assert.Equal(80, level.GetEntity(1).HealthBody!.Current);
            Assert.Contains(new DamageApplied(player, source, DamageKind.Shake, 20, 80), events);
        }

        /// <summary>Verifies the weight uses the original hard-shake impact.</summary>
        [Fact]
        public void WeightHardShakesPlayerOnLanding()
        {
            MaterializedLevel level = Materialize("OWeight", DifficultySpeeds.Standard, objectY: 1);
            EntityId source = level.GetEntity(2).Id;
            EntityId player = level.GetEntity(1).Id;

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(268));

            Assert.Equal(70, level.GetEntity(1).HealthBody!.Current);
            Assert.Contains(new DamageApplied(player, source, DamageKind.HardShake, 30, 70), events);
        }

        /// <summary>Verifies difficulty zero uses its recovered impact amounts.</summary>
        [Theory]
        [InlineData("ORock01", DamageKind.Shake, 10)]
        [InlineData("OWeight", DamageKind.HardShake, 15)]
        public void DifficultyZeroUsesRecoveredImpactDamage(
            string archivalType,
            DamageKind expectedKind,
            double expectedDamage)
        {
            MaterializedLevel level = Materialize(archivalType, DifficultySpeeds.DifficultyZero, objectY: 1);
            EntityId source = level.GetEntity(2).Id;
            EntityId player = level.GetEntity(1).Id;

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(713));

            Assert.Equal(100 - expectedDamage, level.GetEntity(1).HealthBody!.Current);
            Assert.Contains(
                new DamageApplied(player, source, expectedKind, expectedDamage, 100 - expectedDamage),
                events);
        }

        /// <summary>Verifies support without a falling segment does not count as an impact.</summary>
        [Fact]
        public void InitiallySupportedObjectDoesNotDamagePlayer()
        {
            MaterializedLevel level = Materialize("ORock01", DifficultySpeeds.Standard, objectY: 2);

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(100, level.GetEntity(1).HealthBody!.Current);
            Assert.DoesNotContain(events, value => value is DamageApplied);
        }

        /// <summary>Verifies the explicit archival hard-impact mapping.</summary>
        [Fact]
        public void OnlyWeightMaterializesWithHardImpact()
        {
            MaterializedLevel level = LevelMaterializer.Materialize(CreateLevel(
                Object(1, "OPlayer", 0, 4),
                Object(2, "ORock01", 1, 1),
                Object(3, "OCoin01", 2, 1),
                Object(4, "OWeight", 3, 1)));

            Assert.Equal(GravityImpactStrength.Ordinary, level.GetEntity(2).GravityBody!.ImpactStrength);
            Assert.Equal(GravityImpactStrength.Ordinary, level.GetEntity(3).GravityBody!.ImpactStrength);
            Assert.Equal(GravityImpactStrength.Hard, level.GetEntity(4).GravityBody!.ImpactStrength);
        }

        /// <summary>Verifies an impact can stop a player already queued for boundary resolution.</summary>
        [Fact]
        public void SimultaneousPlayerBoundaryIsSafelyStoppedByImpact()
        {
            WorldGrid world = new(5, 5);
            Entity player = new(
                EntityId.New(),
                new GridPosition(1, 3),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2, 1.5, Direction.Right),
                healthBody: new HealthBody(100, 100, TimeSpan.FromSeconds(1)));
            Entity rock = new(
                EntityId.New(),
                new GridPosition(2, 1),
                InteractionMask.BlocksMovement,
                new GravityBody(2, canSlide: false));
            world.Add(player);
            world.Add(rock);
            SimulationWorld simulation = new(world, Direction.Down, new DefaultGravityCollisionPolicy());
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(500));

            Assert.Equal(80, player.HealthBody!.Current);
            Assert.Null(player.Motion);
            Assert.Contains(new DamageApplied(player.Id, rock.Id, DamageKind.Shake, 20, 80), events);
        }

        private static MaterializedLevel Materialize(
            string archivalType,
            DifficultySpeeds profile,
            int objectY)
        {
            return LevelMaterializer.Materialize(
                CreateLevel(
                    Object(1, "OPlayer", 2, 3),
                    Object(2, archivalType, 2, objectY)),
                Direction.Down,
                profile);
        }

        private static LevelDocument CreateLevel(params LevelObject[] objects)
        {
            return new LevelDocument(
                1,
                null,
                new LevelSize(5, 5),
                new EditorPreview(0, 0, 5, 5),
                objects);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(
                id,
                type,
                0,
                new LevelPosition(x, y),
                [],
                new Dictionary<string, System.Text.Json.JsonElement>());
        }
    }
}
