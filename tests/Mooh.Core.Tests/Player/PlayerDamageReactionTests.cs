using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>Verifies player shock, immunity, recovery, and death behavior.</summary>
    public sealed class PlayerDamageReactionTests
    {
        /// <summary>Verifies nonlethal damage stops active walking and starts shock.</summary>
        [Fact]
        public void AppliedDamageStopsWalkingAndStartsShock()
        {
            (SimulationWorld simulation, Entity player) = CreateWorld(100);
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = simulation.Advance(TimeSpan.FromMilliseconds(100));
            Assert.NotNull(player.Motion);
            EntityId source = EntityId.New();

            IReadOnlyList<SimulationEvent> events = simulation.ApplyDamage(
                new DamageRequest(source, player.Id, DamageKind.Shake, 20));

            Assert.Null(player.Motion);
            Assert.Equal(PlayerReactionKind.Shocked, player.PlayerBody!.Reaction.Kind);
            Assert.Equal(TimeSpan.FromMilliseconds(700), player.PlayerBody.Reaction.Remaining);
            Assert.Equal(
                [
                    new DamageApplied(player.Id, source, DamageKind.Shake, 20, 80),
                    new PlayerShockStarted(player.Id, TimeSpan.FromMilliseconds(700)),
                ],
                events);
        }

        /// <summary>Verifies held input remains locked until shock ends, then starts movement.</summary>
        [Fact]
        public void ShockLocksThenRestoresHeldInput()
        {
            (SimulationWorld simulation, Entity player) = CreateWorld(100);
            EntityId source = EntityId.New();
            _ = simulation.ApplyDamage(new DamageRequest(source, player.Id, DamageKind.Shake, 20));
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> beforeRecovery = simulation.Advance(
                TimeSpan.FromMilliseconds(699));
            IReadOnlyList<SimulationEvent> atRecovery = simulation.Advance(
                TimeSpan.FromMilliseconds(1));

            Assert.Empty(beforeRecovery);
            Assert.Equal(TimeSpan.Zero, player.PlayerBody!.Reaction.Remaining);
            Assert.Equal(PlayerReactionKind.Normal, player.PlayerBody!.Reaction.Kind);
            Assert.Equal(
                [
                    new PlayerShockEnded(player.Id),
                    new PlayerMoveStarted(player.Id, Direction.Right),
                ],
                atRecovery);
            Assert.NotNull(player.Motion);
        }

        /// <summary>Verifies the shock state remains immune after the configured health timer expires.</summary>
        [Fact]
        public void ShockStateSuppressesDamageUntilRecovery()
        {
            (SimulationWorld simulation, Entity player) = CreateWorld(100);
            EntityId source = EntityId.New();
            _ = simulation.ApplyDamage(new DamageRequest(source, player.Id, DamageKind.Shake, 20));
            _ = simulation.Advance(TimeSpan.FromMilliseconds(600));

            IReadOnlyList<SimulationEvent> events = simulation.ApplyDamage(
                new DamageRequest(source, player.Id, DamageKind.Fire, 40));

            Assert.Equal(80, player.HealthBody!.Current);
            Assert.Equal(
                new DamageIgnored(
                    player.Id,
                    source,
                    DamageKind.Fire,
                    DamageResult.IgnoredImmune),
                Assert.Single(events));
        }

        /// <summary>Verifies lethal damage permanently locks player control.</summary>
        [Fact]
        public void LethalDamageStartsPermanentDeathReaction()
        {
            (SimulationWorld simulation, Entity player) = CreateWorld(10);
            EntityId source = EntityId.New();

            IReadOnlyList<SimulationEvent> damageEvents = simulation.ApplyDamage(
                new DamageRequest(source, player.Id, DamageKind.Explosion, 10));
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            IReadOnlyList<SimulationEvent> advanceEvents = simulation.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(PlayerReactionKind.Dead, player.PlayerBody!.Reaction.Kind);
            Assert.Null(player.Motion);
            Assert.Equal(
                [
                    new DamageApplied(player.Id, source, DamageKind.Explosion, 10, 0),
                    new PlayerDied(player.Id),
                ],
                damageEvents);
            Assert.Equal([new LevelLost(player.Id)], advanceEvents);
            Assert.Equal(new GridPosition(0, 0), player.Position);
        }

        private static (SimulationWorld Simulation, Entity Player) CreateWorld(double health)
        {
            WorldGrid world = new(3, 1);
            Entity player = new(
                EntityId.New(),
                new GridPosition(0, 0),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right),
                healthBody: new HealthBody(100, health, TimeSpan.FromMilliseconds(500)));
            world.Add(player);
            SimulationWorld simulation = new(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0));
            return (simulation, player);
        }
    }
}
