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
    /// <summary>Verifies the recovered glasses-bonus behavior of Green Monsters.</summary>
    public sealed class GreenMonsterGlassesTests
    {
        /// <summary>Verifies glasses make a monster edible and replace attacks with small navigation.</summary>
        [Fact]
        public void GlassesMakeMonsterEdibleAndSuppressAttack()
        {
            WorldGrid world = new(5, 4);
            _ = AddPlayer(world, new GridPosition(2, 2));
            Entity monster = AddMonster(world, new GridPosition(1, 2));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> started = simulation.SetGreenMonsterGlasses(active: true);

            Assert.Equal(
                [new EnemyTransformationStarted(monster.Id, true, TimeSpan.FromMilliseconds(375))],
                started);
            Assert.True(monster.GreenMonsterBody!.IsSmall);
            Assert.True(monster.Interactions.HasFlag(InteractionMask.PlayerEdible));

            IReadOnlyList<SimulationEvent> completed = simulation.Advance(
                TimeSpan.FromMilliseconds(375));

            Assert.Contains(completed, item => item == new EnemyTransformationCompleted(monster.Id, true));
            Assert.DoesNotContain(completed, item => item is EnemyAttackStarted);
            EnemyTurnStarted turn = Assert.Single(completed, item => item is EnemyTurnStarted)
                as EnemyTurnStarted ?? throw new InvalidOperationException();
            Assert.Equal(TimeSpan.FromMilliseconds(250), turn.Duration);
        }

        /// <summary>Verifies eating a small Green Monster scores its kill without spawning a beetle.</summary>
        [Fact]
        public void PlayerEatsSmallMonsterWithoutSpawningBeetle()
        {
            WorldGrid world = new(5, 4);
            Entity player = AddPlayer(world, new GridPosition(1, 1));
            Entity monster = AddMonster(world, new GridPosition(2, 1));
            SimulationWorld simulation = CreateSimulation(world);
            _ = simulation.SetGreenMonsterGlasses(active: true);
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Contains(events, item => item == new PlayerAte(player.Id, monster.Id));
            Assert.DoesNotContain(events, item => item is SmallGreenBeetleSpawned);
            Assert.Equal(10, player.PlayerBody!.Progress.Score);
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(monster.Id));
        }

        /// <summary>Verifies removing glasses restores ordinary attacks after the recovered transition.</summary>
        [Fact]
        public void RemovingGlassesRestoresNormalAttackDecision()
        {
            WorldGrid world = new(5, 4);
            _ = AddPlayer(world, new GridPosition(2, 2));
            Entity monster = AddMonster(world, new GridPosition(1, 2));
            SimulationWorld simulation = CreateSimulation(world);
            _ = simulation.SetGreenMonsterGlasses(active: true);

            IReadOnlyList<SimulationEvent> started = simulation.SetGreenMonsterGlasses(active: false);

            Assert.Equal(
                [new EnemyTransformationStarted(monster.Id, false, TimeSpan.FromMilliseconds(375))],
                started);
            Assert.False(monster.GreenMonsterBody!.IsSmall);
            Assert.False(monster.Interactions.HasFlag(InteractionMask.PlayerEdible));

            IReadOnlyList<SimulationEvent> completed = simulation.Advance(
                TimeSpan.FromMilliseconds(375));

            Assert.Contains(completed, item => item == new EnemyTransformationCompleted(monster.Id, false));
            Assert.Contains(completed, item => item is EnemyAttackStarted attack
                && attack.EntityId == monster.Id);
        }

        /// <summary>Verifies reapplying the active glasses state does not restart its transition.</summary>
        [Fact]
        public void ReapplyingGlassesDoesNotRestartTransformation()
        {
            WorldGrid world = new(5, 4);
            _ = AddPlayer(world, new GridPosition(4, 3));
            Entity monster = AddMonster(world, new GridPosition(1, 1));
            SimulationWorld simulation = CreateSimulation(world);
            _ = simulation.SetGreenMonsterGlasses(active: true);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(100));
            TimeSpan remaining = monster.GreenMonsterBody!.Remaining;

            IReadOnlyList<SimulationEvent> events = simulation.SetGreenMonsterGlasses(active: true);

            Assert.Empty(events);
            Assert.Equal(remaining, monster.GreenMonsterBody.Remaining);
        }

        /// <summary>Verifies ice removes the mutually exclusive glasses effect before freezing.</summary>
        [Fact]
        public void CollectedIceRemovesGlassesBeforeFreezingMonster()
        {
            LevelDocument source = new(
                1,
                null,
                new LevelSize(6, 4),
                new EditorPreview(0, 0, 6, 4),
                [
                    Object(1, "OPlayer", 1, 1),
                    Object(2, "BIce", 2, 1),
                    Object(3, "CellsMetal01", 2, 2),
                    Object(4, "MGrL", 5, 2),
                    Object(5, "CellsMetal01", 3, 1),
                ]);
            MaterializedLevel level = LevelMaterializer.Materialize(source);
            Entity monster = level.GetEntity(4);
            _ = level.Simulation.SetGreenMonsterGlasses(active: true);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(700));

            GreenMonsterBody body = monster.GreenMonsterBody!;
            Assert.False(body.IsSmall);
            Assert.True(body.IsFrozen);
            Assert.False(monster.Interactions.HasFlag(InteractionMask.PlayerEdible));
            List<SimulationEvent> ordered = [.. events];
            int growth = ordered.FindIndex(item => item is EnemyTransformationStarted started
                && !started.IsSmall);
            int frozen = ordered.FindIndex(item => item is EnemyFrozen);
            Assert.True(growth >= 0);
            Assert.True(frozen > growth);
        }

        private static Entity AddPlayer(WorldGrid world, GridPosition position)
        {
            Entity player = new(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right),
                healthBody: new HealthBody(100, 100, TimeSpan.FromSeconds(1)));
            world.Add(player);
            return player;
        }

        private static Entity AddMonster(WorldGrid world, GridPosition position)
        {
            Entity monster = new(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked,
                greenMonsterBody: new GreenMonsterBody(
                    1.5625,
                    Direction.Right,
                    TimeSpan.FromMilliseconds(300)));
            world.Add(monster);
            return monster;
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                DifficultySpeeds.Standard);
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
