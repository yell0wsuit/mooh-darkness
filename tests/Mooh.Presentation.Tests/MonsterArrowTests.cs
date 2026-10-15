using Mooh.Presentation.Hud;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the arrows of <c>ModRender</c> that point at the last level-end lock carriers off
    /// the screen, pinned 5 px inside its edge by <c>RectPntDir8Get</c>.
    /// </summary>
    public sealed class MonsterArrowTests
    {
        /// <summary>Verifies a monster off the right edge gets an arrow pinned at x 795, level with it.</summary>
        [Fact]
        public void AMonsterOffTheRightEdgeGetsAnArrow()
        {
            HudRun run = Run(("MGrL", 7, 1));

            run.Step(0.016);

            Assert.Contains(new HudSprite("PAGr_Right", 795, 300), run.Hud.Sprites);
        }

        /// <summary>Verifies a blue monster above and to the left gets its own corner arrow.</summary>
        [Fact]
        public void ABlueMonsterOffACornerGetsItsCornerArrow()
        {
            HudRun run = Run(("MBlueL", 6, 0));
            run.Camera!.Snap(12, 6);

            run.Step(0.016);

            Assert.Contains(new HudSprite("PABlue_LeftTop", 5, 5), run.Hud.Sprites);
        }

        /// <summary>Verifies a monster on the screen gets no arrow.</summary>
        [Fact]
        public void AMonsterOnTheScreenGetsNone()
        {
            HudRun run = Run(("MGrL", 4, 1));

            run.Step(0.016);

            Assert.DoesNotContain(run.Hud.Sprites, sprite => sprite.Frame.StartsWith("PA", System.StringComparison.Ordinal));
        }

        /// <summary>Verifies six or more carriers left show no arrows, <c>CNT_MArr_ShowNum</c>.</summary>
        [Fact]
        public void SixCarriersShowNoArrows()
        {
            HudRun run = Run(("MGrL", 2, 0), ("MGrL", 3, 0), ("MGrL", 4, 0), ("MGrL", 5, 0), ("MGrL", 6, 0), ("MGrL", 7, 1));

            run.Step(0.016);

            Assert.DoesNotContain(run.Hud.Sprites, sprite => sprite.Frame.StartsWith("PA", System.StringComparison.Ordinal));
        }

        private static HudRun Run(params (string Type, int X, int Y)[] monsters)
        {
            HudRun run = new(SceneHarness.BuildLevel(1, monsters))
            {
                Camera = new SceneCamera()
            };
            run.Camera.Snap(1, 1);
            return run;
        }
    }
}
