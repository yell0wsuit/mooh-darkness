using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Motion;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies the recovered <c>CPlPushBase.PlPush_OnCell</c> chain a hand strike starts. Unlike
    /// the single-cell push a walking player applies, a struck object keeps entering the next cell
    /// until it is blocked or becomes unsupported, and shakes what it stops against.
    /// </summary>
    public sealed class StrikePushTests
    {
        /// <summary>Verifies a struck rock slides on until a blocking cell stops it.</summary>
        [Fact]
        public void AStruckRockSlidesUntilItIsBlocked()
        {
            MaterializedLevel level = Materialize(
                Floor(0, 8, 2),
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "ORock01", 3, 1),
                    Object(102, "CellsSolid01", 7, 1),
                ]);
            Entity rock = level.GetEntity(101);
            WalkRightTo(level, 2);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(1500));

            Assert.Contains(events, value => value is PushStarted);
            Assert.Equal(new GridPosition(6, 1), rock.Position);
            Assert.Null(rock.Motion);
        }

        /// <summary>
        /// Verifies a struck rock reports where the chain stops it, the moment the recovered
        /// <c>PlPush_OnCell</c> shakes the view along the push and plays the landing sound.
        /// </summary>
        [Fact]
        public void AStruckRockReportsItsStop()
        {
            MaterializedLevel level = Materialize(
                Floor(0, 8, 2),
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "ORock01", 3, 1),
                    Object(102, "CellsSolid01", 7, 1),
                ]);
            Entity rock = level.GetEntity(101);
            WalkRightTo(level, 2);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(1500));

            Assert.Equal(new StrikePushStopped(rock.Id, Direction.Right), Assert.Single(events.OfType<StrikePushStopped>()));
        }

        /// <summary>
        /// Verifies the recovered end of the chain shakes the cell the object stops against, which
        /// is how a struck rock detonates a bomb it reaches.
        /// </summary>
        [Fact]
        public void AStruckRockDetonatesTheBombItStopsAgainst()
        {
            MaterializedLevel level = Materialize(
                Floor(0, 8, 2),
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "ORock01", 3, 1),
                    Object(102, "WBomb", 5, 1),
                ]);
            EntityId bombId = level.GetEntity(102).Id;
            WalkRightTo(level, 2);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(1000));

            Assert.Contains(
                events,
                value => value is ExplosionStarted explosion && explosion.EntityId == bombId);
            Assert.DoesNotContain(level.World.Entities, entity => entity.Id == bombId);
        }

        /// <summary>
        /// Verifies a struck weight consumes crushable cells in its path, which the recovered chain
        /// does before it tests whether the cell ahead blocks it.
        /// </summary>
        [Fact]
        public void AStruckWeightConsumesDiggableTerrainInItsPath()
        {
            MaterializedLevel level = Materialize(
                Floor(0, 8, 2),
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "OWeight", 3, 1),
                    Object(102, "CellsEat01", 4, 1),
                    Object(103, "CellsEat01", 5, 1),
                    Object(104, "CellsSolid01", 7, 1),
                ]);
            Entity weight = level.GetEntity(101);
            EntityId firstTerrainId = level.GetEntity(102).Id;
            EntityId secondTerrainId = level.GetEntity(103).Id;
            WalkRightTo(level, 2);

            level.Simulation.SetPlayerInput(Fire());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(1500));

            Assert.DoesNotContain(level.World.Entities, entity => entity.Id == firstTerrainId);
            Assert.DoesNotContain(level.World.Entities, entity => entity.Id == secondTerrainId);
            Assert.Equal(new GridPosition(6, 1), weight.Position);
        }

        /// <summary>
        /// Verifies the recovered <c>ObjFallSlide</c> test that opens each chain step: an object
        /// that becomes unsupported falls instead of continuing to slide.
        /// </summary>
        [Fact]
        public void AStruckRockFallsWhenItLeavesItsSupport()
        {
            MaterializedLevel level = Materialize(
                Floor(0, 3, 2),
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "ORock01", 3, 1),
                ]);
            Entity rock = level.GetEntity(101);
            WalkRightTo(level, 2);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(2000));

            Assert.Contains(events, value => value is FallStarted);
            Assert.Equal(new GridPosition(4, 5), rock.Position);
        }

        /// <summary>
        /// Verifies a blocked strike still shakes the cell it stops against without moving the
        /// object, which the recovered chain reports by leaving the push unhandled.
        /// </summary>
        [Fact]
        public void ABlockedStrikeShakesTheCellTheObjectStopsAgainst()
        {
            MaterializedLevel level = Materialize(
                Floor(0, 8, 2),
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "ORock01", 3, 1),
                    Object(102, "WBomb", 4, 1),
                ]);
            Entity rock = level.GetEntity(101);
            EntityId bombId = level.GetEntity(102).Id;
            WalkRightTo(level, 2);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(200));

            Assert.Equal(new GridPosition(3, 1), rock.Position);
            Assert.Contains(
                events,
                value => value is ExplosionStarted explosion && explosion.EntityId == bombId);
        }

        /// <summary>
        /// Verifies a struck object stops against a monster that is walking rather than passing
        /// through it. The recovered gate is <c>CellObjCheck(c, 0, 0, True)</c>, which asks whether
        /// the cell ahead holds anything at all; an occupant part-way through its own move still
        /// owns the cell, because occupancy is committed when movement starts.
        /// </summary>
        [Fact]
        public void AStruckRockStopsAgainstAWalkingMonsterAndDetonatesIt()
        {
            MaterializedLevel level = Corridor(
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "ORock01", 3, 1),
                    Object(102, "MGrL", 9, 1),
                ]);
            Entity rock = level.GetEntity(101);
            Entity monster = level.GetEntity(102);
            EntityId monsterId = monster.Id;
            WalkRightTo(level, 2);

            level.Simulation.SetPlayerInput(Fire());
            List<SimulationEvent> events = [];
            for (int step = 0; step < 30; step++)
            {
                events.AddRange(level.Simulation.Advance(TimeSpan.FromMilliseconds(50)));
                if (!level.World.Entities.Any(entity => entity.Id == monsterId))
                {
                    break;
                }

                Assert.NotEqual(monster.Position, rock.Position);
            }

            Assert.Contains(
                events,
                value => value is ExplosionStarted explosion && explosion.EntityId == monsterId);
            Assert.DoesNotContain(level.World.Entities, entity => entity.Id == monsterId);
        }

        /// <summary>
        /// Verifies a struck bubble pops where it stops. Its recovered <c>OnShake</c> is
        /// <c>ObjKill</c>, so the shake the chain applies to the object it stopped against is what
        /// destroys it.
        /// </summary>
        [Fact]
        public void AStruckBubblePopsWhereItStops()
        {
            MaterializedLevel level = Corridor(
                [
                    Object(100, "OPlayer", 0, 1),
                    Object(101, "OBubble", 3, 1),
                    Object(102, "CellsMetal01", 6, 1),
                ]);
            EntityId bubbleId = level.GetEntity(101).Id;
            WalkRightTo(level, 2);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(1500));

            Assert.Contains(events, value => value is PushStarted);
            Assert.DoesNotContain(level.World.Entities, entity => entity.Id == bubbleId);
        }

        /// <summary>A strike takes over a moving target without advancing its cell or losing its offset.</summary>
        [Fact]
        public void StrikingAnObjectMovingInTheSameDirectionPreservesItsSegment()
        {
            MaterializedLevel level = Materialize(Floor(0, 8, 2),
                [Object(100, "OPlayer", 0, 1), Object(101, "ORock01", 3, 1)]);
            Entity rock = level.GetEntity(101);
            WalkRightTo(level, 2);
            level.Simulation.SetPlayerInput(Fire());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(10));
            KinematicsSystem.Start(rock, MotionCause.PlayerPushed, Direction.Right, 1);
            KinematicsSystem.Advance(rock, TimeSpan.FromMilliseconds(200));

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(150));

            Assert.Contains(events, item => item == new PushStarted(rock.Id, Direction.Right));
            Assert.Equal(new GridPosition(3, 1), rock.Position);
            Assert.Equal(MotionCause.StrikePushed, rock.Motion!.Cause);
            Assert.Equal(0.3775, rock.Motion.Progress, 10);
            Assert.Equal(3.75, rock.Motion.Speed);
        }

        /// <summary>
        /// Materializes a one-cell-high corridor with a metal roof and floor, so a walking monster
        /// stays in the struck object's lane instead of turning out of it.
        /// </summary>
        private static MaterializedLevel Corridor(IEnumerable<LevelObject> objects)
        {
            List<LevelObject> cells = Floor(0, 9, 2);
            for (int x = 0; x <= 9; x++)
            {
                cells.Add(Object(200 + x, "CellsMetal01", x, 0));
            }

            return Materialize(cells, objects);
        }

        /// <summary>
        /// Walks the player right by whole cells and releases the control before the arrival, so
        /// the player coasts to the boundary, stops, and ends up facing right without pushing
        /// anything on the way.
        /// </summary>
        private static void WalkRightTo(MaterializedLevel level, int x)
        {
            // The recovered quarter turn takes 105 ms and the standard walking speed crosses one
            // cell in 400 ms.
            double milliseconds = 105 + (400 * x);
            level.Simulation.SetPlayerInput(new PlayerInputState(
                Left: false,
                Up: false,
                Right: true,
                Down: false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(milliseconds - 20));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(60));

            Assert.Equal(new GridPosition(x, 1), level.World.Player!.Position);
            Assert.Equal(Direction.Right, level.World.Player.PlayerBody!.FacingDirection);
        }

        private static PlayerInputState Fire()
        {
            return new PlayerInputState(
                Left: false,
                Up: false,
                Right: false,
                Down: false,
                Fire: true);
        }

        private static List<LevelObject> Floor(int fromX, int toX, int y)
        {
            List<LevelObject> result = [];
            for (int x = fromX; x <= toX; x++)
            {
                result.Add(Object(x, "CellsMetal01", x, y));
            }

            return result;
        }

        private static MaterializedLevel Materialize(
            List<LevelObject> floor,
            IEnumerable<LevelObject> objects)
        {
            floor.AddRange(objects);
            return LevelMaterializer.Materialize(
                new LevelDocument(
                    1,
                    null,
                    new LevelSize(10, 6),
                    new EditorPreview(0, 0, 10, 6),
                    floor));
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            Dictionary<string, System.Text.Json.JsonElement> properties = [];
            return new LevelObject(
                id,
                type,
                0,
                new LevelPosition(x, y),
                [],
                properties);
        }
    }
}
