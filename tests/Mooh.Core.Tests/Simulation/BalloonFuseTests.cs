using System;
using System.Collections.Generic;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies the recovered balloon fuse: <c>PlPush_Ballon</c> enters <c>ST_Work</c> once and
    /// deletes itself, and the chained tick states end in <c>ObjExplRange3x3</c> 7375 ms later.
    /// </summary>
    public sealed class BalloonFuseTests
    {
        /// <summary>The recovered player quarter turn.</summary>
        private const double TurnMilliseconds = 105;

        /// <summary>The recovered fuse: 225 + 3 x (250 + 2000) + 400 ms.</summary>
        private const double FuseMilliseconds = 7375;

        /// <summary>Verifies a walking push lights the fuse and the balloon detonates when it ends.</summary>
        [Fact]
        public void PushedBalloonDetonatesWhenTheFuseEnds()
        {
            MaterializedLevel level = Materialize([Object(100, "OPlayer", 0, 1), Object(101, "OBaloon", 1, 1)]);
            Entity balloon = level.GetEntity(101);

            IReadOnlyList<SimulationEvent> pushEvents = PushRightOnce(level);

            Assert.Contains(pushEvents, item => item == new BalloonFuseLit(
                balloon.Id,
                TimeSpan.FromMilliseconds(FuseMilliseconds)));
            Assert.Contains(pushEvents, item => item == new PushStarted(balloon.Id, Direction.Right));
            Assert.True(balloon.BalloonFuseBody!.HasBeenLit);

            AssertDetonatesAt(level, balloon, TurnMilliseconds + FuseMilliseconds, alreadyElapsed: 110);
        }

        /// <summary>Verifies a second push neither restarts nor extends a lit fuse.</summary>
        [Fact]
        public void SecondPushDoesNotRestartTheFuse()
        {
            MaterializedLevel level = Materialize([Object(100, "OPlayer", 0, 1), Object(101, "OBaloon", 1, 1)]);
            Entity balloon = level.GetEntity(101);
            _ = PushRightOnce(level);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(500));

            level.Simulation.SetPlayerInput(Right());
            IReadOnlyList<SimulationEvent> secondPush = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(10));
            level.Simulation.SetPlayerInput(default);

            Assert.Contains(secondPush, item => item == new PushStarted(balloon.Id, Direction.Right));
            Assert.DoesNotContain(secondPush, item => item is BalloonFuseLit);
            AssertDetonatesAt(level, balloon, TurnMilliseconds + FuseMilliseconds, alreadyElapsed: 620);
        }

        /// <summary>
        /// Verifies a walking push that cannot move the balloon still lights it, because
        /// <c>CtrlMove</c> dispatches <c>PlPush</c> before it tests the cell beyond.
        /// </summary>
        [Fact]
        public void BlockedWalkingPushStillLightsTheFuse()
        {
            MaterializedLevel level = Materialize(
            [
                Object(100, "OPlayer", 0, 1),
                Object(101, "OBaloon", 1, 1),
                Object(102, "CellsMetal01", 2, 1),
            ]);
            Entity balloon = level.GetEntity(101);

            IReadOnlyList<SimulationEvent> events = PushRightOnce(level);

            Assert.Contains(events, item => item is BalloonFuseLit lit && lit.EntityId == balloon.Id);
            Assert.DoesNotContain(events, item => item is PushStarted);
            Assert.Equal(new GridPosition(1, 1), balloon.Position);
        }

        /// <summary>Verifies a blast detonates a balloon whose fuse is already burning.</summary>
        [Fact]
        public void BlastDetonatesALitBalloonImmediately()
        {
            MaterializedLevel level = Materialize([Object(100, "OPlayer", 0, 1), Object(101, "OBaloon", 1, 1)]);
            Entity balloon = level.GetEntity(101);
            _ = PushRightOnce(level);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(1000));
            Entity explosive = new(
                EntityId.New(),
                balloon.Position.Offset(Direction.Right),
                InteractionMask.None,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            level.World.Add(explosive);

            _ = level.Simulation.Detonate(explosive.Id);
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(200));

            Assert.Contains(events, item => item is ExplosionStarted started && started.EntityId == balloon.Id);
        }

        /// <summary>Verifies a struck balloon that moves lights its fuse on the strike.</summary>
        [Fact]
        public void StruckBalloonThatMovesLightsTheFuse()
        {
            MaterializedLevel level = Materialize([Object(100, "OPlayer", 0, 1), Object(101, "OBaloon", 2, 1)]);
            Entity balloon = level.GetEntity(101);
            WalkRightOneCell(level);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(200));

            Assert.Contains(events, item => item is BalloonFuseLit lit && lit.EntityId == balloon.Id);
            Assert.Contains(events, item => item == new PushStarted(balloon.Id, Direction.Right, IsStrike: true));
            Assert.DoesNotContain(events, item => item is ExplosionStarted);
        }

        /// <summary>
        /// Verifies a struck balloon against a wall explodes at once and never lights a fuse,
        /// because <c>OnPlPush</c> only reaches <c>PlPush</c> when <c>PlPush_OnCell</c> moved it.
        /// </summary>
        [Fact]
        public void StruckBalloonAgainstAWallExplodesWithoutLightingAFuse()
        {
            MaterializedLevel level = Materialize(
            [
                Object(100, "OPlayer", 0, 1),
                Object(101, "OBaloon", 2, 1),
                Object(102, "CellsMetal01", 3, 1),
            ]);
            Entity balloon = level.GetEntity(101);
            WalkRightOneCell(level);

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(200));

            Assert.Contains(events, item => item is ExplosionStarted started && started.EntityId == balloon.Id);
            Assert.DoesNotContain(events, item => item is BalloonFuseLit);
        }

        private static void AssertDetonatesAt(
            MaterializedLevel level,
            Entity balloon,
            double detonationMilliseconds,
            double alreadyElapsed)
        {
            IReadOnlyList<SimulationEvent> before = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(detonationMilliseconds - alreadyElapsed - 1));
            Assert.DoesNotContain(before, item => item is ExplosionStarted);
            Assert.Contains(level.World.Entities, entity => entity.Id == balloon.Id);

            IReadOnlyList<SimulationEvent> at = level.Simulation.Advance(TimeSpan.FromMilliseconds(2));
            Assert.Contains(at, item => item is ExplosionStarted started && started.EntityId == balloon.Id);
        }

        /// <summary>Turns right and starts one move, releasing before the next cell is reached.</summary>
        private static IReadOnlyList<SimulationEvent> PushRightOnce(MaterializedLevel level)
        {
            level.Simulation.SetPlayerInput(Right());
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(110));
            level.Simulation.SetPlayerInput(default);
            return events;
        }

        private static void WalkRightOneCell(MaterializedLevel level)
        {
            level.Simulation.SetPlayerInput(Right());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(TurnMilliseconds + 380));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(60));
            Assert.Equal(new GridPosition(1, 1), level.World.Player!.Position);
            Assert.Equal(Direction.Right, level.World.Player.PlayerBody!.FacingDirection);
        }

        private static PlayerInputState Right()
        {
            return new PlayerInputState(Left: false, Up: false, Right: true, Down: false);
        }

        private static PlayerInputState Fire()
        {
            return new PlayerInputState(Left: false, Up: false, Right: false, Down: false, Fire: true);
        }

        private static MaterializedLevel Materialize(List<LevelObject> objects)
        {
            for (int x = 0; x <= 9; x++)
            {
                objects.Add(Object(x, "CellsMetal01", x, 2));
            }

            return LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(10, 4), new EditorPreview(0, 0, 10, 4), objects));
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
