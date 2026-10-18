using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Combat
{
    /// <summary>Verifies deterministic health mutation and damage events.</summary>
    public sealed class DamageSystemTests
    {
        /// <summary>Verifies damage reduces health, starts immunity, and emits an applied event.</summary>
        [Fact]
        public void AppliesDamageAndStartsImmunity()
        {
            Entity source = NewEntity();
            Entity target = NewEntity(new HealthBody(100, 100, TimeSpan.FromMilliseconds(500)));
            List<SimulationEvent> events = [];

            DamageResult result = DamageSystem.Apply(
                target,
                new DamageRequest(source.Id, target.Id, DamageKind.Shake, 20),
                events);

            Assert.Equal(DamageResult.Applied, result);
            Assert.Equal(80, target.HealthBody!.Current);
            Assert.Equal(TimeSpan.FromMilliseconds(500), target.HealthBody.ImmunityRemaining);
            Assert.Equal(
                new DamageApplied(target.Id, source.Id, DamageKind.Shake, 20, 80),
                Assert.Single(events));
        }

        /// <summary>Verifies lethal damage clamps health to zero.</summary>
        [Fact]
        public void ClampsLethalDamageToZero()
        {
            Entity source = NewEntity();
            Entity target = NewEntity(new HealthBody(100, 15, TimeSpan.FromMilliseconds(500)));
            List<SimulationEvent> events = [];

            DamageResult result = DamageSystem.Apply(
                target,
                new DamageRequest(source.Id, target.Id, DamageKind.Explosion, 20),
                events);

            Assert.Equal(DamageResult.Applied, result);
            Assert.True(target.HealthBody!.IsDead);
            Assert.Equal(0, target.HealthBody.Current);
            Assert.Equal(
                new DamageApplied(target.Id, source.Id, DamageKind.Explosion, 20, 0),
                Assert.Single(events));
        }

        /// <summary>Verifies immunity suppresses repeated damage and emits a reason.</summary>
        [Fact]
        public void IgnoresDamageDuringImmunity()
        {
            Entity source = NewEntity();
            Entity target = NewEntity(new HealthBody(100, 100, TimeSpan.FromMilliseconds(500)));
            List<SimulationEvent> firstEvents = [];
            _ = DamageSystem.Apply(
                target,
                new DamageRequest(source.Id, target.Id, DamageKind.Shake, 20),
                firstEvents);
            List<SimulationEvent> repeatedEvents = [];

            DamageResult result = DamageSystem.Apply(
                target,
                new DamageRequest(source.Id, target.Id, DamageKind.Fire, 40),
                repeatedEvents);

            Assert.Equal(DamageResult.IgnoredImmune, result);
            Assert.Equal(80, target.HealthBody!.Current);
            Assert.Equal(
                new DamageIgnored(
                    target.Id,
                    source.Id,
                    DamageKind.Fire,
                    DamageResult.IgnoredImmune),
                Assert.Single(repeatedEvents));
        }

        /// <summary>Verifies dead targets reject subsequent damage.</summary>
        [Fact]
        public void IgnoresDamageToDeadTarget()
        {
            Entity source = NewEntity();
            Entity target = NewEntity(new HealthBody(10, 10, TimeSpan.Zero));
            List<SimulationEvent> firstEvents = [];
            _ = DamageSystem.Apply(
                target,
                new DamageRequest(source.Id, target.Id, DamageKind.Explosion, 10),
                firstEvents);
            List<SimulationEvent> repeatedEvents = [];

            DamageResult result = DamageSystem.Apply(
                target,
                new DamageRequest(source.Id, target.Id, DamageKind.Fire, 1),
                repeatedEvents);

            Assert.Equal(DamageResult.IgnoredDead, result);
            Assert.Equal(
                new DamageIgnored(
                    target.Id,
                    source.Id,
                    DamageKind.Fire,
                    DamageResult.IgnoredDead),
                Assert.Single(repeatedEvents));
        }

        /// <summary>Verifies a damage request must target the supplied entity.</summary>
        [Fact]
        public void RejectsMismatchedTarget()
        {
            Entity target = NewEntity(new HealthBody(100, 100, TimeSpan.Zero));
            DamageRequest request = new(EntityId.New(), EntityId.New(), DamageKind.Strike, 1);

            _ = Assert.Throws<ArgumentException>(() => DamageSystem.Apply(target, request, []));
        }

        private static Entity NewEntity(HealthBody? healthBody = null)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(0, 0),
                InteractionMask.None,
                healthBody: healthBody);
        }
    }
}
