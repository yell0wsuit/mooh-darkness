using System;
using System.Linq;

using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the view's jerks, <c>ModScrStiff</c>, <c>ModScrStiffDir</c> and
    /// <c>ModScrStiffDown</c>, and their spring back on <c>CNT_ScrStiffPrms</c>.
    /// </summary>
    public sealed class ScreenShakeTests
    {
        /// <summary>Verifies a 3x3 blast kicks the view 2.5 px in a random direction, <c>ObjExplRange3x3</c>.</summary>
        [Fact]
        public void ABlastKicksTheViewSoftly()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WBomb", 4, 1)));
            harness.Step(0.016);

            harness.Detonate(harness.Level.World.Entities.Single(entity => entity.ArchivalIdentity?.ArchivalType == "WBomb"));
            harness.Step(0.016);

            (double x, double y) = harness.Presenter.Shake;
            Assert.Equal(2.5, Math.Sqrt((x * x) + (y * y)), 6);
        }

        /// <summary>Verifies the view springs back to rest within a second and a half.</summary>
        [Fact]
        public void TheViewSpringsBack()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WBomb", 4, 1)));
            harness.Step(0.016);
            harness.Detonate(harness.Level.World.Entities.Single(entity => entity.ArchivalIdentity?.ArchivalType == "WBomb"));

            for (int frame = 0; frame < 100; frame++)
            {
                harness.Step(0.016);
            }

            Assert.Equal((0, 0), harness.Presenter.Shake);
        }

        /// <summary>Verifies a falling weight lands with a hard 5 px kick along gravity.</summary>
        [Fact]
        public void AWeightLandsWithAHardKick()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OWeight", 4, 0)));

            harness.StepUntil(() => harness.LastEvents.OfType<Landed>().Any());

            Assert.Equal((0, 5), harness.Presenter.Shake);
        }

        /// <summary>Verifies a falling rock lands with a soft 2.5 px kick along gravity.</summary>
        [Fact]
        public void ARockLandsWithASoftKick()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ORock01", 4, 0)));

            harness.StepUntil(() => harness.LastEvents.OfType<Landed>().Any());

            Assert.Equal((0, 2.5), harness.Presenter.Shake);
        }

        /// <summary>
        /// Verifies a strike on a wall kicks the view 5 px along the strike, as <c>_AEnd_Str</c>
        /// does for what neither takes control nor gives way.
        /// </summary>
        [Fact]
        public void AStrikeOnAWallKicksHard()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("CellsMetal01", 2, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            harness.Step(0.2);

            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false, Fire: true));
            harness.StepUntil(() => harness.LastEvents.OfType<PlayerStruck>().Any());

            Assert.Equal((5, 0), harness.Presenter.Shake);
        }

        /// <summary>Verifies a struck rock that stops against a wall kicks the view softly along the push.</summary>
        [Fact]
        public void AStruckRockStopsWithASoftKick()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ORock01", 3, 1), ("CellsMetal01", 6, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            harness.StepUntil(() => harness.Player.Position.X == 2);
            harness.Input(default);
            harness.StepUntil(() => harness.Player.Motion is null);
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: false, Down: false, Fire: true));

            harness.StepUntil(() => harness.LastEvents.OfType<StrikePushStopped>().Any());

            Assert.Equal((2.5, 0), harness.Presenter.Shake);
        }
    }
}
