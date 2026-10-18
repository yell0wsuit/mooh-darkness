using System;

using SDL3;

using SkiaSharp;

namespace Mooh.Desktop.Platform.Graphics
{
    /// <summary>
    /// Skia drawing on the CPU into a bitmap, presented through SDL's software renderer: the
    /// fallback for machines without a usable GPU driver, after contre-jour-dx's
    /// <c>SdlSoftwareDevice</c>.
    /// </summary>
    public sealed class SdlSoftwareDevice : SdlGraphicsDevice
    {
        private nint _renderer;
        private nint _texture;
        private SKBitmap? _pixels;

        /// <inheritdoc/>
        public override GraphicsBackendKind Kind => GraphicsBackendKind.Software;

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">The renderer could not be made.</exception>
        public override void Initialize()
        {
            CheckThread();

            // Held for the device's lifetime: SDL reads it when the window's framebuffer is made.
            GlHintScope hints = Own(new GlHintScope());

            // Cocoa has no native window framebuffer; SDL must upload the CPU raster there.
            hints.Set(SDL.Hints.FramebufferAcceleration, OperatingSystem.IsMacOS() ? "1" : "0");
            CreateWindow(0);
            _renderer = SDL.CreateRenderer(Window, "software");
            if (_renderer == 0)
            {
                throw new InvalidOperationException($"SDL could not create a software renderer: {SDL.GetError()}");
            }

            nint renderer = _renderer;
            Own(() => SDL.DestroyRenderer(renderer));
            Own(ReleasePixels);
            Adapter = "Skia raster through the SDL software renderer";
        }

        /// <inheritdoc/>
        public override bool AcquireFrame()
        {
            if (!GetDrawableSize(out int width, out int height))
            {
                return false;
            }

            if (!HasFrame || width != Width || height != Height)
            {
                Resize(width, height);
            }

            return true;
        }

        /// <inheritdoc/>
        public override void Present()
        {
            CheckThread();
            SKBitmap pixels = _pixels ?? throw new InvalidOperationException("No frame is acquired.");
            Check(SDL.UpdateTexture(_texture, 0, pixels.GetPixels(), pixels.RowBytes));
            Check(SDL.RenderTexture(_renderer, _texture, 0, 0));
            Check(SDL.RenderPresent(_renderer));
        }

        private void Resize(int width, int height)
        {
            ClearSurface();
            ReleasePixels();
            _pixels = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            SetRasterSurface(SKSurface.Create(_pixels.Info, _pixels.GetPixels(), _pixels.RowBytes));

            // Desktop targets are little-endian, where ABGR8888 stores its bytes in RGBA order.
            _texture = SDL.CreateTexture(_renderer, SDL.PixelFormat.ABGR8888, SDL.TextureAccess.Streaming, width, height);
            if (_texture == 0)
            {
                throw new InvalidOperationException($"SDL could not create the frame texture: {SDL.GetError()}");
            }

            Check(SDL.SetTextureBlendMode(_texture, SDL.BlendMode.None));
            Width = width;
            Height = height;
        }

        private void ReleasePixels()
        {
            if (_texture != 0)
            {
                SDL.DestroyTexture(_texture);
                _texture = 0;
            }

            _pixels?.Dispose();
            _pixels = null;
        }
    }
}
