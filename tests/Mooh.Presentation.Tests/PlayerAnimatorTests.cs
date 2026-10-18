using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the player animator shows the recovered <c>CGameObjPlayer</c> state for what the
    /// simulation is doing, with its ground or flying frames.
    /// </summary>
    public sealed class PlayerAnimatorTests
    {
        /// <summary>The completion animation replaces a lethal reaction once an irrevocable win settles.</summary>
        [Fact]
        public void TheCompletionAnimationOverridesDeathDuringTheWinSettle()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("MBlueL", 6, 1)));
            Entity monster = Assert.Single(harness.Level.World.Entities, entity => entity.BlueMonsterBody is not null);
            Assert.True(harness.Level.World.Remove(monster.Id));
            harness.Step(0.100);
            harness.Damage(1000);
            harness.Step(1.900);

            Assert.Equal("st_gendoutstart", harness.Animator(harness.Player).Animation.State);
            Assert.Equal("ODoorOut01", harness.Animator(harness.Player).Frame);
            harness.Step(0.875);
            Assert.Equal("st_gendoutlock", harness.Animator(harness.Player).Animation.State);
            Assert.Null(harness.Animator(harness.Player).Frame);
        }

        /// <summary>A door already present in the cell opens again instead of creating a duplicate.</summary>
        [Fact]
        public void CompletionReusesAnExistingDoor()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Begin();
            harness.Step(0.010);
            Entity door = Assert.Single(harness.Level.World.Entities, entity => entity.DoorBody is not null);

            _ = harness.Level.Simulation.BeginLevelExit();
            harness.Step(0.001);

            Assert.Same(door, Assert.Single(harness.Level.World.Entities, entity => entity.DoorBody is not null));
            Assert.Equal("st_dooropen", harness.Animator(door).Animation.State);
            Assert.Equal("st_gendoutstart", harness.Animator(harness.Player).Animation.State);
        }

        /// <summary>The player enters a completion door and stays hidden while that door closes.</summary>
        [Fact]
        public void TheCompletionDoorUsesTheRecoveredEntryAndClosingAnimations()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            _ = harness.Level.Simulation.BeginLevelExit();
            harness.Step(0.001);
            Entity door = Assert.Single(harness.Level.World.Entities, entity => entity.DoorBody is not null);

            Assert.Equal("st_gendoutstart", harness.Animator(harness.Player).Animation.State);
            Assert.Equal("st_doorin", harness.Animator(door).Animation.State);
            Assert.Equal(-1, harness.Animator(door).Pass);
            harness.Step(0.500);
            Assert.Equal("st_gendoutoutwait", harness.Animator(harness.Player).Animation.State);
            harness.Step(0.374);
            Assert.Equal("st_gendoutlock", harness.Animator(harness.Player).Animation.State);
            Assert.Null(harness.Animator(harness.Player).Frame);
            Assert.Equal("st_doorcloseandout", harness.Animator(door).Animation.State);
        }

        /// <summary>Finishing while walking preserves the fractional player position and aligns the new door with it.</summary>
        [Fact]
        public void TheCompletionDoorPreservesThePlayersMovingPosition()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Input(new PlayerInputState(false, false, true, false, false));
            harness.Step(0.140);
            Assert.NotNull(harness.Player.Motion);
            (double x, double y) = DrawnPosition.Of(harness.Player);

            _ = harness.Level.Simulation.BeginLevelExit();
            Entity door = Assert.Single(harness.Level.World.Entities, entity => entity.DoorBody is not null);
            Assert.Equal((x, y), DrawnPosition.Of(harness.Player));
            Assert.Equal((x, y), DrawnPosition.Of(door));
            harness.Step(0.300);
            Assert.Equal((x, y), DrawnPosition.Of(harness.Player));
            Assert.Equal((x, y), DrawnPosition.Of(door));
        }

        /// <summary>Verifies a player standing on the floor shows the single standing frame.</summary>
        [Fact]
        public void AStandingPlayerShowsItsStandingFrame()
        {
            SceneHarness harness = new(Level());

            harness.Step(0.016);

            Assert.Equal("st_stbottom", harness.Animator(harness.Player).Animation.State);
            Assert.Equal("OWlkRotL2T3", harness.Animator(harness.Player).Animation.Frame);
        }

        /// <summary>Verifies a player with nothing below shows the flying frames, <c>_OnCell05_Fly</c>.</summary>
        [Fact]
        public void APlayerWithNothingBelowShowsItsFlyingFrames()
        {
            SceneHarness harness = new(Level(playerY: 0));

            harness.Step(0.016);

            Assert.Equal("st_stbottom", harness.Animator(harness.Player).Animation.State);
            Assert.Equal(10, harness.Animator(harness.Player).Animation.Frames.Count);
        }

        /// <summary>Verifies a held new direction turns the player first, then walks.</summary>
        [Fact]
        public void AHeldDirectionTurnsThenWalks()
        {
            SceneHarness harness = new(Level());
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.Step(0.050);
            Assert.Equal("st_rotb2r", harness.Animator(harness.Player).Animation.State);

            harness.Step(0.100);
            Assert.Equal("st_wlkright", harness.Animator(harness.Player).Animation.State);
        }

        /// <summary>
        /// Verifies a walk steps on the floor's own sound: <c>SndStep2</c> plays the step of the
        /// object below, <c>RInfo.PlStepSnd</c>, here metal's.
        /// </summary>
        [Fact]
        public void AWalkStepsOnTheFloorsSound()
        {
            SceneHarness harness = new(Level());
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            bool stepped = false;

            harness.StepUntil(() => stepped |= harness.Presenter.Sounds.Any(cue => cue.Alias == "StepsMetal"), limitSeconds: 1);

            Assert.True(stepped);
        }

        /// <summary>Verifies walking into a pushable rock shows the pushing walk.</summary>
        [Fact]
        public void PushingARockShowsThePushingWalk()
        {
            SceneHarness harness = new(Level(extra: [("ORock01", 2, 1)]));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.Step(0.150);

            Assert.Equal("st_wlkpushright", harness.Animator(harness.Player).Animation.State);
        }

        /// <summary>Verifies pushing against a wall plays the standing push.</summary>
        [Fact]
        public void PushingAWallPlaysTheStandingPush()
        {
            SceneHarness harness = new(Level(extra: [("CellsMetal01", 2, 1)]));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.Step(0.150);

            Assert.Equal("st_stpushright", harness.Animator(harness.Player).Animation.State);
        }

        /// <summary>Verifies a strike plays its wind-up, then its recovery.</summary>
        [Fact]
        public void AStrikePlaysItsWindupThenItsRecovery()
        {
            SceneHarness harness = new(Level());
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: false, Down: false, Fire: true));

            harness.Step(0.016);
            Assert.Equal("st_strinbottom", harness.Animator(harness.Player).Animation.State);

            harness.Input(default);
            harness.Step(0.100);
            Assert.Equal("st_stroutbottom", harness.Animator(harness.Player).Animation.State);
        }

        /// <summary>Verifies the player waits hidden in the start door, then walks out of it.</summary>
        [Fact]
        public void ThePlayerWaitsHiddenInTheStartDoor()
        {
            SceneHarness harness = new(Level());
            harness.Begin();

            harness.Step(0.016);
            Assert.Equal("st_gstartinstart", harness.Animator(harness.Player).Animation.State);
            Assert.Null(harness.Animator(harness.Player).Animation.Frame);

            harness.Step(1.500);
            Assert.Equal("st_gstartininwalk", harness.Animator(harness.Player).Animation.State);
            Assert.NotNull(harness.Animator(harness.Player).Animation.Frame);
        }

        /// <summary>Verifies a killed player plays the death chain.</summary>
        [Fact]
        public void AKilledPlayerPlaysTheDeathChain()
        {
            SceneHarness harness = new(Level());
            harness.Damage(1000);

            harness.Step(0.016);

            Assert.Equal("st_dead", harness.Animator(harness.Player).Animation.State);
        }

        /// <summary>
        /// Verifies an idle player plays the ambient the simulation started, repeating its middle
        /// state four times, and stands facing down when it ends.
        /// </summary>
        [Fact]
        public void AnIdlePlayerPlaysItsAmbient()
        {
            SceneHarness harness = new(Level());
            PlayerAmbientStarted? ambient = null;

            harness.StepUntil(() => (ambient = harness.LastEvents.OfType<PlayerAmbientStarted>().FirstOrDefault()) is not null, limitSeconds: 20);

            string intro = ambient!.Kind == PlayerAmbientKind.Scratch ? "st_ambscr" : "st_ambwhatsin";
            string repeated = ambient.Kind == PlayerAmbientKind.Scratch ? "st_ambscrdo" : "st_ambwhatsdo";
            Assert.Equal(intro, harness.Animator(harness.Player).Animation.State);
            HashSet<string> seen = [];
            for (double elapsed = 0; elapsed < ambient.Duration.TotalSeconds - 0.05; elapsed += 0.01)
            {
                harness.Step(0.01);
                _ = seen.Add(harness.Animator(harness.Player).Animation.State!);
            }

            Assert.Contains(repeated, seen);
            Assert.DoesNotContain("st_stbottom", seen);
            harness.Step(0.1);
            Assert.Equal("st_stbottom", harness.Animator(harness.Player).Animation.State);
        }

        private static LevelDocument Level(int playerY = 1, (string Type, int X, int Y)[]? extra = null)
        {
            return SceneHarness.BuildLevel(playerY, extra ?? []);
        }
    }
}
