using System;

using SkiaSharp;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Exercises the actual compositor with readable synthetic frames.</summary>
    public sealed class FramePresentationTests
    {
        /// <summary>A full-color native-size menu draws directly on the supplied canvas.</summary>
        [Fact]
        public void FourByThreeMenuDoesNotNeedAnIntermediateSurface()
        {
            using FramePresentation presentation = new();
            using SKBitmap bitmap = new(800, 600);
            using SKCanvas canvas = new(bitmap);
            presentation.Draw(canvas, 800, 600, false, true, (target, margin) =>
            {
                Assert.Same(canvas, target);
                Assert.Equal(0, margin);
                target.Clear(SKColors.Blue);
                using SKPaint paint = new()
                {
                    Color = SKColors.Red
                };
                target.DrawRect(395, 295, 10, 10, paint);
            });

            Assert.Equal(SKColors.Red, bitmap.GetPixel(400, 300));
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(0, 0));
        }

        /// <summary>Scaled fixed screens retain the original 800x600 composition and filtering.</summary>
        [Fact]
        public void ScaledFourByThreeMenuRetainsLogicalPixelFiltering()
        {
            using FramePresentation presentation = new();
            using SKBitmap bitmap = new(1600, 1200);
            using SKCanvas canvas = new(bitmap);
            presentation.Draw(canvas, 1600, 1200, false, true, (target, _) =>
            {
                target.Clear(SKColors.Black);
                using SKPaint paint = new()
                {
                    Color = SKColors.Red
                };
                target.DrawRect(400, 300, 1, 1, paint);
            });

            // The logical pixel is linearly upscaled, preserving its soft edges.
            SKColor edge = bitmap.GetPixel(800, 600);
            Assert.InRange(edge.Red, (byte)100, (byte)200);
            Assert.Equal(0, edge.Blue);
            Assert.Equal(255, edge.Alpha);
        }

        /// <summary>Repeated menu frames and aspect changes must never reuse stale content.</summary>
        [Fact]
        public void MenuFramesRefreshAcrossAspectAndColorChanges()
        {
            using FramePresentation presentation = new();
            foreach ((int width, int height, bool fullColor, SKColor color) in new[]
            {
                (1280, 720, true, SKColors.Red),
                (800, 1420, true, SKColors.Blue),
                (1600, 1200, true, SKColors.Green),
                (1280, 720, false, SKColors.Red),
                (1280, 720, true, SKColors.Blue)
            })
            {
                using SKBitmap bitmap = new(width, height);
                using SKCanvas canvas = new(bitmap);
                int calls = 0;
                presentation.Draw(canvas, width, height, false, fullColor, (target, _) =>
                {
                    calls++;
                    target.Clear(color);
                });
                Assert.Equal(1, calls);
                Assert.Equal(color, bitmap.GetPixel(width / 2, height / 2));
                Assert.Equal(color, bitmap.GetPixel(0, 0));
            }
        }

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
        [Theory]
        [InlineData(1280, 720)]
        [InlineData(2520, 1080)]
        [InlineData(3840, 1080)]
        [InlineData(2750, 600)]
        public void FixedScreensGetGaussianBlurredBorders(int width, int height)
        {
            using FramePresentation presentation = new();
            using SKBitmap bitmap = new(width, height);
            using SKCanvas canvas = new(bitmap);
            presentation.Draw(canvas, width, height, false, true, (target, margin) =>
            {
                Assert.Equal(0, margin);
                target.Clear(SKColors.Blue);
                using SKPaint paint = new()
                {
                    Color = SKColors.Red
                };
                target.DrawRect(0, 0, 800, 300, paint);
            });
            Assert.Equal(SKColors.Red, bitmap.GetPixel(width / 2, (height / 2) - 10));
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(width / 2, (height / 2) + 10));
            SKColor blurred = bitmap.GetPixel(0, height / 2);
            Assert.InRange(blurred.Red, (byte)20, (byte)235);
            Assert.InRange(blurred.Blue, (byte)20, (byte)235);
            Assert.Equal(255, blurred.Alpha);
        }

        /// <summary>Portrait views expose sharp world content above and below the original screen.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TallScreenRevealsWorldAtOriginalScale(bool fullColor)
        {
            using FramePresentation presentation = new();
            using SKBitmap bitmap = new(800, 1420);
            using SKCanvas canvas = new(bitmap);
            presentation.Draw(canvas, 800, 1420, true, fullColor, (target, margin) =>
            {
                Assert.Equal(0, margin);
                target.Clear(SKColors.Blue);
                using SKPaint paint = new()
                {
                    Color = SKColors.Red
                };
                target.DrawRect(380, -390, 40, 40, paint);
                target.DrawRect(380, 950, 40, 40, paint);
            });
            Assert.Equal(SKColors.Red, bitmap.GetPixel(400, 40));
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(400, 80));
            Assert.Equal(SKColors.Red, bitmap.GetPixel(400, 1380));
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(400, 1340));
        }

        /// <summary>Fixed portrait screens retain sharp central content with blurred top and bottom borders.</summary>
        [Fact]
        public void TallFixedScreenGetsBlurAboveAndBelow()
        {
            using FramePresentation presentation = new();
            using SKBitmap bitmap = new(800, 1420);
            using SKCanvas canvas = new(bitmap);
            presentation.Draw(canvas, 800, 1420, false, true, (target, margin) =>
            {
                target.Clear(SKColors.Blue);
                using SKPaint paint = new()
                {
                    Color = SKColors.Red
                };
                target.DrawRect(0, 0, 400, 600, paint);
            });
            Assert.Equal(SKColors.Red, bitmap.GetPixel(390, 710));
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(410, 710));
            foreach (int y in new[] { 0, 1419 })
            {
                SKColor blurred = bitmap.GetPixel(400, y);
                Assert.InRange(blurred.Red, (byte)20, (byte)235);
                Assert.InRange(blurred.Blue, (byte)20, (byte)235);
                Assert.Equal(255, blurred.Alpha);
            }
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
        /// <summary>Resizing keeps the center fixed and fills the wider world in both color modes.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ResizeBetweenPortraitAndUltrawideKeepsWorldCentered(bool fullColor)
        {
            using FramePresentation presentation = new();
            foreach ((int width, int height, float expectedMargin) in new[]
            {
                (600, 900, 0f), (2520, 1080, 300f), (3840, 1080, 2000f / 3), (2750, 600, 975f), (800, 600, 0f)
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
                float scale = Math.Min(width / 800f, height / 600f);
                // Sample inside the solid body, away from linear filtering at its boundary.
                Assert.Equal(SKColors.Red, bitmap.GetPixel(width / 2, (height / 2) + (int)(3 * scale)));
                Assert.Equal(SKColors.Blue, bitmap.GetPixel(width / 2, (height / 2) + (int)(6 * scale)));
                if (width != 800 || height != 600)
                {
                    Assert.Equal(SKColors.Blue, bitmap.GetPixel(0, height / 2));
                    Assert.Equal(SKColors.Blue, bitmap.GetPixel(width - 1, height / 2));
                    Assert.Equal(SKColors.Blue, bitmap.GetPixel(width / 2, 0));
                    Assert.Equal(SKColors.Blue, bitmap.GetPixel(width / 2, height - 1));
                }
            }
        }
    }
}
