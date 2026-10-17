using System;
using System.IO;

using Mooh.Core.Content;
using Mooh.Core.Session;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using SDL3;

using SkiaSharp;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Checks full-window composition using the actual level, HUD, menu and backdrop assets.</summary>
    public sealed class ViewportRenderingTests
    {
        /// <summary>Gameplay and fixed screens fill every edge at wide and tall window sizes.</summary>
        [Theory]
        [InlineData(2520, 1080, "game")]
        [InlineData(3840, 1080, "game")]
        [InlineData(2750, 600, "game")]
        [InlineData(2520, 1080, "main")]
        [InlineData(3840, 1080, "main")]
        [InlineData(2750, 600, "main")]
        [InlineData(2520, 1080, "pause")]
        [InlineData(3840, 1080, "pause")]
        [InlineData(2750, 600, "pause")]
        [InlineData(600, 900, "game")]
        [InlineData(800, 1420, "game")]
        [InlineData(600, 900, "main")]
        [InlineData(800, 1420, "main")]
        [InlineData(600, 900, "pause")]
        [InlineData(800, 1420, "pause")]
        public void ActualScreensFillWindowEdges(int width, int height, string screen)
        {
            string assets = Path.Combine(AppContext.BaseDirectory, "assets");
            using FileStream animation = File.OpenRead(Path.Combine(assets, "animations", "animations.json"));
            using FileStream text = File.OpenRead(Path.Combine(assets, "text", "strings.bin"));
            using FileStream font = File.OpenRead(Path.Combine(assets, "fonts", HudPresenter.LevelFontName + ".fnt"));
            GameShell shell = new(assets, JsonLevelPackLoader.Load(Path.Combine(assets, "levels", "manifest.json")),
                AnimationLibrary.Load(animation), StringTable.Load(text), BitmapFont.Load(HudPresenter.LevelFontName, font),
                new PlayerProfile(), null);
            if (screen == "main")
            {
                shell.InspectScreen(screen);
            }
            else
            {
                shell.StartDirect(0, DifficultySpeeds.Standard);
            }
            for (int frame = 0; frame < 120; frame++)
            {
                if (shell.Game is DesktopGame game)
                {
                    game.Camera.HorizontalMargin = FramePresentation.HorizontalMargin(width, height);
                    game.Camera.VerticalMargin = FramePresentation.VerticalMargin(width, height);
                }
                shell.Update(default, [], 0.1);
            }
            if (screen == "pause")
            {
                shell.HandleKey(SDL.Keycode.Escape, true);
                for (int frame = 0; frame < 5; frame++)
                {
                    shell.Update(default, [], 0.1);
                }
            }

            using SceneRenderer scene = new(Path.Combine(assets, "sprites"));
            using HudRenderer hud = new(Path.Combine(assets, "fonts"));
            using ScreenRenderer screens = new(Path.Combine(assets, "screens"), scene, hud);
            using FramePresentation presentation = new();
            using SKBitmap bitmap = new(width, height);
            using SKCanvas canvas = new(bitmap);
            presentation.Draw(canvas, width, height, shell.ShowsWorld, true, (target, margin) =>
            {
                if (shell.ShowsWorld && shell.Game is DesktopGame game)
                {
                    game.Camera.HorizontalMargin = margin;
                    game.Camera.VerticalMargin = FramePresentation.VerticalMargin(width, height);
                    scene.DrawBackdrop(target, game.Backdrop, game.Camera);
                    scene.Draw(target, game.Presenter.Sprites, game.Camera);
                    hud.Draw(target, game, scene);
                }
                screens.Draw(target, shell.Elements, margin,
                    shell.ShowsWorld ? FramePresentation.VerticalMargin(width, height) : 0);
            });
            Assert.NotEqual(SKColors.Black, bitmap.GetPixel(0, height / 2));
            Assert.NotEqual(SKColors.Black, bitmap.GetPixel(width - 1, height / 2));
            Assert.NotEqual(SKColors.Black, bitmap.GetPixel(width / 2, 0));
            Assert.NotEqual(SKColors.Black, bitmap.GetPixel(width / 2, height - 1));

            // Opt-in captures let visual checks use the same real rendering path as the regression.
            if (Environment.GetEnvironmentVariable("MOOH_CAPTURE_DIR") is string output)
            {
                _ = Directory.CreateDirectory(output);
                using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                using FileStream file = File.Create(Path.Combine(output, $"{screen}-{width}x{height}.png"));
                png.SaveTo(file);
            }
        }
    }
}
