using System;
using System.IO;

using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using SkiaSharp;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Checks that menu composition preserves the same native text rasterization as gameplay.</summary>
    public sealed class MenuTextQualityTests
    {
        /// <summary>Fitted menu text matches direct gameplay text across scaled, wide and portrait views.</summary>
        [Theory]
        [InlineData(1600, 1200)]
        [InlineData(2560, 1440)]
        [InlineData(3840, 1080)]
        [InlineData(1200, 1800)]
        public void MenuAndGameplayTextHaveMatchingNativePixels(int width, int height)
        {
            using HudRenderer text = new(Path.Combine(AppContext.BaseDirectory, "assets", "fonts"));
            using SKBitmap menu = Render(false);
            using SKBitmap gameplay = Render(true);
            float scale = Math.Min(width / 800f, height / 600f);
            int centerX = width / 2, centerY = height / 2;
            for (int y = centerY - (int)(110 * scale); y < centerY + (int)(30 * scale); y++)
            {
                for (int x = centerX - (int)(230 * scale); x < centerX + (int)(230 * scale); x++)
                {
                    SKColor expected = gameplay.GetPixel(x, y), actual = menu.GetPixel(x, y);
                    // Gradient coordinates can round by one channel step when the
                    // surface origin changes; edge coverage must otherwise match.
                    Assert.Equal(expected.Alpha, actual.Alpha);
                    Assert.InRange(Math.Abs(expected.Red - actual.Red), 0, 1);
                    Assert.InRange(Math.Abs(expected.Green - actual.Green), 0, 1);
                    Assert.InRange(Math.Abs(expected.Blue - actual.Blue), 0, 1);
                }
            }

            SKBitmap Render(bool world)
            {
                SKBitmap bitmap = new(width, height);
                using SKCanvas canvas = new(bitmap);
                using FramePresentation presentation = new();
                presentation.Draw(canvas, width, height, world, true, (target, _) =>
                {
                    target.Clear(SKColors.Black);
                    text.DrawText(target, new HudText("MAIN MENU", 400, 250, TextAlignment.Center, "MenuFntTit"));
                    text.DrawText(target, new HudText("New game", 400, 310, TextAlignment.Center, "MenuFntBtnNrm"));
                });
                return bitmap;
            }
        }
    }
}
