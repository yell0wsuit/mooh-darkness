using System;
using System.IO;

using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using SkiaSharp;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Checks that bold UI text keeps a clear, compact outline.</summary>
    public sealed class BitmapOutlineTests
    {
        /// <summary>The button rim stays compact so it does not overwhelm the colored bold face.</summary>
        [Theory]
        [InlineData("MenuFntBtnNrm")]
        [InlineData("MenuFntBtnSel")]
        public void MenuOutlineStaysCompact(string style)
        {
            using SKBitmap bitmap = new(160, 120);
            using SKCanvas canvas = new(bitmap);
            canvas.Clear(SKColors.Transparent);
            using HudRenderer renderer = new(Path.Combine(AppContext.BaseDirectory, "assets", "fonts"));
            renderer.DrawText(canvas, new HudText("H", 40, 80, TextAlignment.Left, style));
            int fillRight = -1, outlineRight = -1;
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    SKColor pixel = bitmap.GetPixel(x, y);
                    if (pixel.Alpha > 100)
                    {
                        outlineRight = Math.Max(outlineRight, x);
                    }
                    if (pixel.Alpha > 220 && pixel.Red > 230 && pixel.Green > 150 && pixel.Blue < 80)
                    {
                        fillRight = Math.Max(fillRight, x);
                    }
                }
            }
            Assert.True(fillRight >= 0);
            Assert.InRange(outlineRight - fillRight, 1, 2);
        }
    }
}
