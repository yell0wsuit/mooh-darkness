using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>Verifies restoration of the current enemy action after a real ice pickup.</summary>
    public sealed class MonsterThawTests
    {
        /// <summary>Thaw restarts the current animation phase, rather than its leftover timer or the whole chain.</summary>
        [Theory]
        [InlineData(false, 100, 625)]
        [InlineData(false, 500, 250)]
        [InlineData(false, 700, 500)]
        [InlineData(true, 100, 600)]
        [InlineData(true, 500, 150)]
        [InlineData(true, 700, 685)]
        [InlineData(true, 1050, 300)]
        [InlineData(true, 1350, 300)]
        public void ThawRestartsTheInterruptedAttackPhase(bool blue, int freezeAtMilliseconds, int expectedMilliseconds)
        {
            (WorldGrid _, Entity player, Entity monster, SimulationWorld simulation) = Create(blue);
            Freeze(simulation, monster, freezeAtMilliseconds);
            simulation.SetPlayerInput(default);

            IReadOnlyList<SimulationEvent> thawed = simulation.AdvanceBonusTimers(player.PlayerBody!.Progress.IceRemaining);

            Assert.Contains(thawed, item => item == new EnemyThawed(monster.Id));
            Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), Remaining(monster));
            IReadOnlyList<SimulationEvent> waiting = simulation.Advance(TimeSpan.FromMilliseconds(expectedMilliseconds - 1));
            Assert.DoesNotContain(waiting, item => item is EnemyStrike or EnemyTurnCompleted);
            Assert.Equal(TimeSpan.FromMilliseconds(1), Remaining(monster));
            IReadOnlyList<SimulationEvent> completed = simulation.Advance(TimeSpan.FromMilliseconds(1));
            if (blue && freezeAtMilliseconds is 700 or 1050)
            {
                Assert.Equal(BlueMonsterState.AttackTurnaround, monster.BlueMonsterBody!.State);
                Assert.Equal(TimeSpan.FromMilliseconds(300), Remaining(monster));
            }
            else
            {
                Assert.Contains(completed, item => item is EnemyStrike or EnemyTurnStarted or EnemyTurnCompleted);
            }
        }

        /// <summary>A player back in strike range receives damage only after the restarted strike phase completes.</summary>
        [Theory]
        [InlineData(false, 250)]
        [InlineData(true, 150)]
        public void ThawedAttacksCannotDamageThePlayerEarly(bool blue, int expectedMilliseconds)
        {
            (WorldGrid _, Entity player, Entity monster, SimulationWorld simulation) = Create(blue);
            Freeze(simulation, monster, 500);
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = simulation.Advance(TimeSpan.FromMilliseconds(700));
            simulation.SetPlayerInput(default);
            Assert.Equal(new GridPosition(2, 1), player.Position);
            Assert.True(monster.IsFrozenMonster);
            _ = simulation.AdvanceBonusTimers(player.PlayerBody!.Progress.IceRemaining);
            double health = player.HealthBody!.Current;

            IReadOnlyList<SimulationEvent> early = simulation.Advance(TimeSpan.FromMilliseconds(expectedMilliseconds - 1));

            Assert.DoesNotContain(early, item => item is DamageApplied);
            Assert.Equal(health, player.HealthBody.Current);
            IReadOnlyList<SimulationEvent> impact = simulation.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Contains(impact, item => item is DamageApplied damage && damage.EntityId == player.Id);
            Assert.Equal(health - 20, player.HealthBody.Current);
        }

        /// <summary>Ordinary bonus expiration must not charge the preceding frozen interval to the restarted action.</summary>
        [Theory]
        [InlineData(false, 250)]
        [InlineData(true, 150)]
        public void NormalExpirationPreservesTheFullRestartedPhase(bool blue, int expectedMilliseconds)
        {
            (WorldGrid _, Entity player, Entity monster, SimulationWorld simulation) = Create(blue);
            Freeze(simulation, monster, 500);
            simulation.SetPlayerInput(default);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(player.PlayerBody!.Progress.IceRemaining);

            Assert.Contains(events, item => item == new EnemyThawed(monster.Id));
            Assert.DoesNotContain(events, item => item is EnemyStrike or EnemyTurnCompleted);
            Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), Remaining(monster));
        }

        /// <summary>A partially completed turn plays its full duration again after thaw.</summary>
        [Theory]
        [InlineData(false, 250)]
        [InlineData(true, 300)]
        public void ThawRestartsTurns(bool blue, int expectedMilliseconds)
        {
            (WorldGrid world, Entity player, Entity monster, SimulationWorld simulation) = Create(blue, Direction.Right);
            _ = Solid(world, new GridPosition(4, 1));
            _ = Solid(world, new GridPosition(3, 0));
            Freeze(simulation, monster, 100);
            simulation.SetPlayerInput(default);

            _ = simulation.AdvanceBonusTimers(player.PlayerBody!.Progress.IceRemaining);

            Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), Remaining(monster));
            Assert.DoesNotContain(simulation.Advance(TimeSpan.FromMilliseconds(expectedMilliseconds - 1)),
                item => item is EnemyTurnCompleted);
            Assert.Contains(simulation.Advance(TimeSpan.FromMilliseconds(1)),
                item => item == new EnemyTurnCompleted(monster.Id, Direction.Down));
        }

        /// <summary>Thaw resumes the saved walk directly even if frozen-time changes would alter an AI decision.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ThawResumesTheInterruptedWalkWithoutChoosingANewAction(bool blue)
        {
            (WorldGrid world, Entity player, Entity monster, SimulationWorld simulation) = Create(blue, Direction.Right);
            Entity upperWall = Solid(world, new GridPosition(3, 0));
            Freeze(simulation, monster, 100);
            simulation.SetPlayerInput(default);
            Assert.Equal(new GridPosition(3, 1), monster.Position);
            _ = world.Remove(upperWall.Id);
            if (!blue)
            {
                _ = Solid(world, new GridPosition(4, 1));
            }

            IReadOnlyList<SimulationEvent> events = simulation.AdvanceBonusTimers(player.PlayerBody!.Progress.IceRemaining);

            Assert.Contains(events, item => item is EnemyMoveStarted started && started.EntityId == monster.Id);
            Assert.DoesNotContain(events, item => item is EnemyTurnStarted or EnemyAttackStarted);
            Assert.Equal(new GridPosition(4, 1), monster.Position);
            Assert.NotNull(monster.Motion);
            Assert.Equal(Direction.Right, monster.Motion.TravelDirection);
            Assert.Equal(0, monster.Motion.Progress);
        }

        private static void Freeze(SimulationWorld simulation, Entity monster, int milliseconds)
        {
            _ = simulation.Advance(TimeSpan.FromMilliseconds(milliseconds));
            simulation.SetPlayerInput(new PlayerInputState(true, false, false, false));
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Contains(events, item => item is PickupCollected);
            Assert.Contains(events, item => item == new EnemyFrozen(monster.Id));
            Assert.True(monster.IsFrozenMonster);
        }

        private static TimeSpan Remaining(Entity monster)
        {
            return monster.GreenMonsterBody?.Remaining ?? monster.BlueMonsterBody!.Remaining;
        }

        private static (WorldGrid, Entity, Entity, SimulationWorld) Create(bool blue, Direction facing = Direction.Left)
        {
            WorldGrid world = new(8, 4);
            Entity player = new(EntityId.New(), new GridPosition(2, 1), InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Left, TimeSpan.FromMilliseconds(1)),
                healthBody: new HealthBody(100, 100, TimeSpan.Zero));
            world.Add(player);
            Entity monster = new(EntityId.New(), new GridPosition(3, 1), InteractionMask.BlocksMovement,
                greenMonsterBody: blue ? null : new GreenMonsterBody(1.5625, facing, TimeSpan.FromMilliseconds(250)),
                blueMonsterBody: blue ? new BlueMonsterBody(1.5625, facing, TimeSpan.FromMilliseconds(300)) : null);
            world.Add(monster);
            ArchivalObjectDescriptor descriptor = ArchivalObjectCatalog.Get("BIce");
            LevelObject source = new(2, "BIce", 0, new LevelPosition(1, 1), [], null);
            world.Add(new Entity(EntityId.New(), new GridPosition(1, 1), descriptor.Interactions,
                archivalIdentity: new ArchivalObjectIdentity(source, descriptor)));
            for (int x = 0; x < 8; x++)
            {
                _ = Solid(world, new GridPosition(x, 2));
            }

            SimulationWorld simulation = new(world, Direction.Down, new DefaultGravityCollisionPolicy(),
                new Random(0), 0.5, DifficultySpeeds.Standard with
                {
                    HealthDrainPerSecond = 0
                });
            return (world, player, monster, simulation);
        }

        private static Entity Solid(WorldGrid world, GridPosition cell)
        {
            Entity entity = new(EntityId.New(), cell, InteractionMask.BlocksMovement | InteractionMask.MovementLocked);
            world.Add(entity);
            return entity;
        }
    }
}
