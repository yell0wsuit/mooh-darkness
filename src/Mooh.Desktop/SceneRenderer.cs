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
        private readonly string _spritesDirectory = spritesDirectory;

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
                Atlas atlas = AtlasOf(sprite.Page);
                if (!atlas.Frames.Contains(sprite.Frame))
                {
                    continue;
                }

                SpriteFrame frame = atlas.Frames[sprite.Frame];
                if (frame.Width <= 0 || frame.Height <= 0)
                {
                    continue;
                }

                (double x, double y) = camera.ToScreen(sprite.X, sprite.Y);
                canvas.DrawImage(
                    atlas.Image,
                    SKRect.Create(frame.X, frame.Y, frame.Width, frame.Height),
                    SKRect.Create((float)(x + frame.PivotX), (float)(y + frame.PivotY), frame.Width, frame.Height),
                    Sampling);
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
