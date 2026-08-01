using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>Verifies recovered explosion behavior attached to archival level entities.</summary>
    public sealed class LandingExplosionTests
    {
        /// <summary>Verifies every checkpoint landing detonator explodes after a real fall.</summary>
        [Theory]
        [InlineData("OBottle")]
        [InlineData("WBomb")]
        [InlineData("WGrBox")]
        [InlineData("WROne")]
        [InlineData("WRBox")]
        [InlineData("WMinesBox")]
        [InlineData("WBBomb")]
        [InlineData("BCola")]
        [InlineData("BIce")]
        public void RecoveredLandingDetonatorExplodes(string archivalType)
        {
            MaterializedLevel level = LevelMaterializer.Materialize(CreateLevel(
                Object(1, "OPlayer", 4, 4),
                Object(2, archivalType, 2, 1),
                Object(3, "CellsMetal01", 2, 3)));
            Entity source = level.GetEntity(2);

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(535));

            _ = Assert.Throws<KeyNotFoundException>(() => level.World.GetEntity(source.Id));
            Assert.Contains(new Landed(source.Id, Direction.Down), events);
            Assert.Contains(new ExplosionStarted(source.Id, new GridPosition(2, 2)), events);
            Assert.True(
                IndexOf<Landed>(events) < IndexOf<ExplosionStarted>(events),
                "Landing must be observed before its explosion starts.");
        }

        /// <summary>Verifies a merely supported explosive does not run its landing trigger.</summary>
        [Fact]
        public void InitiallySupportedBottleDoesNotExplode()
        {
            MaterializedLevel level = LevelMaterializer.Materialize(CreateLevel(
                Object(1, "OPlayer", 4, 4),
                Object(2, "OBottle", 2, 2),
                Object(3, "CellsMetal01", 2, 3)));
            Entity bottle = level.GetEntity(2);

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Same(bottle, level.World.GetEntity(bottle.Id));
            Assert.DoesNotContain(events, value => value is ExplosionStarted);
        }

        /// <summary>Verifies recovered ExplWork repeats downward shake after starting the blast.</summary>
        [Fact]
        public void LandingExplosionRepeatsDownwardShakeAfterCenterBlast()
        {
            MaterializedLevel level = LevelMaterializer.Materialize(CreateLevel(
                Object(1, "OPlayer", 2, 3),
                Object(2, "OBottle", 2, 1)));
            EntityId player = level.GetEntity(1).Id;
            EntityId bottle = level.GetEntity(2).Id;

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(535));

            Assert.Equal(
                [
                    new FallStarted(bottle, Direction.Down),
                    new CellEntered(bottle, new GridPosition(2, 2)),
                    new Landed(bottle, Direction.Down),
                    new DamageApplied(player, bottle, DamageKind.Shake, 20, 80),
                    new PlayerShockStarted(player, TimeSpan.FromMilliseconds(700)),
                    new ExplosionStarted(bottle, new GridPosition(2, 2)),
                    new EntityDestroyed(bottle, bottle),
                    new ExplosionCellReached(bottle, new GridPosition(2, 2)),
                    new DamageIgnored(player, bottle, DamageKind.Shake, DamageResult.IgnoredImmune),
                ],
                events);
        }

        /// <summary>Verifies representative original blast reactions are materialized explicitly.</summary>
        [Fact]
        public void MaterializesRecoveredExplosionReactions()
        {
            MaterializedLevel level = LevelMaterializer.Materialize(CreateLevel(
                Object(1, "OPlayer", 0, 0),
                Object(2, "ORock01", 1, 1),
                Object(3, "OCoin01", 2, 1),
                Object(4, "CellsSolid01", 3, 1),
                Object(5, "PipeHor", 4, 1),
                Object(6, "MGrL", 5, 1),
                Object(7, "MMBio", 6, 1),
                Object(8, "OBottle", 7, 1),
                Object(9, "WMine", 1, 2),
                Object(10, "CellsMetal01", 2, 2),
                Object(11, "OKDKey1", 3, 2),
                Object(12, "OBarel", 4, 2),
                Object(13, "OArrowL", 5, 2)));

            foreach (int id in new[] { 2, 3, 4, 5, 6, 7 })
            {
                Assert.Equal(ExplosionReaction.Destroy, level.GetEntity(id).ExplosionBody!.Reaction);
            }

            Assert.Equal(ExplosionReaction.Detonate3x3, level.GetEntity(8).ExplosionBody!.Reaction);
            Assert.Equal(ExplosionReaction.Detonate3x3, level.GetEntity(9).ExplosionBody!.Reaction);
            foreach (int id in new[] { 10, 11, 12, 13 })
            {
                Assert.Null(level.GetEntity(id).ExplosionBody);
            }
        }

        private static int IndexOf<TEvent>(IReadOnlyList<SimulationEvent> events)
            where TEvent : SimulationEvent
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index] is TEvent)
                {
                    return index;
                }
            }

            return -1;
        }

        private static LevelDocument CreateLevel(params LevelObject[] objects)
        {
            return new LevelDocument(
                1,
                null,
                new LevelSize(8, 6),
                new EditorPreview(0, 0, 8, 6),
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
