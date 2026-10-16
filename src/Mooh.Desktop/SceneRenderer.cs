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
        private readonly SKPaint _backdropPaint = new();
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
            if (_backdropImage is null)
            {
                _backdropImage = SKImage.FromEncodedData(Path.Combine(_spritesDirectory, "..", "screens", Backdrop.ImageName + ".jpg"));
                if (_backdropImage is not null)
                {
                    using SKShader shader = _backdropImage.ToShader(SKShaderTileMode.Mirror, SKShaderTileMode.Clamp, Sampling);
                    _backdropPaint.Shader = shader;
                }
            }
            float margin = (float)camera.HorizontalMargin;
            if (_backdropImage is not null)
            {
                // Keep source pixels at their original scale, mirroring only beyond the image's edges.
                int saved = canvas.Save();
                canvas.Translate(layout.ImageX, layout.ImageY);
                canvas.DrawRect(SKRect.Create(-margin - layout.ImageX, -layout.ImageY, 800 + (2 * margin), 600), _backdropPaint);
                canvas.RestoreToCount(saved);
            }

            double firstColumn = layout.Sprites.Count > 0 ? layout.Sprites[0].X : 0;
            double lastColumn = firstColumn;
            foreach (BackdropSprite sprite in layout.Sprites)
            {
                if (sprite.Page != Backdrop.LampPage)
                {
                    lastColumn = Math.Max(lastColumn, sprite.X);
                }
            }
            foreach (BackdropSprite sprite in layout.Sprites)
            {
                DrawFrame(canvas, sprite.Page, sprite.Frame, sprite.X, sprite.Y, sprite.Additive ? _additive : null);
                if (margin > 0 && sprite.Page != Backdrop.LampPage)
                {
                    // One additional 250-pixel column on each side covers the 16:9 overscan.
                    if (sprite.X == firstColumn)
                    {
                        DrawFrame(canvas, sprite.Page, sprite.Frame, sprite.X - 250, sprite.Y, null);
                    }
                    if (sprite.X == lastColumn)
                    {
                        DrawFrame(canvas, sprite.Page, sprite.Frame, sprite.X + 250, sprite.Y, null);
                    }
                }
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
                DrawFrame(canvas, sprite.Page, sprite.Frame, x, y, sprite.Additive ? _additive : null);
            }
        }

        /// <summary>Draws one frame with its anchor at a screen point, as the screen panels do.</summary>
        /// <param name="canvas">The canvas, set up in the original's 800 by 600 screen space.</param>
        /// <param name="page">The sprite page.</param>
        /// <param name="frame">The frame name.</param>
        /// <param name="x">The anchor's screen x.</param>
        /// <param name="y">The anchor's screen y.</param>
        public void DrawScreenFrame(SKCanvas canvas, string page, string frame, double x, double y)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            DrawFrame(canvas, page, frame, x, y, null);
        }

        /// <summary>Repeats the menu shade texture across the original logical screen.</summary>
        internal void DrawScreenTile(SKCanvas canvas, string page, string name, float horizontalMargin = 0)
        {
            SpriteFrame frame = AtlasOf(page).Frames[name];
            for (int y = -frame.Height; y < 600; y += frame.Height)
            {
                for (int x = -frame.Width - (int)Math.Ceiling(horizontalMargin); x < 800 + horizontalMargin; x += frame.Width)
                {
                    DrawFrame(canvas, page, name, x - frame.PivotX, y - frame.PivotY, null);
                }
            }
        }

        /// <summary>Releases every loaded atlas image.</summary>
        public void Dispose()
        {
            foreach (Atlas atlas in _atlases.Values)
            {
                foreach (SKImage frame in atlas.Images.Values)
                {
                    frame.Dispose();
                }

                atlas.Image.Dispose();
            }

            _atlases.Clear();
            _backdropImage?.Dispose();
            _additive.Dispose();
            _backdropPaint.Dispose();
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

            if (!atlas.Images.TryGetValue(name, out SKImage? image))
            {
                // A standalone frame clamps linear filtering to its edge pixels. Sampling an
                // atlas rectangle instead can fade its edges or bleed neighboring frames.
                image = atlas.Image.Subset(SKRectI.Create(frame.X, frame.Y, frame.Width, frame.Height));
                atlas.Images[name] = image;
            }

            canvas.DrawImage(
                image,
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
            if (!File.Exists(image))
            {
                image = Path.Combine(_spritesDirectory, "..", "screens", page + ".jpg");
            }
            using FileStream stream = File.OpenRead(table);
            SpritePage frames = SpritePage.Load(page, stream);
            SKImage pixels = SKImage.FromEncodedData(image)
                ?? throw new InvalidDataException($"The atlas '{image}' could not be decoded.");
            atlas = new Atlas(pixels, frames);
            _atlases[page] = atlas;
            return atlas;
        }

        private sealed record Atlas(SKImage Image, SpritePage Frames)
        {
            public Dictionary<string, SKImage> Images { get; } = [with(StringComparer.Ordinal)];
        }
    }
}
