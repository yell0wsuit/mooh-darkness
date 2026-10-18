using System;
using System.Runtime.InteropServices;

using SDL3;

using SkiaSharp;

namespace Mooh.Desktop.Platform.Graphics
{
    /// <summary>
    /// A core OpenGL 3.2 context from the system driver with a Skia surface over the window's
    /// default framebuffer, after contre-jour-dx's <c>SdlGlDevice</c>.
    /// </summary>
    public sealed class SdlGlDevice : SdlGraphicsDevice
    {
        private const uint GlFramebufferBinding = 0x8CA6;
        private const uint GlRgba8 = 0x8058;
        private const uint GlVendor = 0x1F00;
        private const uint GlRenderer = 0x1F01;
        private const uint GlVersion = 0x1F02;
        private const int CoreProfileMask = 1;

        private nint _glContext;
        private uint _framebuffer;

        /// <inheritdoc/>
        public override GraphicsBackendKind Kind => GraphicsBackendKind.OpenGL;

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">The context could not be made.</exception>
        public override void Initialize()
        {
            CheckThread();
            SDL.GLResetAttributes();
            Check(SDL.GLSetAttribute(SDL.GLAttr.ContextMajorVersion, 3));
            Check(SDL.GLSetAttribute(SDL.GLAttr.ContextMinorVersion, 2));
            Check(SDL.GLSetAttribute(SDL.GLAttr.ContextProfileMask, CoreProfileMask));
            Check(SDL.GLSetAttribute(SDL.GLAttr.DoubleBuffer, 1));
            Check(SDL.GLSetAttribute(SDL.GLAttr.RedSize, 8));
            Check(SDL.GLSetAttribute(SDL.GLAttr.GreenSize, 8));
            Check(SDL.GLSetAttribute(SDL.GLAttr.BlueSize, 8));
            Check(SDL.GLSetAttribute(SDL.GLAttr.AlphaSize, 8));
            Check(SDL.GLSetAttribute(SDL.GLAttr.StencilSize, 8));
            CreateWindow(SDL.WindowFlags.OpenGL);
            _glContext = SDL.GLCreateContext(Window);
            if (_glContext == 0)
            {
                throw new InvalidOperationException($"SDL could not create a GL 3.2 context: {SDL.GetError()}");
            }

            nint glContext = _glContext;
            Own(() => SDL.GLDestroyContext(glContext));
            Check(SDL.GLMakeCurrent(Window, _glContext));

            // The window's own framebuffer is not always 0 (macOS layers it), so ask rather than assume.
            _framebuffer = (uint)GetInteger(GlFramebufferBinding);
            _ = SDL.GLSetSwapInterval(1);
            GRGlInterface glInterface = Own(GRGlInterface.Create(SDL.GLGetProcAddress)
                ?? throw new InvalidOperationException("Skia could not resolve the GL functions."));
            Context = Own(GRContext.CreateGl(glInterface)
                ?? throw new InvalidOperationException("Skia could not create a GL context."));
            Adapter = $"{GetString(GlRenderer)}, {GetString(GlVersion)} ({GetString(GlVendor)})";
        }

        /// <inheritdoc/>
        public override bool AcquireFrame()
        {
            if (!GetDrawableSize(out int width, out int height))
            {
                return false;
            }

            Check(SDL.GLMakeCurrent(Window, _glContext));
            if (!HasFrame || width != Width || height != Height)
            {
                CreateSurface(width, height);
            }

            return true;
        }

        /// <inheritdoc/>
        /// <exception cref="GraphicsDeviceLostException">The swap failed.</exception>
        public override void Present()
        {
            CheckThread();
            if (!SDL.GLSwapWindow(Window))
            {
                throw new GraphicsDeviceLostException($"SDL could not swap the GL window: {SDL.GetError()}");
            }
        }

        private static unsafe int GetInteger(uint name)
        {
            nint getIntegerv = SDL.GLGetProcAddress("glGetIntegerv");
            if (getIntegerv == 0)
            {
                throw new InvalidOperationException("glGetIntegerv is unavailable.");
            }

            int value = 0;
            ((delegate* unmanaged[Cdecl]<uint, int*, void>)getIntegerv)(name, &value);
            return value;
        }

        private static unsafe string GetString(uint name)
        {
            nint getString = SDL.GLGetProcAddress("glGetString");
            if (getString == 0)
            {
                return "unknown";
            }

            byte* text = ((delegate* unmanaged[Cdecl]<uint, byte*>)getString)(name);
            return Marshal.PtrToStringUTF8((nint)text) ?? "unknown";
        }

        private void CreateSurface(int width, int height)
        {
            Flush();
            ClearSurface();
            Check(SDL.GLGetAttribute(SDL.GLAttr.StencilSize, out int stencil));
            Check(SDL.GLGetAttribute(SDL.GLAttr.MultisampleSamples, out int samples));
            Context!.ResetContext();
            SetSurface(
                new GRBackendRenderTarget(width, height, samples, stencil, new GRGlFramebufferInfo(_framebuffer, GlRgba8)),
                GRSurfaceOrigin.BottomLeft,
                SKColorType.Rgba8888);
            Width = width;
            Height = height;
        }
    }
}
