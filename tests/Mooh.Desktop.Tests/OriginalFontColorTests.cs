using System;
using System.IO;

using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using SkiaSharp;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Palette values sampled from the original bitmap atlases before the TrueType migration.</summary>
    public sealed class OriginalFontColorTests
    {
        [Theory]
        [InlineData("AppFont", 0xFFFFFFFFu)]
        [InlineData("EditFntSelTit", 0xFFFFFFBBu)]
        [InlineData("EditFntTit", 0xFFFFFF80u)]
        [InlineData("EditFntTTip", 0xFF000000u)]
        [InlineData("MapStoryFont", 0xFF000000u)]
        [InlineData("MenuFntBtnNrm", 0xFFFEBD2Cu)]
        [InlineData("MenuFntBtnSel", 0xFFF5F532u)]
        [InlineData("MenuFntBtnPrs", 0xFFB27D09u)]
        [InlineData("MenuFntEncTit", 0xFFFFBE2Du)]
        [InlineData("MenuFntEncTitSel", 0xFFF5F532u)]
        [InlineData("MenuFntEncTxt", 0xFFF5F532u)]
        [InlineData("MenuFntMapTxt", 0xFFF5F532u)]
        [InlineData("MenuFontRegTxt", 0xFFFFE000u)]
        [InlineData("MenuFontToolTip", 0xFFFFFFFFu)]
        [InlineData("MenuTTipFont", 0xFF8745ABu)]
        [InlineData("PanelDigFnt", 0xFFF6E73Du)]
        [InlineData("PanelDigScr", 0xFFF6E73Du)]
        [InlineData("PanelDigTextFnt", 0xFFF6E73Du)]
        [InlineData("PanelDigTextRedFnt", 0xFFF63D3Du)]
        [InlineData("PanelInstrBlackFnt", 0xFF000000u)]
        [InlineData("VersionFont", 0xFFEBEBEBu)]
        public void SolidFillsUseOriginalPalette(string style, uint color)
        {
            using SKBitmap bitmap = Render(style);
            int matches = 0;
            foreach (SKColor pixel in bitmap.Pixels)
            {
                if (pixel == new SKColor(color))
                {
                    matches++;
                }
            }
            Assert.True(matches >= 10, $"{style} must contain the original fill #{color:X8}.");
        }

        [Theory]
        [InlineData("MapStoryFont")]
        [InlineData("PanelInstrBlackFnt")]
        [InlineData("EditFntTTip")]
        public void PlainBlackStylesHaveNoAddedColoredOutline(string style)
        {
            using SKBitmap bitmap = Render(style);
            Assert.All(bitmap.Pixels, pixel => Assert.True(pixel.Alpha == 0 || (pixel.Red == 0 && pixel.Green == 0 && pixel.Blue == 0)));
        }

        [Fact]
        public void TooltipRetainsItsOrangeRedEdge()
        {
            using SKBitmap bitmap = Render("MenuFontToolTip");
            Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 20 && pixel.Red > 200 && pixel.Green < 150 && pixel.Blue < 100);
        }

        [Fact]
        public void MenuTitleRetainsGoldGradientAndGrayBevel()
        {
            using SKBitmap bitmap = Render("MenuFntTit");
            Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 200 && pixel.Red > 245 && pixel.Green > 215 && pixel.Blue is > 50 and < 100);
            Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 200 && pixel.Red > 240 && pixel.Green is > 160 and < 190 && pixel.Blue < 20);
            Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 100 && pixel.Red is > 80 and < 200 && pixel.Red == pixel.Green && pixel.Green == pixel.Blue);
        }

        [Fact]
        public void FloatingScoreRetainsGreenAndPurpleEdging()
        {
            using SKBitmap bitmap = Render("GameFontScr");
            Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 150 && pixel.Green > 130 && pixel.Red < 100 && pixel.Blue < 40);
            Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 150 && pixel.Blue > 150 && pixel.Red > 100 && pixel.Green < 130);
        }

        [Fact]
        public void FadingTextFadesAllEdgeLayersTogether()
        {
            using SKBitmap bitmap = Render("GameFontScr", 0.25);
            Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 0);
            Assert.All(bitmap.Pixels, pixel => Assert.InRange(pixel.Alpha, (byte)0, (byte)64));
        }

        private static SKBitmap Render(string style, double opacity = 1)
        {
            SKBitmap bitmap = new(600, 140);
            using SKCanvas canvas = new(bitmap);
            canvas.Clear(SKColors.Transparent);
            using HudRenderer renderer = new(Path.Combine(AppContext.BaseDirectory, "assets", "fonts"));
            renderer.DrawText(canvas, new HudText("Hello 123", 30, 90, TextAlignment.Left, style), opacity);
            return bitmap;
        }
    }
}
