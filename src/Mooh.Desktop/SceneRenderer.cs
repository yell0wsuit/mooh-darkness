using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Presentation.Scene;
using Mooh.Presentation.Sprites;

using SkiaSharp;

namespace Mooh.Desktop
{
    /// <summary>
    /// Draws the presenter's sprites from the recovered atlases. A frame's anchor is its object's
    /// screen position; the frame spans its recovered edges around it.
    /// </summary>
    /// <param name="spritesDirectory">The folder holding the <c>Sprite*.png</c> atlases and their <c>.seq</c> tables.</param>
    internal sealed class SceneRenderer(string spritesDirectory) : IDisposable
    {
        private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.None);
        private readonly Dictionary<string, Atlas> _atlases = [with(StringComparer.Ordinal)];
        private readonly SKPaint _additive = new() { BlendMode = SKBlendMode.Plus };
        private readonly string _spritesDirectory = spritesDirectory;
        private SKImage? _backdropImage;

        /// <summary>Draws the backdrop: its image, then its tiles, lamps and glows.</summary>
        /// <param name="canvas">The canvas, set up in the original's 800 by 600 screen space.</param>
        /// <param name="backdrop">The level's backdrop.</param>
        /// <param name="camera">The camera over the level.</param>
        public void DrawBackdrop(SKCanvas canvas, Backdrop backdrop, SceneCamera camera)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(backdrop);
            ArgumentNullException.ThrowIfNull(camera);
            (double baseX, double baseY) = camera.Base;
            BackdropLayout layout = backdrop.Layout(baseX, baseY);
            _backdropImage ??= SKImage.FromEncodedData(Path.Combine(_spritesDirectory, "..", "screens", Backdrop.ImageName + ".jpg"));
            if (_backdropImage is not null)
            {
                canvas.DrawImage(_backdropImage, layout.ImageX, layout.ImageY, Sampling);
            }

            foreach (BackdropSprite sprite in layout.Sprites)
            {
                DrawFrame(canvas, sprite.Page, sprite.Frame, sprite.X, sprite.Y, sprite.Additive ? _additive : null);
            }
        }

        /// <summary>Draws the sprites in their order.</summary>
        /// <param name="canvas">The canvas, set up in the original's 800 by 600 screen space.</param>
        /// <param name="sprites">The presenter's sprites.</param>
        /// <param name="camera">The camera over the level.</param>
        public void Draw(SKCanvas canvas, IReadOnlyList<SceneSprite> sprites, SceneCamera camera)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(sprites);
            ArgumentNullException.ThrowIfNull(camera);
            foreach (SceneSprite sprite in sprites)
            {
                (double x, double y) = camera.ToScreen(sprite.X, sprite.Y);
                DrawFrame(canvas, sprite.Page, sprite.Frame, x, y, null);
            }
        }

        /// <summary>Releases every loaded atlas image.</summary>
        public void Dispose()
        {
            foreach (Atlas atlas in _atlases.Values)
            {
                atlas.Image.Dispose();
            }

            _atlases.Clear();
            _backdropImage?.Dispose();
            _additive.Dispose();
        }

        private void DrawFrame(SKCanvas canvas, string page, string name, double x, double y, SKPaint? paint)
        {
            Atlas atlas = AtlasOf(page);
            if (!atlas.Frames.Contains(name))
            {
                return;
            }

            SpriteFrame frame = atlas.Frames[name];
            if (frame.Width <= 0 || frame.Height <= 0)
            {
                return;
            }

            canvas.DrawImage(
                atlas.Image,
                SKRect.Create(frame.X, frame.Y, frame.Width, frame.Height),
                SKRect.Create((float)(x + frame.PivotX), (float)(y + frame.PivotY), frame.Width, frame.Height),
                Sampling,
                paint);
        }

        private Atlas AtlasOf(string page)
        {
            if (_atlases.TryGetValue(page, out Atlas? atlas))
            {
                return atlas;
            }

            string table = Path.Combine(_spritesDirectory, page + ".seq");
            string image = Path.Combine(_spritesDirectory, page + ".png");
            using FileStream stream = File.OpenRead(table);
            SpritePage frames = SpritePage.Load(page, stream);
            SKImage pixels = SKImage.FromEncodedData(image)
                ?? throw new InvalidDataException($"The atlas '{image}' could not be decoded.");
            atlas = new Atlas(pixels, frames);
            _atlases[page] = atlas;
            return atlas;
        }

        private sealed record Atlas(SKImage Image, SpritePage Frames);
    }
}
