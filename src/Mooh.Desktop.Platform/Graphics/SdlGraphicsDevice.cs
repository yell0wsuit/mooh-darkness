using System;
using System.Threading;

using SDL3;

using SkiaSharp;

namespace Mooh.Desktop.Platform.Graphics
{
    /// <summary>
    /// An SDL window and the Skia surface a frame is drawn into, after contre-jour-dx's
    /// <c>SdlGraphicsDevice</c>. Each device owns its window, since SDL fixes a window's graphics API
    /// when it is created. Everything acquired is registered right after it succeeds, so a failure
    /// part-way through <see cref="Initialize"/> releases exactly what was acquired. Main thread only.
    /// </summary>
    public abstract class SdlGraphicsDevice : IDisposable
    {
        /// <summary>The window title.</summary>
        public const string WindowTitle = "Mooh";

        /// <summary>The original's screen width, <c>DDraw.ulScrSizeX</c>.</summary>
        public const int LogicalWidth = 800;

        /// <summary>The original's screen height, <c>DDraw.ulScrSizeY</c>.</summary>
        public const int LogicalHeight = 600;

        private readonly int _ownerThread = Environment.CurrentManagedThreadId;
        private readonly CandidateLifetime _resources = new();
        private SKSurface? _surface;
        private GRBackendRenderTarget? _target;
        private bool _disposed;

        /// <summary>Gets the renderer.</summary>
        public abstract GraphicsBackendKind Kind
        {
            get;
        }

        /// <summary>Gets the SDL window.</summary>
        public nint Window
        {
            get; private set;
        }

        /// <summary>Gets the GPU context, or <see langword="null"/> for a device that draws on the CPU.</summary>
        public GRContext? Context
        {
            get; protected set;
        }

        /// <summary>Gets a description of the adapter, for the console.</summary>
        public string Adapter { get; protected set; } = "unknown";

        /// <summary>Gets the acquired frame's canvas.</summary>
        /// <exception cref="InvalidOperationException">No frame is acquired.</exception>
        public SKCanvas Canvas => _surface?.Canvas ?? throw new InvalidOperationException("No frame is acquired.");

        /// <summary>Gets the surface width in pixels.</summary>
        public int Width
        {
            get; protected set;
        }

        /// <summary>Gets the surface height in pixels.</summary>
        public int Height
        {
            get; protected set;
        }

        /// <summary>Gets whether a surface exists.</summary>
        protected bool HasFrame => _surface is not null;

        /// <summary>Creates the hidden window and everything the renderer needs to draw into it.</summary>
        public abstract void Initialize();

        /// <summary>Prepares the next frame's surface.</summary>
        /// <returns><see langword="false"/> while the window is minimized or has no area.</returns>
        public abstract bool AcquireFrame();

        /// <summary>Shows the drawn frame.</summary>
        public abstract void Present();

        /// <summary>
        /// Submits the frame and waits for it. Skia abandons a context when the driver reports the
        /// device gone, and every later draw would be dropped silently.
        /// </summary>
        /// <exception cref="GraphicsDeviceLostException">The context was abandoned.</exception>
        public void Flush()
        {
            CheckThread();
            Context?.Flush(submit: true, synchronous: true);
            if (Context?.IsAbandoned == true)
            {
                throw new GraphicsDeviceLostException("Skia abandoned the graphics context.");
            }
        }

        /// <summary>Reads the acquired frame's pixels back.</summary>
        /// <returns>The pixels; the caller disposes them.</returns>
        /// <exception cref="InvalidOperationException">No frame is acquired, or the read failed.</exception>
        public SKBitmap ReadPixels()
        {
            CheckThread();
            if (_surface is null)
            {
                throw new InvalidOperationException("No frame is acquired.");
            }

            SKBitmap bitmap = new(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (!_surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
            {
                bitmap.Dispose();
                throw new InvalidOperationException("The frame could not be read back.");
            }

            return bitmap;
        }

        /// <summary>Releases the surface, then everything the device acquired.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Releases the device.</summary>
        /// <param name="disposing">Whether this is a <see cref="Dispose()"/> call.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed || !disposing)
            {
                return;
            }

            CheckThread();
            _disposed = true;
            try
            {
                ClearSurface();
            }
            finally
            {
                _resources.Dispose();
            }
        }

        /// <summary>Registers a resource to release with the device.</summary>
        /// <typeparam name="T">The resource type.</typeparam>
        /// <param name="resource">The resource.</param>
        /// <returns><paramref name="resource"/>.</returns>
        protected T Own<T>(T resource)
            where T : IDisposable
        {
            return _resources.Own(resource);
        }

        /// <summary>Registers a native release to run with the device's.</summary>
        /// <param name="release">The release.</param>
        protected void Own(Action release)
        {
            _ = _resources.Own(new NativeRelease(release));
        }

        /// <summary>
        /// Creates the device's hidden window. Candidates are built and thrown away until one passes
        /// its draw check, and the host shows the survivor.
        /// </summary>
        /// <param name="flags">The renderer's window flags.</param>
        /// <exception cref="InvalidOperationException">SDL could not create the window.</exception>
        protected void CreateWindow(SDL.WindowFlags flags)
        {
            CheckThread();
            Window = SDL.CreateWindow(
                WindowTitle,
                1280,
                720,
                flags | SDL.WindowFlags.Resizable | SDL.WindowFlags.HighPixelDensity | SDL.WindowFlags.Hidden);
            if (Window == 0)
            {
                throw new InvalidOperationException($"SDL could not create the window: {SDL.GetError()}");
            }

            Own(() =>
            {
                SDL.DestroyWindow(Window);
                Window = 0;
            });
        }

        /// <summary>Checks the call is on the owning main thread of a live device.</summary>
        /// <exception cref="InvalidOperationException">The call is on another thread.</exception>
        protected void CheckThread()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Environment.CurrentManagedThreadId != _ownerThread || !SDL.IsMainThread())
            {
                throw new InvalidOperationException("SDL graphics must run on the main thread.");
            }
        }

        /// <summary>Reads the window's size in pixels.</summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns><see langword="false"/> when there is nothing to draw into.</returns>
        protected bool GetDrawableSize(out int width, out int height)
        {
            CheckThread();
            Check(SDL.GetWindowSizeInPixels(Window, out width, out height));
            return width > 0 && height > 0 && (SDL.GetWindowFlags(Window) & SDL.WindowFlags.Minimized) == 0;
        }

        /// <summary>Wraps a GPU render target. The target is kept even when Skia refuses it, so it is still released.</summary>
        /// <param name="target">The render target.</param>
        /// <param name="origin">The target's origin.</param>
        /// <param name="colorType">The target's pixel format.</param>
        /// <exception cref="InvalidOperationException">Skia refused the target.</exception>
        protected void SetSurface(GRBackendRenderTarget target, GRSurfaceOrigin origin, SKColorType colorType)
        {
            ClearSurface();
            _target = target;
            _surface = SKSurface.Create(Context, _target, origin, colorType)
                ?? throw new InvalidOperationException("Skia could not wrap the window's render target.");
        }

        /// <summary>Uses a CPU raster surface.</summary>
        /// <param name="raster">The surface.</param>
        /// <exception cref="InvalidOperationException">The surface could not be made.</exception>
        protected void SetRasterSurface(SKSurface? raster)
        {
            ClearSurface();
            _surface = raster ?? throw new InvalidOperationException("Skia could not create a raster surface.");
        }

        /// <summary>Releases the surface before its backing target.</summary>
        protected void ClearSurface()
        {
            _surface?.Dispose();
            _surface = null;
            _target?.Dispose();
            _target = null;
        }

        /// <summary>Turns an SDL failure into an exception carrying its error.</summary>
        /// <param name="success">The SDL call's result.</param>
        /// <exception cref="InvalidOperationException">The call failed.</exception>
        protected static void Check(bool success)
        {
            if (!success)
            {
                throw new InvalidOperationException(SDL.GetError());
            }
        }

        private sealed class NativeRelease(Action release) : IDisposable
        {
            private Action? _pending = release;

            public void Dispose()
            {
                Interlocked.Exchange(ref _pending, null)?.Invoke();
            }
        }
    }
}
