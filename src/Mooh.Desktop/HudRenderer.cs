using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Presentation.Hud;
using Mooh.Presentation.Scene;
using Mooh.Presentation.Text;

using SkiaSharp;

namespace Mooh.Desktop
{
    /// <summary>
    /// Draws the screen panels, the level texts and the help panel with the recovered bitmap
    /// fonts. The pause dims the screen.
    /// </summary>
    /// <param name="fontsDirectory">The folder holding the <c>.fnt</c> tables and their atlases.</param>
    internal sealed class HudRenderer(string fontsDirectory) : IDisposable
    {
        private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.None);
        private readonly Dictionary<string, (BitmapFont Font, SKImage Atlas)> _fonts = [with(StringComparer.Ordinal)];
        private readonly SKPaint _shade = new() { Color = new SKColor(0, 0, 0, 128) };
        private readonly SKPaint _fade = new();

        /// <summary>Draws the panels, their values, the help and the outcome.</summary>
        /// <param name="canvas">The canvas, in the 800 by 600 screen space.</param>
        /// <param name="game">The game.</param>
        /// <param name="scene">The renderer holding the sprite atlases.</param>
        public void Draw(SKCanvas canvas, DesktopGame game, SceneRenderer scene)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(scene);
            // The floating scores are overlay parts, drawn after the world and before the panels.
            foreach (SceneText floating in game.Presenter.Texts)
            {
                (double x, double y) = game.Camera.ToScreen(floating.X, floating.Y);
                DrawText(canvas, new HudText(floating.Text, x, y, TextAlignment.Center, floating.Font), floating.Opacity);
            }

            foreach (HudSprite sprite in game.Hud.Sprites)
            {
                scene.DrawScreenFrame(canvas, HudPresenter.PageName, sprite.Frame, sprite.X, sprite.Y);
            }

            foreach (HudText text in game.Hud.Texts)
            {
                DrawText(canvas, text);
            }

            if (game.IsPaused)
            {
                canvas.DrawRect(SKRect.Create(0, 0, 800, 600), _shade);
            }
        }

        /// <summary>Releases the font atlases and the paint.</summary>
        public void Dispose()
        {
            foreach ((BitmapFont _, SKImage atlas) in _fonts.Values)
            {
                atlas.Dispose();
            }

            _fonts.Clear();
            _shade.Dispose();
            _fade.Dispose();
        }

        private void DrawText(SKCanvas canvas, HudText text, double opacity = 1)
        {
            (BitmapFont font, SKImage atlas) = FontOf(text.Font);
            _fade.Color = SKColors.White.WithAlpha((byte)Math.Round(255 * Math.Clamp(opacity, 0, 1)));
            foreach (GlyphPlacement glyph in font.Layout(text.Text, text.X, text.Y, text.Alignment))
            {
                canvas.DrawImage(
                    atlas,
                    SKRect.Create(glyph.SourceX, glyph.SourceY, glyph.Width, glyph.Height),
                    SKRect.Create((float)glyph.X, (float)glyph.Y, glyph.Width, glyph.Height),
                    Sampling,
                    opacity < 1 ? _fade : null);
            }
        }

        private (BitmapFont Font, SKImage Atlas) FontOf(string name)
        {
            if (_fonts.TryGetValue(name, out (BitmapFont Font, SKImage Atlas) loaded))
            {
                return loaded;
            }

            BitmapFont font;
            using (FileStream stream = File.OpenRead(Path.Combine(fontsDirectory, name + ".fnt")))
            {
                font = BitmapFont.Load(name, stream);
            }

            string image = Path.Combine(fontsDirectory, name + ".png");
            SKImage atlas = SKImage.FromEncodedData(image)
                ?? throw new InvalidDataException($"The font atlas '{image}' could not be decoded.");
            _fonts[name] = (font, atlas);
            return (font, atlas);
        }
    }
}
