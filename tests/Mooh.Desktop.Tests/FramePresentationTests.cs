using SkiaSharp;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Exercises the actual compositor with readable synthetic frames.</summary>
    public sealed class FramePresentationTests
    {
        /// <summary>The wider view reveals sharp world content outside the old screen.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GameplayExpandsWithoutBlurringTheWorld(bool fullColor)
        {
            using FramePresentation presentation = new();
            using SKBitmap bitmap = new(1280, 720);
            using SKCanvas canvas = new(bitmap);
            presentation.Draw(canvas, 1280, 720, true, fullColor, (target, margin) =>
            {
                Assert.Equal(400f / 3, margin, 3);
                target.Clear(SKColors.Blue);
                using SKPaint paint = new()
                {
                    Color = SKColors.Red
                };
                target.DrawRect(-120, 100, 40, 100, paint);
            });
            Assert.Equal(SKColors.Red, bitmap.GetPixel(30, 180));
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(90, 180));
        }

        /// <summary>The original stays crisp while its borders contain a softened copy.</summary>
        [Fact]
        public void FixedScreensGetGaussianBlurredBorders()
        {
            using FramePresentation presentation = new();
            using SKBitmap bitmap = new(1280, 720);
            using SKCanvas canvas = new(bitmap);
            presentation.Draw(canvas, 1280, 720, false, true, (target, margin) =>
            {
                Assert.Equal(0, margin);
                target.Clear(SKColors.Blue);
                using SKPaint paint = new()
                {
                    Color = SKColors.Red
                };
                target.DrawRect(0, 0, 800, 300, paint);
            });
            Assert.Equal(SKColors.Red, bitmap.GetPixel(640, 350));
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(640, 370));
            SKColor blurred = bitmap.GetPixel(40, 360);
            Assert.InRange(blurred.Red, (byte)20, (byte)235);
            Assert.InRange(blurred.Blue, (byte)20, (byte)235);
            Assert.Equal(255, blurred.Alpha);
        }

        /// <summary>A 4:3 window still shows the complete original frame.</summary>
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void FourByThreeKeepsOriginalBounds(bool world, bool fullColor)
        {
            using FramePresentation presentation = new();
            using SKBitmap bitmap = new(800, 600);
            using SKCanvas canvas = new(bitmap);
            presentation.Draw(canvas, 800, 600, world, fullColor, (target, margin) =>
            {
                Assert.Equal(0, margin);
                target.Clear(SKColors.Blue);
            });
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(0, 0));
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(799, 599));
        }
        /// <summary>Resizing keeps the center fixed and caps the world at 16:9 in both color modes.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ResizeBetweenPortraitAndUltrawideKeepsWorldCentered(bool fullColor)
        {
            using FramePresentation presentation = new();
            foreach ((int width, int height, float expectedMargin) in new[]
            {
                (600, 900, 0f), (2560, 1080, 400f / 3), (800, 600, 0f)
            })
            {
                using SKBitmap bitmap = new(width, height);
                using SKCanvas canvas = new(bitmap);
                presentation.Draw(canvas, width, height, true, fullColor, (target, margin) =>
                {
                    Assert.Equal(expectedMargin, margin, 3);
                    target.Clear(SKColors.Blue);
                    using SKPaint paint = new()
                    {
                        Color = SKColors.Red
                    };
                    target.DrawRect(395, 295, 10, 10, paint);
                });
                Assert.Equal(SKColors.Red, bitmap.GetPixel(width / 2, height / 2));
                if (width == 600)
                {
                    Assert.Equal(SKColors.Black, bitmap.GetPixel(300, 0));
                }
                else if (width == 2560)
                {
                    Assert.Equal(SKColors.Black, bitmap.GetPixel(0, 540));
                    Assert.Equal(SKColors.Blue, bitmap.GetPixel(325, 540));
                }
            }
        }
    }
}
