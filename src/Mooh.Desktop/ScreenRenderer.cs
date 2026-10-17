using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Presentation.Hud;
using Mooh.Presentation.Screens;

using SkiaSharp;

namespace Mooh.Desktop
{
    /// <summary>Draws ordered shell commands with the original screen images, atlases and bitmap fonts.</summary>
    internal sealed class ScreenRenderer(string screensDirectory, SceneRenderer scene, HudRenderer text) : IDisposable
    {
        private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.None);
        private readonly Dictionary<string, SKImage> _images = [with(StringComparer.Ordinal)];
        private readonly SKPaint _opacity = new();

        public void Draw(SKCanvas canvas, IReadOnlyList<ScreenElement> elements, float horizontalMargin = 0, float verticalMargin = 0)
        {
            foreach (ScreenElement element in elements)
            {
                if (element is ScreenImage image)
                {
                    if (!_images.TryGetValue(image.Name, out SKImage? asset))
                    {
                        asset = SKImage.FromEncodedData(Path.Combine(screensDirectory, image.Name))
                            ?? throw new InvalidDataException("Cannot decode screen image '" + image.Name + "'.");
                        _images.Add(image.Name, asset);
                    }
                    _opacity.Color = SKColors.White.WithAlpha((byte)Math.Round(255 * Math.Clamp(image.Opacity, 0, 1)));
                    canvas.DrawImage(asset, SKRect.Create((float)image.X, (float)image.Y, (float)image.Width, (float)image.Height), Sampling, _opacity);
                }
                else if (element is ScreenTile tile)
                {
                    scene.DrawScreenTile(canvas, tile.Page, tile.Frame, horizontalMargin, verticalMargin);
                }
                else if (element is ScreenSprite sprite)
                {
                    scene.DrawScreenFrame(canvas, sprite.Page, sprite.Frame, sprite.X, sprite.Y);
                }
                else if (element is ScreenText label)
                {
                    text.DrawText(canvas, new HudText(label.Text, label.X, label.Y, label.Alignment, label.Font), label.Opacity);
                }
                else if (element is ScreenClip clip)
                {
                    int saved = canvas.Save();
                    canvas.ClipRect(SKRect.Create((float)clip.X, (float)clip.Y, (float)clip.Width, (float)clip.Height));
                    Draw(canvas, clip.Elements, horizontalMargin, verticalMargin);
                    canvas.RestoreToCount(saved);
                }
            }
        }

        public void Dispose()
        {
            foreach (SKImage image in _images.Values)
            {
                image.Dispose();
            }

            _images.Clear();
            _opacity.Dispose();
        }
    }
}
