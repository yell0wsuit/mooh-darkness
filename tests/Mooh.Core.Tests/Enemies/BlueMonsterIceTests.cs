using System;
using System.Collections.Generic;

using Mooh.Core.Content;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>Verifies the recovered ice-bonus behavior of blue monsters.</summary>
    public sealed class BlueMonsterIceTests
    {
        /// <summary>Verifies ice stops a walking blue monster and thawing resumes its decisions.</summary>
        [Fact]
        public void CollectedIceFreezesAndLaterThawsWalkingMonster()
        {
            MaterializedLevel level = MaterializeWalkingLevel();
            Entity player = level.GetEntity(1);
            Entity monster = level.GetEntity(4);
            BlueMonsterBody body = monster.BlueMonsterBody
                ?? throw new InvalidOperationException("Expected a blue-monster body.");
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> collectionEvents = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(700));

            Assert.Contains(collectionEvents, item => item is PickupCollected collected
                && collected.PickupEntityId == level.EntityIdsByObjectId[2]);
            Assert.Contains(collectionEvents, item => item == new EnemyFrozen(monster.Id));
            Assert.True(body.IsFrozen);
            Assert.Null(monster.Motion);
            Assert.Equal(BlueMonsterState.Walking, body.State);
            GridPosition frozenAt = monster.Position;

            level.Simulation.SetPlayerInput(default);
            TimeSpan iceRemaining = player.PlayerBody!.Progress.IceRemaining;
            IReadOnlyList<SimulationEvent> frozenEvents = level.Simulation.Advance(
                iceRemaining - TimeSpan.FromMilliseconds(1));
            Assert.DoesNotContain(frozenEvents, item => item is EnemyThawed);
            Assert.DoesNotContain(frozenEvents, item => item is EnemyMoveStarted or EnemyTurnStarted);
            Assert.True(body.IsFrozen);
            Assert.Equal(frozenAt, monster.Position);

            IReadOnlyList<SimulationEvent> thawEvents = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(1));

            Assert.Contains(thawEvents, item => item == new EnemyThawed(monster.Id));
            Assert.False(body.IsFrozen);
            Assert.NotNull(monster.Motion);
            Assert.Equal(BlueMonsterState.Walking, body.State);
        }

        /// <summary>Verifies a blast shatters a frozen blue monster without detonating or spawning.</summary>
        [Fact]
        public void ExplosionShattersFrozenMonsterWithoutSpawningBeetle()
        {
            MaterializedLevel level = MaterializeWalkingLevel();
            Entity monster = level.GetEntity(4);
            FreezeByCollectingIce(level, monster);
            Entity explosive = new(
                EntityId.New(),
                monster.Position.Offset(Direction.Up),
                InteractionMask.None,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            level.World.Add(explosive);

            _ = level.Simulation.Detonate(explosive.Id);
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(200));

            Assert.Contains(events, item => item == new EntityDestroyed(monster.Id, explosive.Id));
            Assert.DoesNotContain(events, item => item is ExplosionStarted started
                && started.EntityId == monster.Id);
            Assert.DoesNotContain(events, item => item is SmallBeetleSpawned);
            _ = Assert.Throws<KeyNotFoundException>(() => level.World.GetEntity(monster.Id));
        }

        /// <summary>Verifies a landing shake shatters a frozen blue monster instead of detonating it.</summary>
        [Fact]
        public void LandingShakeShattersFrozenMonsterWithoutDetonating()
        {
            MaterializedLevel level = MaterializeWalkingLevel();
            Entity monster = level.GetEntity(4);
            FreezeByCollectingIce(level, monster);
            Entity rock = new(
                EntityId.New(),
                monster.Position.Offset(Direction.Up).Offset(Direction.Up),
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked,
                new GravityBody(2, canSlide: false));
            level.World.Add(rock);

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Contains(events, item => item == new EntityDestroyed(monster.Id, rock.Id));
            Assert.DoesNotContain(events, item => item is ExplosionStarted);
            Assert.DoesNotContain(events, item => item is SmallBeetleSpawned);
        }

        /// <summary>Verifies a hand strike reaches the frozen monster's recovered PlCtrl and shatters it.</summary>
        [Fact]
        public void HandStrikeShattersFrozenMonster()
        {
            MaterializedLevel level = MaterializeStrikeLevel("MBlueR");
            Entity player = level.GetEntity(1);
            Entity monster = level.GetEntity(4);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(700));
            Assert.True(monster.BlueMonsterBody!.IsFrozen);
            Assert.Equal(new GridPosition(2, 0), player.Position);

            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, false, false, Fire: true));
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(300));

            Assert.Contains(events, item => item == new EntityDestroyed(monster.Id, player.Id));
            Assert.DoesNotContain(events, item => item is ExplosionStarted);
            Assert.DoesNotContain(events, item => item is SmallBeetleSpawned);
        }

        /// <summary>Verifies an unfrozen blue monster still detonates on a landing shake.</summary>
        [Fact]
        public void UnfrozenMonsterIsUnaffectedByTheIceRules()
        {
            MaterializedLevel level = MaterializeWalkingLevel();
            Entity monster = level.GetEntity(4);
            Assert.False(monster.BlueMonsterBody!.IsFrozen);

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(100));

            Assert.DoesNotContain(events, item => item is EnemyFrozen);
            Assert.Contains(events, item => item is EnemyMoveStarted started
                && started.EntityId == monster.Id);
        }

        internal static void FreezeByCollectingIce(MaterializedLevel level, Entity monster)
        {
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(700));
            level.Simulation.SetPlayerInput(default);
            Assert.True(monster.BlueMonsterBody?.IsFrozen ?? monster.GreenMonsterBody!.IsFrozen);
        }

        private static MaterializedLevel MaterializeWalkingLevel()
        {
            LevelDocument source = new(
                1,
                null,
                new LevelSize(7, 4),
                new EditorPreview(0, 0, 7, 4),
                [
                    Object(1, "OPlayer", 1, 1),
                    Object(2, "BIce", 2, 1),
                    Object(3, "CellsMetal01", 2, 2),
                    Object(4, "MBlueL", 5, 2),
                    Object(5, "CellsMetal01", 3, 1),
                    Object(6, "CellsMetal01", 3, 3),
                    Object(7, "CellsMetal01", 4, 3),
                    Object(8, "CellsMetal01", 5, 3),
                    Object(9, "CellsMetal01", 6, 3),
                ]);
            return LevelMaterializer.Materialize(source);
        }

        /// <summary>
        /// Builds a level in which the player eats the ice and then stands facing a monster that is
        /// still turning in place, so the monster is frozen before it can ever face the player.
        /// </summary>
        internal static MaterializedLevel MaterializeStrikeLevel(string monsterType)
        {
            LevelDocument source = new(
                1,
                null,
                new LevelSize(5, 3),
                new EditorPreview(0, 0, 5, 3),
                [
                    Object(1, "OPlayer", 1, 0),
                    Object(2, "BIce", 2, 0),
                    Object(3, "CellsMetal01", 2, 1),
                    Object(4, monsterType, 3, 0),
                    Object(5, "CellsMetal01", 4, 0),
                    Object(6, "CellsMetal01", 3, 1),
                    Object(7, "CellsMetal01", 1, 1),
                ]);
            return LevelMaterializer.Materialize(source);
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
