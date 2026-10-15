using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Core.Content;
using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using SkiaSharp;

namespace Mooh.Desktop
{
    /// <summary>
    /// Draws the screen panels and the help panel with the recovered bitmap fonts, and the line that
    /// announces a decided level.
    /// </summary>
    /// <remarks>
    /// The outcome line stands still at the original's level-text place, <c>TxtTypeLvl</c>'s
    /// (400, 250) in <c>PanelDigScr</c>, until its slide-in is ported; the pause dims the screen.
    /// </remarks>
    /// <param name="fontsDirectory">The folder holding the <c>.fnt</c> tables and their atlases.</param>
    /// <param name="strings">The recovered text table, if loaded.</param>
    internal sealed class HudRenderer(string fontsDirectory, StringTable? strings) : IDisposable
    {
        /// <summary>The font of the original's level texts, <c>PgFontDigScr</c>.</summary>
        private const string LevelTextFont = "PanelDigScr";

        private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.None);
        private readonly Dictionary<string, (BitmapFont Font, SKImage Atlas)> _fonts = [with(StringComparer.Ordinal)];
        private readonly SKPaint _shade = new() { Color = new SKColor(0, 0, 0, 128) };

        /// <summary>Draws the panels, their values, the help and the outcome.</summary>
        /// <param name="canvas">The canvas, in the 800 by 600 screen space.</param>
        /// <param name="game">The game.</param>
        /// <param name="scene">The renderer holding the sprite atlases.</param>
        public void Draw(SKCanvas canvas, DesktopGame game, SceneRenderer scene)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(scene);
            foreach (HudSprite sprite in game.Hud.Sprites)
            {
                scene.DrawScreenFrame(canvas, HudPresenter.PageName, sprite.Frame, sprite.X, sprite.Y);
            }

            foreach (HudText text in game.Hud.Texts)
            {
                DrawText(canvas, HudPresenter.FontName, text);
            }

            if (game.OutcomeTextId is string id && strings is not null && strings.TryGetText(id, out string line))
            {
                DrawText(canvas, LevelTextFont, new HudText(line, 400, 250, TextAlignment.Center));
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
        }

        private void DrawText(SKCanvas canvas, string fontName, HudText text)
        {
            (BitmapFont font, SKImage atlas) = FontOf(fontName);
            foreach (GlyphPlacement glyph in font.Layout(text.Text, text.X, text.Y, text.Alignment))
            {
                canvas.DrawImage(
                    atlas,
                    SKRect.Create(glyph.SourceX, glyph.SourceY, glyph.Width, glyph.Height),
                    SKRect.Create((float)glyph.X, (float)glyph.Y, glyph.Width, glyph.Height),
                    Sampling);
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
