using System;
using System.Globalization;

using Mooh.Core.Player;
using Mooh.Core.Session;

using SkiaSharp;

namespace Mooh.Desktop
{
    /// <summary>
    /// Draws a plain status line until the original's panels and bitmap fonts are drawn: the level,
    /// score, health or time, the current weapon and its ammunition, and the session state.
    /// </summary>
    internal sealed class HudRenderer : IDisposable
    {
        private readonly SKFont _font = new(SKTypeface.Default, 16);
        private readonly SKPaint _text = new() { Color = SKColors.White, IsAntialias = true };
        private readonly SKPaint _shade = new() { Color = new SKColor(0, 0, 0, 160) };

        /// <summary>Draws the status line.</summary>
        /// <param name="canvas">The canvas, in the 800 by 600 screen space.</param>
        /// <param name="game">The game.</param>
        public void Draw(SKCanvas canvas, DesktopGame game)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(game);
            GameSession session = game.Session;
            PlayerBody? body = session.Level.World.Player?.PlayerBody;
            double? clock = session.Level.World.Player?.HealthBody?.Clock?.Remaining.TotalSeconds;
            string vitality = clock is double seconds
                ? string.Create(CultureInfo.InvariantCulture, $"Time {Math.Ceiling(seconds):0}")
                : string.Create(CultureInfo.InvariantCulture, $"Health {session.Level.World.Player?.HealthBody?.Current ?? 0:0}");
            string weapon = body is null
                ? string.Empty
                : string.Create(CultureInfo.InvariantCulture, $"{body.Progress.CurrentWeapon} {body.Progress.AmmoOf(body.Progress.CurrentWeapon)}");
            string state = game.IsPaused ? "  PAUSED" : session.State switch
            {
                GameSessionState.Playing => string.Empty,
                GameSessionState.LevelCompleted => "  LEVEL COMPLETE",
                GameSessionState.LevelFailed => "  LEVEL LOST",
                GameSessionState.CampaignCompleted => "  CAMPAIGN COMPLETE",
                _ => string.Empty,
            };
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"Level {session.LevelIndex + 1}/{session.LevelCount}   Score {session.TotalScore}   {vitality}   {weapon}{state}");
            canvas.DrawRect(SKRect.Create(0, 0, 800, 24), _shade);
            canvas.DrawText(line, 8, 17, SKTextAlign.Left, _font, _text);
        }

        /// <summary>Releases the font and paints.</summary>
        public void Dispose()
        {
            _font.Dispose();
            _text.Dispose();
            _shade.Dispose();
        }
    }
}
