using System;
using System.IO;

using Mooh.Presentation.Sprites;

using SkiaSharp;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Checks atlas frame edges at fractional desktop viewport scales.</summary>
    public sealed class SpriteSeamTests
    {
        /// <summary>Filtering a frame must not sample the atlas pixels beside its rectangle.</summary>
        [Theory]
        [InlineData("SpriteCellMetal", "OCellM01", 1.25f)]
        [InlineData("SpriteCellMetal", "OCellM01", 1.67f)]
        [InlineData("SpriteCellMetal", "OCellM01", 2.0f)]
        [InlineData("SpriteObjs", "ORock01", 1.25f)]
        [InlineData("SpriteObjs", "ORock01", 1.67f)]
        [InlineData("SpriteObjs", "ORock01", 2.0f)]
        [InlineData("SpriteObjBombs", "OSt00", 1.25f)]
        [InlineData("SpriteObjBombs", "OSt00", 1.67f)]
        [InlineData("SpriteObjBombs", "OSt00", 2.0f)]
        public void ScaledSpritesMatchAnIsolatedFrame(string page, string name, float scale)
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "assets", "sprites");
            using FileStream stream = File.OpenRead(Path.Combine(directory, page + ".json"));
            SpriteFrame frame = SpritePage.Load(page, stream)[name];
            using SKImage atlas = SKImage.FromEncodedData(Path.Combine(directory, page + ".png"));
            using SKImage isolated = atlas.Subset(SKRectI.Create(frame.X, frame.Y, frame.Width, frame.Height));
            using SceneRenderer renderer = new(directory);
            using SKBitmap actual = new(240, 240);
            using SKBitmap expected = new(240, 240);
            using (SKCanvas canvas = new(actual))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(scale);
                renderer.DrawScreenFrame(canvas, page, name, 50.3, 60.2);
            }

            using (SKCanvas canvas = new(expected))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(scale);
                canvas.DrawImage(isolated,
                    SKRect.Create((float)(50.3 + frame.PivotX), (float)(60.2 + frame.PivotY), frame.Width, frame.Height),
                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            }

            for (int y = 0; y < actual.Height; y++)
            {
                for (int x = 0; x < actual.Width; x++)
                {
                    Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
                }
            }
        }
    }
}
