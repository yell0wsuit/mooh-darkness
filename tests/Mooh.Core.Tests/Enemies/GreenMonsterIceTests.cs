using System;
using System.Collections.Generic;

using Mooh.Core.Content;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>Verifies the recovered ice-bonus behavior of green monsters.</summary>
    public sealed class GreenMonsterIceTests
    {
        /// <summary>Verifies ice stops a moving monster and thawing resumes autonomous decisions.</summary>
        [Fact]
        public void CollectedIceFreezesAndLaterThawsMovingMonster()
        {
            MaterializedLevel level = MaterializeIceLevel();
            Entity player = level.GetEntity(1);
            Entity monster = level.GetEntity(4);
            GreenMonsterBody body = monster.GreenMonsterBody
                ?? throw new InvalidOperationException("Expected a green-monster body.");
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> collectionEvents = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(700));

            Assert.Contains(collectionEvents, item => item is PickupCollected collected
                && collected.PickupEntityId == level.EntityIdsByObjectId[2]);
            Assert.Contains(collectionEvents, item => item == new EnemyFrozen(monster.Id));
            Assert.True(body.IsFrozen);
            Assert.Null(monster.Motion);
            Assert.Equal(new GridPosition(5, 2), monster.Position);
            Assert.Equal(GreenMonsterState.Walking, body.State);
            TimeSpan iceRemaining = player.PlayerBody!.Progress.IceRemaining;
            Assert.InRange(
                iceRemaining,
                TimeSpan.FromMilliseconds(44_404),
                TimeSpan.FromMilliseconds(44_406));

            level.Simulation.SetPlayerInput(default);
            Assert.DoesNotContain(
                level.Simulation.Advance(iceRemaining - TimeSpan.FromMilliseconds(1)),
                item => item is EnemyThawed);
            Assert.True(body.IsFrozen);

            IReadOnlyList<SimulationEvent> thawEvents = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(1));

            Assert.Contains(thawEvents, item => item == new EnemyThawed(monster.Id));
            Assert.False(body.IsFrozen);
            Assert.NotNull(monster.Motion);
            Assert.Equal(GreenMonsterState.Walking, body.State);
            Assert.Equal(TimeSpan.Zero, player.PlayerBody.Progress.IceRemaining);
        }

        /// <summary>Verifies frozen green monsters are destroyed rather than detonated by a blast.</summary>
        [Fact]
        public void ExplosionShattersFrozenMonsterWithoutStartingAnotherExplosion()
        {
            MaterializedLevel level = MaterializeIceLevel();
            Entity monster = level.GetEntity(4);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(700));
            Entity explosive = new(
                EntityId.New(),
                monster.Position.Offset(Direction.Left),
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

        /// <summary>Verifies a hand strike reaches the frozen monster's recovered PlCtrl and shatters it.</summary>
        [Fact]
        public void HandStrikeShattersFrozenMonster()
        {
            MaterializedLevel level = BlueMonsterIceTests.MaterializeStrikeLevel("MGrR");
            Entity player = level.GetEntity(1);
            Entity monster = level.GetEntity(4);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(700));
            Assert.True(monster.GreenMonsterBody!.IsFrozen);
            Assert.Equal(new GridPosition(2, 0), player.Position);

            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, false, false, Fire: true));
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(300));

            Assert.Contains(events, item => item == new EntityDestroyed(monster.Id, player.Id));
            Assert.DoesNotContain(events, item => item is ExplosionStarted);
            Assert.DoesNotContain(events, item => item is SmallBeetleSpawned);
            _ = Assert.Single(events, item => item is FrozenMonsterShattered);
        }

        private static MaterializedLevel MaterializeIceLevel()
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
