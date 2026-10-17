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
    /// Draws the screen panels, the level texts and the help panel with the bundled Comic Neue TrueType
    /// fonts.
    /// </summary>
    /// <param name="fontsDirectory">The folder holding the Comic Neue TrueType fonts.</param>
    internal sealed class HudRenderer(string fontsDirectory) : IDisposable
    {
        private readonly Dictionary<string, GameFont> _fonts = [with(StringComparer.Ordinal)];
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
        }

        /// <summary>Releases the font atlases and the paint.</summary>
        public void Dispose()
        {
            foreach (GameFont font in _fonts.Values)
            {
                font.Dispose();
            }

            _fonts.Clear();
            _fade.Dispose();
        }

        internal void DrawText(SKCanvas canvas, HudText text, double opacity = 1)
        {
            GameFont font = FontOf(text.Font);
            byte alpha = (byte)Math.Round(255 * Math.Clamp(opacity, 0, 1));
            float x = (float)font.AlignedX(text.Text, text.X, text.Alignment);
            float y = (float)text.Y + font.BaselineOffset;
            _fade.IsAntialias = true;
            _fade.Style = SKPaintStyle.Stroke;
            _fade.StrokeWidth = 2;
            _fade.Color = SKColors.Black.WithAlpha(alpha);
            canvas.DrawText(text.Text, x, y, SKTextAlign.Left, font.Font, _fade);
            _fade.Style = SKPaintStyle.Fill;
            _fade.Color = font.Color.WithAlpha(alpha);
            canvas.DrawText(text.Text, x, y, SKTextAlign.Left, font.Font, _fade);
        }

        private GameFont FontOf(string name)
        {
            if (_fonts.TryGetValue(name, out GameFont? loaded))
            {
                return loaded;
            }
            using FileStream stream = File.OpenRead(Path.Combine(fontsDirectory, GameFont.FileName(name)));
            GameFont font = GameFont.Load(name, stream);
            _fonts[name] = font;
            return font;
        }
    }
}
