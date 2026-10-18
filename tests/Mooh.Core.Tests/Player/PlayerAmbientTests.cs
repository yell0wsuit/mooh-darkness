using System;
using System.Collections.Generic;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies the recovered idle ambient: <c>GamePCtrlCycle</c> calls <c>AmbDo</c> once the
    /// player has stood in a <c>bDoAmb</c> state for longer than <c>CNT_GPrm_PAmbTime</c>
    /// (15 s). Every ambient state faces down, so the player ends it facing down.
    /// </summary>
    public sealed class PlayerAmbientTests
    {
        /// <summary>Verifies an idle player starts an ambient after 15 s and faces down.</summary>
        [Fact]
        public void AnIdlePlayerStartsAnAmbientAndFacesDown()
        {
            MaterializedLevel level = Materialize();
            Entity player = level.GetEntity(1);
            TurnLeft(level);

            IReadOnlyList<SimulationEvent> waiting = level.Simulation.Advance(TimeSpan.FromSeconds(14.9));
            Assert.DoesNotContain(waiting, item => item is PlayerAmbientStarted);
            Assert.Equal(Direction.Left, player.PlayerBody!.FacingDirection);

            IReadOnlyList<SimulationEvent> started = level.Simulation.Advance(TimeSpan.FromSeconds(0.2));
            Assert.Contains(started, item => item is PlayerAmbientStarted);
            Assert.Equal(Direction.Down, player.PlayerBody.FacingDirection);
        }

        /// <summary>
        /// Verifies the next ambient waits 15 s from the end of the first: the ambient states are
        /// not <c>bDoAmb</c>, so the timer restarts on every cycle until the ambient ends.
        /// </summary>
        [Fact]
        public void TheNextAmbientWaitsForTheFirstToEnd()
        {
            MaterializedLevel level = Materialize();
            List<(double Seconds, PlayerAmbientStarted Ambient)> ambients = [];
            double now = 0;
            while (ambients.Count < 2)
            {
                foreach (SimulationEvent item in level.Simulation.Advance(TimeSpan.FromMilliseconds(10)))
                {
                    if (item is PlayerAmbientStarted ambient)
                    {
                        ambients.Add((now + 0.01, ambient));
                    }
                }

                now += 0.01;
                Assert.True(now < 60, "The ambients never came.");
            }

            double firstLength = ambients[0].Ambient.Duration.TotalSeconds;
            Assert.InRange(ambients[1].Seconds, 30 + firstLength - 0.02, 30 + firstLength + 0.02);
            Assert.Contains(firstLength, (double[])[3.85, 2.45]);
        }

        /// <summary>Verifies pushing against a wall keeps the ambient away.</summary>
        [Fact]
        public void PushingAgainstAWallKeepsTheAmbientAway()
        {
            MaterializedLevel level = Materialize(wallOnTheRight: true);
            level.Simulation.SetPlayerInput(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            List<SimulationEvent> events = [];
            for (int frame = 0; frame < 20 * 60; frame++)
            {
                events.AddRange(level.Simulation.Advance(TimeSpan.FromSeconds(1.0 / 60)));
            }

            Assert.Contains(events, item => item is PlayerBlocked);
            Assert.DoesNotContain(events, item => item is PlayerAmbientStarted);
        }

        private static void TurnLeft(MaterializedLevel level)
        {
            level.Simulation.SetPlayerInput(new PlayerInputState(Left: true, Up: false, Right: false, Down: false));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(20));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(100));
            Assert.Equal(Direction.Left, level.GetEntity(1).PlayerBody!.FacingDirection);
            Assert.Null(level.GetEntity(1).Motion);
        }

        private static MaterializedLevel Materialize(bool wallOnTheRight = false)
        {
            List<LevelObject> objects = [Object(1, "OPlayer", 1, 1)];
            for (int x = 0; x <= 5; x++)
            {
                objects.Add(Object(100 + x, "CellsMetal01", x, 2));
            }

            if (wallOnTheRight)
            {
                objects.Add(Object(200, "CellsMetal01", 2, 1));
            }

            return LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(6, 4), new EditorPreview(0, 0, 6, 4), objects),
                Direction.Down,
                Undrained.Standard);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], new Dictionary<string, System.Text.Json.JsonElement>());
        }
    }
}
