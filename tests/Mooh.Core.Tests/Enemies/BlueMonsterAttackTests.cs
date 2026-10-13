using System;
using System.Collections.Generic;
using System.Linq;

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
    /// <summary>Verifies recovered blue-monster strikes and turnaround.</summary>
    public sealed class BlueMonsterAttackTests
    {
        /// <summary>Verifies a forward player takes priority over the open left cell.</summary>
        [Fact]
        public void ForwardPlayerStartsAttackWindup()
        {
            WorldGrid world = new(3, 3);
            Entity monster = NewMonster(1, 1, Direction.Right);
            Entity player = NewPlayer(2, 1);
            world.Add(monster);
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(BlueMonsterState.AttackWindup, monster.BlueMonsterBody!.State);
            Assert.Equal(TimeSpan.FromMilliseconds(599), monster.BlueMonsterBody.Remaining);
            Assert.Equal(
                new EnemyAttackStarted(
                    monster.Id,
                    new GridPosition(2, 1),
                    TimeSpan.FromMilliseconds(600)),
                Assert.Single(events));
        }

        /// <summary>Verifies the strike lands after exactly 600 milliseconds.</summary>
        [Fact]
        public void StrikeLandsAfterRecoveredWindup()
        {
            WorldGrid world = new(2, 1);
            Entity monster = NewMonster(0, 0, Direction.Right);
            Entity player = NewPlayer(1, 0);
            world.Add(monster);
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> beforeImpact = simulation.Advance(
                TimeSpan.FromMilliseconds(599));

            Assert.Equal(100, player.HealthBody!.Current);
            Assert.Equal(BlueMonsterState.AttackWindup, monster.BlueMonsterBody!.State);
            Assert.Equal(TimeSpan.FromMilliseconds(1), monster.BlueMonsterBody.Remaining);
            _ = Assert.Single(beforeImpact);

            IReadOnlyList<SimulationEvent> impact = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(80, player.HealthBody.Current);
            Assert.Equal(PlayerReactionKind.Shocked, player.PlayerBody!.Reaction.Kind);
            Assert.Equal(BlueMonsterState.AttackRecovery, monster.BlueMonsterBody.State);
            Assert.Equal(TimeSpan.FromMilliseconds(850), monster.BlueMonsterBody.Remaining);
            Assert.Equal(
                [
                    new EnemyStrike(monster.Id, new GridPosition(1, 0)),
                    new DamageApplied(player.Id, monster.Id, DamageKind.Strike, 20, 80),
                    new PlayerShockStarted(player.Id, TimeSpan.FromMilliseconds(700)),
                    new EnemyTurnStarted(
                        monster.Id,
                        Direction.Right,
                        Direction.Left,
                        TimeSpan.FromMilliseconds(1150)),
                ],
                impact);
        }

        /// <summary>Verifies a player leaving the forward cell during wind-up avoids damage.</summary>
        [Fact]
        public void PlayerLeavingDuringWindupAvoidsDamage()
        {
            WorldGrid world = new(3, 1);
            Entity monster = NewMonster(0, 0, Direction.Right);
            Entity player = NewPlayer(1, 0);
            world.Add(monster);
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(600));

            Assert.Equal(100, player.HealthBody!.Current);
            Assert.Equal(new GridPosition(2, 0), player.Position);
            Assert.Contains(events, simulationEvent => simulationEvent is EnemyStrike);
            Assert.DoesNotContain(events, simulationEvent => simulationEvent is DamageApplied);
        }

        /// <summary>Verifies recovered post-strike duration depends on the attack direction.</summary>
        /// <param name="direction">The attack direction.</param>
        /// <param name="expectedTotalMilliseconds">The expected total turnaround duration.</param>
        /// <param name="expectedPhaseMilliseconds">The expected initial recovery-phase duration.</param>
        [Theory]
        [InlineData(Direction.Left, 985, 685)]
        [InlineData(Direction.Up, 1150, 850)]
        [InlineData(Direction.Right, 1150, 850)]
        [InlineData(Direction.Down, 1150, 850)]
        public void StrikeStartsRecoveredTurnaround(
            Direction direction,
            int expectedTotalMilliseconds,
            int expectedPhaseMilliseconds)
        {
            (WorldGrid world, Entity monster, _) = NewFacingScenario(direction);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(600));
            EnemyTurnStarted turnaround = Assert.Single(events.OfType<EnemyTurnStarted>());

            Assert.Equal(BlueMonsterState.AttackRecovery, monster.BlueMonsterBody!.State);
            Assert.Equal(
                TimeSpan.FromMilliseconds(expectedPhaseMilliseconds),
                monster.BlueMonsterBody.Remaining);
            Assert.Equal(TimeSpan.FromMilliseconds(expectedTotalMilliseconds), turnaround.Duration);
        }

        /// <summary>Verifies turnaround completion leaves the monster facing away from its strike.</summary>
        [Fact]
        public void CompletedTurnaroundFacesOppositeDirection()
        {
            (WorldGrid world, Entity monster, _) = NewFacingScenario(Direction.Left);
            SimulationWorld simulation = CreateSimulation(world);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(600));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(985));

            Assert.Equal(Direction.Right, monster.BlueMonsterBody!.FacingDirection);
            Assert.Contains(
                new EnemyTurnCompleted(monster.Id, Direction.Right),
                events);
        }

        /// <summary>Verifies facing changes for the recovered final 300-millisecond turn phase.</summary>
        [Fact]
        public void FacingChangesBeforeTurnaroundCompletes()
        {
            (WorldGrid world, Entity monster, _) = NewFacingScenario(Direction.Left);
            SimulationWorld simulation = CreateSimulation(world);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(600));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(685));

            Assert.Equal(Direction.Right, monster.BlueMonsterBody!.FacingDirection);
            Assert.Equal(BlueMonsterState.AttackTurnaround, monster.BlueMonsterBody.State);
            Assert.Equal(TimeSpan.FromMilliseconds(300), monster.BlueMonsterBody.Remaining);
            Assert.DoesNotContain(events, simulationEvent => simulationEvent is EnemyTurnCompleted);
        }

        /// <summary>Verifies the post-strike turnaround resumes the forward-only decision.</summary>
        [Fact]
        public void CompletedTurnaroundChecksForwardBeforeTurningLeft()
        {
            (WorldGrid world, Entity monster, _) = NewFacingScenario(Direction.Right);
            SimulationWorld simulation = CreateSimulation(world);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(600));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1150));

            Assert.Equal(Direction.Left, monster.BlueMonsterBody!.FacingDirection);
            Assert.Equal(BlueMonsterState.Walking, monster.BlueMonsterBody.State);
            Assert.Contains(
                new EnemyMoveStarted(monster.Id, new GridPosition(1, 1), new GridPosition(0, 1)),
                events);
            Assert.DoesNotContain(events, simulationEvent => simulationEvent is EnemyTurnStarted);
        }

        /// <summary>
        /// Verifies simultaneous green and blue strikes resolve in registration order. The green
        /// strike shocks the player first, which clears its strike bits, so the blue strike that
        /// lands in the same instant finds no target.
        /// </summary>
        [Fact]
        public void SimultaneousMixedStrikesRespectImmunityInRegistrationOrder()
        {
            WorldGrid world = new(3, 1);
            Entity green = new(
                EntityId.New(),
                new GridPosition(0, 0),
                InteractionMask.BlocksMovement,
                greenMonsterBody: new GreenMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(250),
                    attackWindupDuration: TimeSpan.FromMilliseconds(600)));
            Entity player = NewPlayer(1, 0);
            Entity blue = NewMonster(2, 0, Direction.Left);
            world.Add(green);
            world.Add(player);
            world.Add(blue);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(600));
            SimulationEvent[] damageEvents =
            [
                .. events.Where(
                    simulationEvent => simulationEvent is DamageApplied or DamageIgnored),
            ];

            Assert.Equal(80, player.HealthBody!.Current);
            Assert.Equal(
                [
                    new DamageApplied(player.Id, green.Id, DamageKind.Strike, 20, 80),
                ],
                damageEvents);
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

        private static (WorldGrid World, Entity Monster, Entity Player) NewFacingScenario(
            Direction direction)
        {
            WorldGrid world = new(3, 3);
            Entity monster = NewMonster(1, 1, direction);
            GridPosition playerPosition = monster.Position.Offset(direction);
            Entity player = NewPlayer(playerPosition.X, playerPosition.Y);
            world.Add(monster);
            world.Add(player);
            return (world, monster, player);
        }

        private static Entity NewMonster(int x, int y, Direction facingDirection)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(x, y),
                InteractionMask.BlocksMovement,
                blueMonsterBody: new BlueMonsterBody(
                    1.5625,
                    facingDirection,
                    TimeSpan.FromMilliseconds(300)));
        }

        private static Entity NewPlayer(int x, int y)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(x, y),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right),
                healthBody: new HealthBody(100, 100, TimeSpan.FromMilliseconds(500)));
        }
    }
}
