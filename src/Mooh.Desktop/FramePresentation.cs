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
        private GRRecordingContext? _frameContext;

        public FramePresentation()
        {
            _blurPaint = new SKPaint { ImageFilter = _blur };
        }

        /// <summary>Gets additional world space on each side at the original vertical scale.</summary>
        public static float HorizontalMargin(int width, int height)
        {
            return (float)((Math.Max(800, width * 600.0 / height) - 800) / 2);
        }

        /// <summary>Gets additional world space above and below at the original horizontal scale.</summary>
        public static float VerticalMargin(int width, int height)
        {
            return (float)((Math.Max(600, height * 800.0 / width) - 600) / 2);
        }

        private static int WorldHeight(int width, int height)
        {
            return (int)Math.Round(Math.Max(height * 960.0 / width, 720));
        }

        private static int WorldWidth(int width, int height)
        {
            return (int)Math.Round(Math.Max(width * 720.0 / height, 960));
        }

        /// <summary>Draws the native world view or a reusable legacy frame with optional blurred borders.</summary>
        public void Draw(SKCanvas canvas, int width, int height, bool world, bool fullColor, Action<SKCanvas, float> draw)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            bool expandedWorld = world && fullColor;
            bool directMenu = !world && fullColor && width == 800 && height == 600;
            if (expandedWorld || directMenu)
            {
                // Gameplay and native-size menus need no intermediate image or blurred borders.
                float scale = Math.Min(width / 800f, height / 600f);
                int worldSaved = canvas.Save();
                canvas.ResetMatrix();
                canvas.Clear(SKColors.Black);
                canvas.Translate((width - (800 * scale)) / 2, (height - (600 * scale)) / 2);
                canvas.Scale(scale);
                float worldMargin = world ? HorizontalMargin(width, height) : 0;
                float worldVerticalMargin = world ? VerticalMargin(width, height) : 0;
                canvas.ClipRect(SKRect.Create(-worldMargin, -worldVerticalMargin,
                    800 + (2 * worldMargin), 600 + (2 * worldVerticalMargin)));
                draw(canvas, worldMargin);
                canvas.RestoreToCount(worldSaved);
                return;
            }

            SKImageInfo info = new(world ? WorldWidth(width, height) : 800, world ? WorldHeight(width, height) : 600,
                fullColor ? SKColorType.Rgba8888 : SKColorType.Rgb565, SKAlphaType.Opaque);
            // Borrow the destination's context; its owner controls its lifetime. A GPU
            // render target avoids rasterizing and uploading the entire menu every frame.
            GRRecordingContext? context = fullColor ? canvas.Context : null;
            if (_frame is null || _info != info || _frameContext?.Handle != context?.Handle)
            {
                _frame?.Dispose();
                _frame = (context is null ? SKSurface.Create(info)
                    : SKSurface.Create(context, true, info) ?? SKSurface.Create(info))
                    ?? throw new InvalidOperationException("Cannot create the presentation surface.");
                _info = info;
                _frameContext = context;
            }

            SKCanvas target = _frame.Canvas;
            target.ResetMatrix();
            target.Clear(SKColors.Black);
            int frameSaved = target.Save();
            float margin = world ? HorizontalMargin(width, height) : 0;
            float verticalMargin = world ? VerticalMargin(width, height) : 0;
            if (world)
            {
                // Match the exact viewport ratio despite rounding the raster surface dimensions.
                target.Scale(info.Width / (800 + (2 * margin)), info.Height / (600 + (2 * verticalMargin)));
                target.Translate(margin, verticalMargin);
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

            float logicalWidth = world ? 800 + (2 * margin) : 800;
            float logicalHeight = world ? 600 + (2 * verticalMargin) : 600;
            float fit = Math.Min(width / logicalWidth, height / logicalHeight);
            SKRect destination = SKRect.Create((width - (logicalWidth * fit)) / 2,
                (height - (logicalHeight * fit)) / 2, logicalWidth * fit, logicalHeight * fit);
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
