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

namespace Mooh.Core.Tests.Combat
{
    /// <summary>
    /// Verifies the recovered passive drain of <c>Game.ModCycle</c>: outside timed play and outside
    /// every <c>bImmort</c> state, health falls by <c>CNT_GPrmHealthSp</c> per millisecond, which
    /// empties a full bar in fifteen minutes.
    /// </summary>
    public sealed class HealthDrainTests
    {
        /// <summary>Verifies health falls at the recovered rate.</summary>
        [Fact]
        public void HealthFallsAtTheRecoveredRate()
        {
            (SimulationWorld simulation, Entity player) = Create(DifficultySpeeds.Standard);

            _ = simulation.Advance(TimeSpan.FromSeconds(9));

            Assert.Equal(99.0, player.HealthBody!.Current, 9);
        }

        /// <summary>Verifies the drain bypasses <c>OnHelthLost</c> and so arms no immunity.</summary>
        [Fact]
        public void DrainArmsNoImmunity()
        {
            (SimulationWorld simulation, Entity player) = Create(DifficultySpeeds.Standard);
            _ = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(TimeSpan.Zero, player.HealthBody!.ImmunityRemaining);
            IReadOnlyList<SimulationEvent> hit = simulation.ApplyDamage(
                new DamageRequest(player.Id, player.Id, DamageKind.Fire, 10));
            Assert.Contains(hit, item => item is DamageApplied);
        }

        /// <summary>Verifies a shocked player, being <c>bImmort</c>, does not drain.</summary>
        [Fact]
        public void ShockedPlayerDoesNotDrain()
        {
            (SimulationWorld simulation, Entity player) = Create(DifficultySpeeds.Standard);
            _ = simulation.ApplyDamage(new DamageRequest(player.Id, player.Id, DamageKind.Fire, 10));

            _ = simulation.Advance(TimeSpan.FromMilliseconds(600));

            Assert.Equal(90, player.HealthBody!.Current);
        }

        /// <summary>Verifies timed play replaces health with the clock and never drains.</summary>
        [Fact]
        public void TimedDifficultyDoesNotDrain()
        {
            (SimulationWorld simulation, Entity player) = Create(DifficultySpeeds.Timed);

            _ = simulation.Advance(TimeSpan.FromSeconds(9));

            Assert.Equal(100, player.HealthBody!.Current);
        }

        /// <summary>
        /// Verifies the drain alone loses the level: the loss branch puts the player into
        /// <c>ST_Dead</c> when it first observes empty health, then settles for 1500 ms.
        /// </summary>
        [Fact]
        public void SustainedDrainLosesTheLevel()
        {
            (SimulationWorld simulation, Entity player) = Create(DifficultySpeeds.Standard, health: 0.5);

            IReadOnlyList<SimulationEvent> dying = simulation.Advance(TimeSpan.FromSeconds(5));

            Assert.True(player.HealthBody!.IsDead);
            Assert.Contains(dying, item => item == new PlayerDied(player.Id));
            Assert.Equal(PlayerReactionKind.Dead, player.PlayerBody!.Reaction.Kind);
            Assert.Equal(LevelOutcome.InProgress, simulation.Outcome);

            IReadOnlyList<SimulationEvent> settled = simulation.Advance(TimeSpan.FromMilliseconds(1100));

            Assert.Contains(settled, item => item is LevelLost);
            Assert.Equal(LevelOutcome.Lost, simulation.Outcome);
        }

        private static (SimulationWorld Simulation, Entity Player) Create(
            DifficultySpeeds speeds,
            double health = 100)
        {
            WorldGrid world = new(3, 3);
            Entity player = new(
                EntityId.New(),
                new GridPosition(1, 1),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Down),
                healthBody: new HealthBody(100, health, TimeSpan.FromMilliseconds(650)));
            world.Add(player);
            SimulationWorld simulation = new(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                speeds);
            return (simulation, player);
        }
    }
}
