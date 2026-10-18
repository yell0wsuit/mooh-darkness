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

        /// <summary>A monster visible in the additional world area needs no indicator.</summary>
        [Fact]
        public void WidescreenRevealsMonsterAndMovesPanelsToEdges()
        {
            HudRun run = Run(("MGrL", 7, 1));
            run.Camera!.HorizontalMargin = 400.0 / 3;
            run.Step(0.016);
            Assert.DoesNotContain(run.Hud.Sprites, sprite => sprite.Frame.StartsWith("PA", System.StringComparison.Ordinal));
            Assert.Contains(run.Hud.Sprites, sprite => sprite.Frame == "PNrmHealth" && sprite.X < 0);
        }

        /// <summary>An offscreen monster points to the extended edge.</summary>
        [Fact]
        public void WidescreenIndicatorUsesExtendedEdge()
        {
            HudRun run = Run(("MGrL", 7, 1));
            run.Camera!.HorizontalMargin = 400.0 / 3;
            run.Camera.Snap(0, 1);
            run.Step(0.016);
            Assert.Contains(new HudSprite("PAGr_Right", 928, 300), run.Hud.Sprites);
        }

        /// <summary>Ultrawide panels retain their 16:9 positions while additional monsters stay visible.</summary>
        [Fact]
        public void UltrawideKeepsPanelsInsideCenteredSafeArea()
        {
            HudRun wide = Run(("MGrL", 7, 1));
            wide.Camera!.HorizontalMargin = 400.0 / 3;
            wide.Step(0.016);
            HudRun ultra = Run(("MGrL", 7, 1));
            ultra.Camera!.HorizontalMargin = 975;
            ultra.Step(0.016);
            Assert.Equal(wide.Hud.Sprites, ultra.Hud.Sprites);
            Assert.Equal(wide.Hud.Texts, ultra.Hud.Texts);
        }

        /// <summary>World indicators point to the actual viewport edge beyond the panel safe area.</summary>
        [Fact]
        public void UltrawideIndicatorUsesFullViewportEdge()
        {
            HudRun run = Run(("MGrL", 7, 1));
            run.Camera!.HorizontalMargin = 975;
            run.Camera.Snap(-15, 1);
            run.Step(0.016);
            Assert.Contains(new HudSprite("PAGr_Right", 1770, 300), run.Hud.Sprites);
        }

        /// <summary>A monster visible above the old screen needs no portrait indicator.</summary>
        [Fact]
        public void TallScreenRevealsMonsterAboveOriginalScreen()
        {
            HudRun run = Run(("MGrL", 4, 1));
            run.Camera!.VerticalMargin = 410;
            run.Camera.Snap(4, 7);
            run.Step(0.016);
            Assert.DoesNotContain(run.Hud.Sprites, sprite => sprite.Frame.StartsWith("PA", System.StringComparison.Ordinal));
        }

        /// <summary>Vertical indicators follow the expanded edges while HUD panels stay in the centered area.</summary>
        [Theory]
        [InlineData(12, "PAGr_Top", -405)]
        [InlineData(-12, "PAGr_Bottom", 1005)]
        public void TallScreenIndicatorsUseFullViewportEdges(int cameraRow, string frame, int y)
        {
            HudRun run = Run(("MGrL", 4, 1));
            run.Camera!.VerticalMargin = 410;
            run.Camera.Snap(4, cameraRow);
            run.Step(0.016);
            Assert.Contains(run.Hud.Sprites, sprite => sprite.Frame == frame && sprite.Y == y);
            Assert.Equal(51, run.Sprite("PNrmHealth").Y);
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
