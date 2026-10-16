using System;
using System.IO;
using System.Linq;

using Mooh.Presentation.Screens;
using Mooh.Presentation.Sprites;
using Mooh.Presentation.Text;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks original menu compositing, hover feedback and panel transitions.</summary>
    public sealed class MenuPanelTests
    {
        /// <summary>Default keyboard selection never creates a tooltip at an uninitialized mouse position.</summary>
        [Fact]
        public void KeyboardSelectionDoesNotCreateAMouseTooltip()
        {
            MenuPanelPresentation menu = Create();
            menu.Show(MenuPanelKind.Main, "Menu", [new MenuItem("start", "Start", "Begin", 0, 0)]);
            menu.Advance(1);

            Assert.DoesNotContain(menu.Elements.OfType<ScreenText>(), text => text.Font == "MenuFontToolTip");
        }

        /// <summary>A hovered tooltip tiles its original background and stays inside the right edge.</summary>
        [Fact]
        public void HoverTooltipUsesItsFontBoundsAndOriginalBackgroundTiles()
        {
            MenuPanelPresentation menu = Create();
            menu.Show(MenuPanelKind.Main, "Menu", [new MenuItem("start", "Start", "A tooltip long enough to need several background tiles", 0, 0)], centerX: 310);
            menu.Advance(0.35);
            MenuButton button = menu.Buttons[0];
            menu.MovePointer(button.Right - 1, button.Top + 3);
            menu.Advance(0.75);
            ScreenText tooltip = Assert.Single(menu.Elements.OfType<ScreenText>(), text => text.Font == "MenuFontToolTip");
            ScreenSprite[] tiles = [.. menu.Elements.OfType<ScreenSprite>().Where(sprite => sprite.Frame.StartsWith("OTTip", StringComparison.Ordinal))];
            Assert.NotEmpty(tiles);
            Assert.Equal("OTTipLB", tiles[0].Frame);
            Assert.Equal("OTTipR", tiles[^1].Frame);
            Assert.Contains(tiles, tile => tile.Frame == "OTTipC");
            Assert.Equal(800, tiles[^1].X + 16);
            Assert.Equal(button.Top + 3 - 45, tooltip.Y);
            Assert.True(tooltip.X < button.Right + 9);
        }

        /// <summary>Highlighted and pressed lettering retains the normal font composited above it.</summary>
        [Fact]
        public void SelectedAndPressedTextRetainTheNormalFontOverlay()
        {
            MenuPanelPresentation menu = Create();
            menu.Show(MenuPanelKind.Main, "Menu", [new MenuItem("start", "Start", "", 0, 0)]);
            menu.Advance(0.35);
            Assert.Equal(["MenuFntBtnSel", "MenuFntBtnNrm"], menu.Elements.OfType<ScreenText>().Where(text => text.Text == "Start").Select(text => text.Font));
            menu.Press();
            Assert.Equal(["MenuFntBtnPrs", "MenuFntBtnNrm"], menu.Elements.OfType<ScreenText>().Where(text => text.Text == "Start").Select(text => text.Font));
        }

        /// <summary>The previous panel remains visible throughout its original exit slide.</summary>
        [Fact]
        public void ReplacementKeepsTheOldPanelSlidingOutFor350Milliseconds()
        {
            MenuPanelPresentation menu = Create();
            menu.Show(MenuPanelKind.Main, "Old", [new MenuItem("old", "Old button", "", 0, 0)]);
            menu.Advance(0.35);
            menu.Show(MenuPanelKind.Message, "New", [new MenuItem("new", "New button", "", 0, 0)]);
            Assert.Equal(12, menu.Elements.OfType<ScreenSprite>().Count(sprite => sprite.Frame.StartsWith("c_BackMain_", StringComparison.Ordinal)));
            menu.Advance(0.175);
            Assert.Equal(50, menu.Elements.OfType<ScreenSprite>().First(sprite => sprite.Frame.StartsWith("c_BackMain_", StringComparison.Ordinal)).Y);
            Assert.Equal(["MenuFntBtnNrm"], menu.Elements.OfType<ScreenText>().Where(text => text.Text == "Old button").Select(text => text.Font));
            menu.Advance(0.175);
            Assert.DoesNotContain(menu.Elements.OfType<ScreenSprite>(), sprite => sprite.Frame.StartsWith("c_BackMain_", StringComparison.Ordinal));
        }

        /// <summary>Panel content draws between the background and title and survives the complete exit slide.</summary>
        [Fact]
        public void PanelContentsKeepOriginalLayerOrderAndExitPosition()
        {
            MenuPanelPresentation menu = Create();
            menu.Show(MenuPanelKind.Main, "Old", [new MenuItem("old", "Old button", "", 0, 0)]);
            menu.Contents = [new ScreenText("Old score", 10, 20)];
            menu.Advance(0.35);
            ScreenElement[] original = [.. menu.Elements];
            Assert.Equal("Old score", Assert.IsType<ScreenText>(original[12]).Text);
            Assert.Equal("Old", Assert.IsType<ScreenText>(original[13]).Text);
            menu.Show(MenuPanelKind.Message, "New", [new MenuItem("new", "New button", "", 0, 0)]);
            Assert.Empty(menu.Contents);
            menu.Advance(0.175);
            ScreenText score = Assert.Single(menu.Elements.OfType<ScreenText>(), text => text.Text == "Old score");
            Assert.Equal((410.0, 70.0), (score.X, score.Y));
            menu.Advance(0.175);
            Assert.DoesNotContain(menu.Elements.OfType<ScreenText>(), text => text.Text == "Old score");
        }

        /// <summary>Entry gives buttons a bounded downward bounce which settles at their original anchors.</summary>
        [Fact]
        public void ButtonEntryBounceSettlesAndItsAnchorMatchesDrawing()
        {
            MenuPanelPresentation menu = Create();
            menu.Show(MenuPanelKind.Main, "Menu", [new MenuItem("start", "Start", "", 0, 0)]);
            menu.Advance(0.35);
            (double x, double y) = menu.GetButtonAnchor("start");
            menu.Advance(0.025);
            (double bouncedX, double bouncedY) = menu.GetButtonAnchor("start");
            Assert.InRange(bouncedY - y, 1, 10);
            ScreenSprite button = Assert.Single(menu.Elements.OfType<ScreenSprite>(), sprite => sprite.Frame.StartsWith("OBtn", StringComparison.Ordinal));
            Assert.Equal((bouncedX, bouncedY), (button.X, button.Y));
            for (int frame = 0; frame < 120; frame++)
            {
                menu.Advance(0.025);
            }

            Assert.Equal((x, y), menu.GetButtonAnchor("start"));
        }

        /// <summary>A stalled frame applies the same native 25 ms spring steps as ordinary menu updates.</summary>
        [Fact]
        public void ButtonSpringUsesOriginalMenuSubsteps()
        {
            MenuPanelPresentation largeFrame = Create();
            MenuPanelPresentation stepped = Create();
            foreach (MenuPanelPresentation menu in new[] { largeFrame, stepped })
            {
                menu.Show(MenuPanelKind.Main, "Menu", [new MenuItem("start", "Start", "", 0, 0)]);
                menu.Advance(0.35);
            }

            largeFrame.Advance(0.1);
            for (int frame = 0; frame < 4; frame++)
            {
                stepped.Advance(0.025);
            }

            Assert.Equal(stepped.GetButtonAnchor("start"), largeFrame.GetButtonAnchor("start"));
        }

        /// <summary>Checkbox glyph updates preserve entry state and button geometry.</summary>
        [Fact]
        public void UpdatingButtonTextDoesNotRestartPanelEntry()
        {
            MenuPanelPresentation menu = Create();
            menu.Show(MenuPanelKind.Main, "Menu", [new MenuItem("check", "One", "", 0, 0)]);
            menu.Advance(0.35);
            (double x, double y) = menu.GetButtonAnchor("check");
            menu.SetButtonLabel("check", "Two");
            Assert.True(menu.IsReady);
            Assert.Equal((x, y), menu.GetButtonAnchor("check"));
            Assert.Contains(menu.Elements.OfType<ScreenText>(), text => text.Text == "Two");
            Assert.DoesNotContain(menu.Elements.OfType<ScreenText>(), text => text.Text == "One");
            menu.Press();
            Assert.Equal((x + 2, y + 2), menu.GetButtonAnchor("check"));
        }

        /// <summary>The source panel composites finish their entry before accepting activation.</summary>
        [Fact]
        public void PanelUsesCompositeTilesAndWaitsForItsSlideBeforeAcceptingInput()
        {
            MenuPanelPresentation menu = Create();
            menu.Show(MenuPanelKind.Main, "Menu", [new MenuItem("start", "Start", "Begin", 0, 0)]);
            Assert.Equal(12, menu.Elements.OfType<ScreenSprite>().Count(frame => frame.Frame.StartsWith("c_BackMain_", StringComparison.Ordinal)));
            Assert.False(menu.IsReady);
            Assert.Null(menu.Activate());
            menu.Advance(0.35);
            Assert.True(menu.IsReady);
            Assert.Equal("start", menu.Activate());
            Assert.All(menu.Elements.OfType<ScreenSprite>().Take(12), frame => Assert.Equal(300, frame.Y));
        }

        /// <summary>Keyboard wrapping and pointer release preserve the originally selected action.</summary>
        [Fact]
        public void NavigationWrapsAndMouseReleaseRequiresThePressedButton()
        {
            MenuPanelPresentation menu = Create();
            menu.Show(MenuPanelKind.Main, "Menu", [new MenuItem("a", "One", "", 0, 0), new MenuItem("b", "Two", "", 0, 60)]);
            menu.Advance(0.35);
            menu.Navigate(-1);
            Assert.Equal("b", menu.Activate());
            MenuButton first = menu.Buttons[0];
            menu.MovePointer(first.Left + 2, first.Top + 2);
            menu.Press();
            menu.MovePointer(0, 0);
            Assert.Null(menu.Release());
        }

        private static MenuPanelPresentation Create()
        {
            using FileStream page = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "sprites", "SpriteMenuPrt.seq"));
            using FileStream font = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "fonts", "MenuFntBtnNrm.fnt"));
            using FileStream tooltip = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "fonts", "MenuFontToolTip.fnt"));
            return new MenuPanelPresentation(SpritePage.Load("SpriteMenuPrt", page), BitmapFont.Load("MenuFntBtnNrm", font), BitmapFont.Load("MenuFontToolTip", tooltip), new Random(1));
        }
    }
}
