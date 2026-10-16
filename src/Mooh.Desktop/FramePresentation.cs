using System;

using SkiaSharp;

namespace Mooh.Desktop
{
    /// <summary>Composes the centered legacy screen and the expanded world view.</summary>
    internal sealed class FramePresentation : IDisposable
    {
        private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.None);
        private readonly SKImageFilter _blur = SKImageFilter.CreateBlur(18, 18, SKShaderTileMode.Clamp);
        private readonly SKPaint _blurPaint;
        private SKSurface? _frame;
        private SKImageInfo _info;

        public FramePresentation()
        {
            _blurPaint = new SKPaint { ImageFilter = _blur };
        }

        /// <summary>Gets additional world space on each side, capped at 16:9.</summary>
        public static float HorizontalMargin(int width, int height)
        {
            return (WorldWidth(width, height) - 960) / 2.4f;
        }

        private static int WorldWidth(int width, int height)
        {
            return (int)Math.Round(Math.Clamp(width * 720.0 / height, 960, 1280));
        }

        /// <summary>Draws the native world view or a reusable legacy frame with optional blurred borders.</summary>
        public void Draw(SKCanvas canvas, int width, int height, bool world, bool fullColor, Action<SKCanvas, float> draw)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (world && fullColor)
            {
                // Keep gameplay on the selected device at native resolution.
                float scale = Math.Min(width / (float)WorldWidth(width, height), height / 720f) * 1.2f;
                int worldSaved = canvas.Save();
                canvas.ResetMatrix();
                canvas.Clear(SKColors.Black);
                canvas.Translate((width - (800 * scale)) / 2, (height - (600 * scale)) / 2);
                canvas.Scale(scale);
                float worldMargin = HorizontalMargin(width, height);
                canvas.ClipRect(SKRect.Create(-worldMargin, 0, 800 + (2 * worldMargin), 600));
                draw(canvas, worldMargin);
                canvas.RestoreToCount(worldSaved);
                return;
            }

            SKImageInfo info = new(world ? WorldWidth(width, height) : 800, world ? 720 : 600,
                fullColor ? SKColorType.Rgba8888 : SKColorType.Rgb565, SKAlphaType.Opaque);
            if (_frame is null || _info != info)
            {
                _frame?.Dispose();
                _frame = SKSurface.Create(info) ?? throw new InvalidOperationException("Cannot create the presentation surface.");
                _info = info;
            }

            SKCanvas target = _frame.Canvas;
            target.ResetMatrix();
            target.Clear(SKColors.Black);
            int frameSaved = target.Save();
            float margin = world ? HorizontalMargin(width, height) : 0;
            if (world)
            {
                target.Scale(1.2f);
                target.Translate(margin, 0);
            }

            draw(target, margin);
            target.RestoreToCount(frameSaved);
            using SKImage image = _frame.Snapshot();
            int saved = canvas.Save();
            canvas.ResetMatrix();
            canvas.Clear(SKColors.Black);
            if (!world && (long)width * 600 != (long)height * 800)
            {
                // Clamp the source edges before blurring, so the backdrop remains opaque.
                float cover = Math.Max(width / 800f, height / 600f);
                _ = canvas.Save();
                canvas.Translate((width - (800 * cover)) / 2, (height - (600 * cover)) / 2);
                canvas.Scale(cover);
                canvas.DrawImage(image, 0, 0, Sampling, _blurPaint);
                canvas.Restore();
            }

            float fit = Math.Min(width / (float)info.Width, height / (float)info.Height);
            SKRect destination = SKRect.Create((width - (info.Width * fit)) / 2,
                (height - (info.Height * fit)) / 2, info.Width * fit, info.Height * fit);
            canvas.DrawImage(image, destination, Sampling);
            canvas.RestoreToCount(saved);
        }

        public void Dispose()
        {
            _frame?.Dispose();
            _blurPaint.Dispose();
            _blur.Dispose();
        }
    }
}
