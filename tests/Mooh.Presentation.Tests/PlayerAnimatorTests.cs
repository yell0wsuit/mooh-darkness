using Mooh.Core.Content;
using Mooh.Core.Player;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the player animator shows the recovered <c>CGameObjPlayer</c> state for what the
    /// simulation is doing, with its ground or flying frames.
    /// </summary>
    public sealed class PlayerAnimatorTests
    {
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

        private static LevelDocument Level(int playerY = 1, (string Type, int X, int Y)[]? extra = null)
        {
            return SceneHarness.BuildLevel(playerY, extra ?? []);
        }
    }
}
