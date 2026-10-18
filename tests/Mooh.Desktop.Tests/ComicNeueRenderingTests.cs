using System;
using System.IO;

using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using SkiaSharp;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Checks rendered Comic Neue glyph placement against the measured layout.</summary>
    public sealed class ComicNeueRenderingTests
    {
        /// <summary>Checks that left, centered and right alignment use the same metrics for layout and drawing.</summary>
        [Theory]
        [InlineData(TextAlignment.Left, 200)]
        [InlineData(TextAlignment.Center, 400)]
        [InlineData(TextAlignment.Right, 600)]
        public void RasterizedTextUsesMeasuredAlignment(TextAlignment alignment, double anchor)
        {
            string fonts = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
            using FileStream stream = File.OpenRead(Path.Combine(fonts, GameFont.FileName("MenuFntBtnNrm")));
            using GameFont font = GameFont.Load("MenuFntBtnNrm", stream);
            Assert.Equal("Comic Neue", font.Font.Typeface.FamilyName);
            using HudRenderer renderer = new(fonts);
            using SKBitmap bitmap = new(800, 160);
            using SKCanvas canvas = new(bitmap);
            canvas.Clear(SKColors.Transparent);
            renderer.DrawText(canvas, new HudText("Play Café", anchor, 80, alignment, "MenuFntBtnNrm"));
            int left = bitmap.Width, right = -1;
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).Alpha != 0)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                    }
                }
            }
            Assert.True(right > left);
            double pen = font.AlignedX("Play Café", anchor, alignment);
            (int boundsLeft, _, int boundsRight, _) = font.Bounds("Play Café");
            Assert.InRange(left, pen + boundsLeft - 2, pen + boundsLeft + 2);
            Assert.InRange(right, pen + boundsRight - 2, pen + boundsRight + 2);
        }
    }
}
