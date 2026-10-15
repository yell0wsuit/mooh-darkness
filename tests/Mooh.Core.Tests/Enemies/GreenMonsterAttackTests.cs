using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>Verifies source-backed green-monster attack timing and damage.</summary>
    public sealed class GreenMonsterAttackTests
    {
        /// <summary>Verifies attack durations must be positive.</summary>
        /// <param name="windupMilliseconds">The candidate wind-up duration.</param>
        /// <param name="recoveryMilliseconds">The candidate recovery duration.</param>
        [Theory]
        [InlineData(0, 500)]
        [InlineData(625, 0)]
        public void BodyRejectsInvalidAttackDuration(
            int windupMilliseconds,
            int recoveryMilliseconds)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new GreenMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(250),
                    TimeSpan.FromMilliseconds(windupMilliseconds),
                    TimeSpan.FromMilliseconds(recoveryMilliseconds)));
        }

        /// <summary>Verifies strike damage must be finite and positive.</summary>
        /// <param name="strikeDamage">The candidate strike damage.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void BodyRejectsInvalidStrikeDamage(double strikeDamage)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new GreenMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(250),
                    strikeDamage: strikeDamage));
        }

        /// <summary>Verifies an attackable player ahead starts a 625-millisecond wind-up.</summary>
        [Fact]
        public void PlayerAheadStartsSourceBackedWindup()
        {
            (SimulationWorld simulation, Entity monster, Entity player) = CreateFacingPair();

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(624));

            Assert.Equal(GreenMonsterState.AttackWindup, monster.GreenMonsterBody!.State);
            Assert.Equal(TimeSpan.FromMilliseconds(1), monster.GreenMonsterBody.Remaining);
            Assert.Null(monster.Motion);
            Assert.Equal(100, player.HealthBody!.Current);
            Assert.Equal(
                new EnemyAttackStarted(
                    monster.Id,
                    new GridPosition(1, 0),
                    TimeSpan.FromMilliseconds(625)),
                Assert.Single(events));
        }

        /// <summary>
        /// Verifies a shocked player is not a target. Every recovered shock state is
        /// <c>bImmort</c>, and <c>StateSet</c> clears <c>CNT_OrMaskMonStrike</c> for such a state,
        /// so the monster facing it turns away instead of winding up.
        /// </summary>
        [Fact]
        public void ShockedPlayerAheadIsNotATarget()
        {
            (SimulationWorld simulation, Entity monster, Entity player) = CreateFacingPair();
            _ = simulation.ApplyDamage(new DamageRequest(player.Id, player.Id, DamageKind.Fire, 10));
            Assert.Equal(PlayerReactionKind.Shocked, player.PlayerBody!.Reaction.Kind);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(10));

            Assert.DoesNotContain(events, item => item is EnemyAttackStarted);
            Assert.Equal(GreenMonsterState.Turning, monster.GreenMonsterBody!.State);
            Assert.True(player.PlayerBody.IsImmortal);
        }

        /// <summary>Verifies monsters decide before held input reserves an escape cell.</summary>
        [Fact]
        public void InitialMonsterDecisionPrecedesPlayerInput()
        {
            (SimulationWorld simulation, Entity monster, Entity player) = CreateFacingPair(
                width: 2,
                height: 2,
                playerFacing: Direction.Up);
            simulation.SetPlayerInput(new PlayerInputState(false, true, false, false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(GreenMonsterState.AttackWindup, monster.GreenMonsterBody!.State);
            Assert.Equal(new GridPosition(1, 0), player.Position);
            Assert.Equal(
                [
                    new EnemyAttackStarted(
                        monster.Id,
                        new GridPosition(1, 1),
                        TimeSpan.FromMilliseconds(625)),
                    new PlayerMoveStarted(player.Id, Direction.Up),
                ],
                events);
        }

        /// <summary>Verifies the strike fires after 625 milliseconds and routes 20 shake damage.</summary>
        [Fact]
        public void WindupCompletionStrikesPlayer()
        {
            (SimulationWorld simulation, Entity monster, Entity player) = CreateFacingPair();
            EntityId monsterId = monster.Id;
            _ = simulation.Advance(TimeSpan.FromMilliseconds(624));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(GreenMonsterState.AttackRecovery, monster.GreenMonsterBody!.State);
            Assert.Equal(TimeSpan.FromMilliseconds(500), monster.GreenMonsterBody.Remaining);
            Assert.Equal(80, player.HealthBody!.Current);
            Assert.Equal(TimeSpan.FromMilliseconds(500), player.HealthBody.ImmunityRemaining);
            Assert.Equal(PlayerReactionKind.Shocked, player.PlayerBody!.Reaction.Kind);
            Assert.Equal(TimeSpan.FromMilliseconds(700), player.PlayerBody.Reaction.Remaining);
            Assert.Equal(
                [
                    new EnemyStrike(monsterId, new GridPosition(1, 0)),
                    new DamageApplied(player.Id, monsterId, DamageKind.Strike, 20, 80),
                    new PlayerShockStarted(player.Id, TimeSpan.FromMilliseconds(700)),
                    new ShakeImpact(monsterId, 54.0 / 80, 0),
                ],
                events);
        }

        /// <summary>Verifies the strike rechecks its target cell after the player leaves.</summary>
        [Fact]
        public void PlayerLeavingDuringWindupAvoidsDamage()
        {
            (SimulationWorld simulation, Entity monster, Entity player) = CreateFacingPair(
                width: 2,
                height: 2,
                playerFacing: Direction.Up);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(1));
            simulation.SetPlayerInput(new PlayerInputState(false, true, false, false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(624));

            Assert.Equal(new GridPosition(1, 0), player.Position);
            Assert.Equal(100, player.HealthBody!.Current);
            Assert.Equal(GreenMonsterState.AttackRecovery, monster.GreenMonsterBody!.State);
            Assert.Contains(new EnemyStrike(monster.Id, new GridPosition(1, 1)), events);
            Assert.DoesNotContain(events, simulationEvent => simulationEvent is DamageApplied);
        }

        /// <summary>Verifies attack recovery always transitions into the clockwise turn.</summary>
        [Fact]
        public void RecoveryTransitionsToClockwiseTurn()
        {
            (SimulationWorld simulation, Entity monster, _) = CreateFacingPair(height: 2);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(625));

            IReadOnlyList<SimulationEvent> beforeRecovery = simulation.Advance(
                TimeSpan.FromMilliseconds(499));
            IReadOnlyList<SimulationEvent> atRecovery = simulation.Advance(
                TimeSpan.FromMilliseconds(1));

            Assert.Empty(beforeRecovery);
            Assert.Equal(GreenMonsterState.Turning, monster.GreenMonsterBody!.State);
            Assert.Equal(Direction.Right, monster.GreenMonsterBody.FacingDirection);
            Assert.Equal(TimeSpan.FromMilliseconds(250), monster.GreenMonsterBody.Remaining);
            Assert.Equal(
                new EnemyTurnStarted(
                    monster.Id,
                    Direction.Right,
                    Direction.Down,
                    TimeSpan.FromMilliseconds(250)),
                Assert.Single(atRecovery));
        }

        /// <summary>Verifies recovered held input cannot escape a strike expiring at the same instant.</summary>
        [Fact]
        public void SimultaneousShockRecoveryDefersPlayerControlUntilAfterStrike()
        {
            WorldGrid world = new(2, 2);
            Entity monster = new(
                EntityId.New(),
                new GridPosition(0, 1),
                InteractionMask.BlocksMovement,
                greenMonsterBody: new GreenMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(250),
                    attackWindupDuration: TimeSpan.FromMilliseconds(700)));
            Entity player = NewPlayer(1, 1, Direction.Up);
            world.Add(monster);
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);

            // The windup has to start before the shock, because a shocked player is bImmort and
            // carries no CNT_OrMaskMonStrike for the monster to select.
            IReadOnlyList<SimulationEvent> decision = simulation.Advance(TimeSpan.FromTicks(1));
            EntityId source = EntityId.New();
            _ = simulation.ApplyDamage(
                new DamageRequest(source, player.Id, DamageKind.Shake, 20));
            simulation.SetPlayerInput(new PlayerInputState(false, true, false, false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(
                TimeSpan.FromMilliseconds(700) - TimeSpan.FromTicks(1));

            Assert.Equal(
                new EnemyAttackStarted(monster.Id, player.Position, TimeSpan.FromMilliseconds(700)),
                Assert.Single(decision));
            Assert.Equal(new GridPosition(1, 1), player.Position);
            Assert.Equal(60, player.HealthBody!.Current);
            Assert.Equal(PlayerReactionKind.Shocked, player.PlayerBody!.Reaction.Kind);
            Assert.Equal(
                [
                    new PlayerShockEnded(player.Id),
                    new EnemyStrike(monster.Id, player.Position),
                    new DamageApplied(player.Id, monster.Id, DamageKind.Strike, 20, 60),
                    new PlayerShockStarted(player.Id, TimeSpan.FromMilliseconds(700)),
                    new ShakeImpact(monster.Id, monster.Position.X + (54.0 / 80), monster.Position.Y),
                ],
                events);
        }

        /// <summary>Verifies suspended reversal resumes only after a simultaneous strike rechecks its cell.</summary>
        [Fact]
        public void SimultaneousReverseCompletionResumesAfterStrike()
        {
            WorldGrid world = new(4, 2);
            Entity monster = new(
                EntityId.New(),
                new GridPosition(0, 1),
                InteractionMask.BlocksMovement,
                greenMonsterBody: new GreenMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(250),
                    attackWindupDuration: TimeSpan.FromMilliseconds(212)));
            Entity player = NewPlayer(1, 1, Direction.Right);
            world.Add(monster);
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(1));
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = simulation.Advance(TimeSpan.FromMilliseconds(1));
            simulation.SetPlayerInput(new PlayerInputState(true, false, false, false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(210));

            Assert.Equal(100, player.HealthBody!.Current);
            Assert.Equal(new GridPosition(1, 1), player.Position);
            Assert.Equal(Direction.Left, player.Motion!.TravelDirection);
            Assert.Equal(
                [
                    new PlayerRotationStarted(player.Id, Direction.Right, Direction.Left),
                    new PlayerRotationCompleted(player.Id, Direction.Left),
                    new EnemyStrike(monster.Id, new GridPosition(1, 1)),
                    new PlayerMoveStarted(player.Id, Direction.Left),
                ],
                events);
        }

        /// <summary>
        /// Verifies simultaneous attackers resolve in registration order. The first strike shocks
        /// the player, and a shocked player carries no <c>CNT_CMaskMonGrStrike</c>, so the second
        /// monster's <c>_AEnd_StrAll</c> finds no target and asks for no damage at all.
        /// </summary>
        [Fact]
        public void SimultaneousStrikesFollowRegistrationOrder()
        {
            WorldGrid world = new(3, 1);
            Entity first = NewMonster(0, 0, Direction.Right);
            Entity second = NewMonster(2, 0, Direction.Left);
            Entity player = NewPlayer(1, 0, Direction.Right);
            world.Add(first);
            world.Add(second);
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(625));

            Assert.Equal(80, player.HealthBody!.Current);
            Assert.Equal(
                [
                    new EnemyAttackStarted(
                        first.Id,
                        player.Position,
                        TimeSpan.FromMilliseconds(625)),
                    new EnemyAttackStarted(
                        second.Id,
                        player.Position,
                        TimeSpan.FromMilliseconds(625)),
                    new EnemyStrike(first.Id, player.Position),
                    new DamageApplied(player.Id, first.Id, DamageKind.Strike, 20, 80),
                    new PlayerShockStarted(player.Id, TimeSpan.FromMilliseconds(700)),
                    new ShakeImpact(first.Id, 54.0 / 80, 0),
                    new EnemyStrike(second.Id, player.Position),
                ],
                events);
        }

        private static (SimulationWorld Simulation, Entity Monster, Entity Player) CreateFacingPair(
            int width = 2,
            int height = 1,
            Direction playerFacing = Direction.Right)
        {
            WorldGrid world = new(width, height);
            int row = height - 1;
            Entity monster = NewMonster(0, row, Direction.Right);
            Entity player = NewPlayer(1, row, playerFacing);
            world.Add(monster);
            world.Add(player);
            return (CreateSimulation(world), monster, player);
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                Undrained.Standard);
        }

        private static Entity NewMonster(int x, int y, Direction direction)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(x, y),
                InteractionMask.BlocksMovement,
                greenMonsterBody: new GreenMonsterBody(
                    1.5625,
                    direction,
                    TimeSpan.FromMilliseconds(250)));
        }

        private static Entity NewPlayer(int x, int y, Direction direction)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(x, y),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, direction),
                healthBody: new HealthBody(100, 100, TimeSpan.FromMilliseconds(500)));
        }
    }
}
