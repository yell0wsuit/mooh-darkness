using System;

using SkiaSharp;

namespace Mooh.Desktop.Platform.Graphics
{
    /// <summary>
    /// Proves a shaded draw reaches a device's render target. A driver that fails to link a program
    /// draws nothing while Skia reports success, so a clear alone cannot tell a working device from
    /// a silent one; a gradient forces a program to exist.
    /// </summary>
    public static class DrawCheck
    {
        /// <summary>Gets the color the check clears to.</summary>
        public static SKColor Background => SKColors.Black;

        /// <summary>Draws the check.</summary>
        /// <param name="canvas">The device's canvas.</param>
        /// <param name="width">The surface width.</param>
        /// <param name="height">The surface height.</param>
        public static void Draw(SKCanvas canvas, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            canvas.Clear(Background);
            using SKShader shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                [SKColors.Red, SKColors.Blue],
                null,
                SKShaderTileMode.Clamp);
            using SKPaint paint = new()
            {
                Shader = shader,
            };
            canvas.DrawRect(SKRect.Create(0, 0, width, height), paint);
        }

        /// <summary>Gets the pixel the check reads back.</summary>
        /// <param name="width">The surface width.</param>
        /// <param name="height">The surface height.</param>
        /// <returns>The surface's center.</returns>
        public static SKPointI Sample(int width, int height)
        {
            return new SKPointI(width / 2, height / 2);
        }

        /// <summary>Gets whether a read-back pixel shows the check was drawn.</summary>
        /// <param name="sample">The pixel.</param>
        /// <returns><see langword="true"/> when the gradient reached the target.</returns>
        public static bool Drew(SKColor sample)
        {
            return sample.Alpha != 0 && sample != Background;
        }
    }
}
