using System;
using System.Runtime.InteropServices;

using SDL3;

using SkiaSharp;

namespace Mooh.Desktop.Platform.Graphics
{
    /// <summary>
    /// SDL's Metal view and its <c>CAMetalLayer</c>, drawn by Skia on Metal, after contre-jour-dx's
    /// <c>MetalDevice</c>. Each frame takes the layer's next drawable inside its own autorelease pool
    /// and presents it through one command buffer, one frame in flight.
    /// </summary>
    public sealed partial class MetalDevice : SdlGraphicsDevice
    {
        private const int PixelFormatBgra8Unorm = 80;
        private const int CommandBufferStatusCompleted = 4;
        private const int CommandBufferStatusError = 5;

        private nint _layer;
        private nint _queue;
        private nint _drawable;
        private nint _pool;

        /// <inheritdoc/>
        public override GraphicsBackendKind Kind => GraphicsBackendKind.Metal;

        /// <inheritdoc/>
        /// <exception cref="PlatformNotSupportedException">The platform is not macOS.</exception>
        /// <exception cref="InvalidOperationException">A Metal object could not be made.</exception>
        public override void Initialize()
        {
            CheckThread();
            if (!OperatingSystem.IsMacOS())
            {
                throw new PlatformNotSupportedException("Metal requires macOS.");
            }

            nint initPool = ObjC.Send(ObjC.Send(ObjC.Class("NSAutoreleasePool"), "alloc"), "init");
            Own(() => ObjC.Send(initPool, "drain"));
            CreateWindow(SDL.WindowFlags.Metal);
            nint view = SDL.MetalCreateView(Window);
            if (view == 0)
            {
                throw new InvalidOperationException($"SDL could not create a Metal view: {SDL.GetError()}");
            }

            Own(() => SDL.MetalDestroyView(view));
            _layer = SDL.MetalGetLayer(view);
            if (_layer == 0)
            {
                throw new InvalidOperationException("SDL did not create a CAMetalLayer.");
            }

            nint device = ObjC.MTLCreateSystemDefaultDevice();
            if (device == 0)
            {
                throw new InvalidOperationException("No Metal device.");
            }

            Own(() => ObjC.Send(device, "release"));
            Adapter = Marshal.PtrToStringUTF8(ObjC.Send(ObjC.Send(device, "name"), "UTF8String")) ?? "Metal";
            _ = ObjC.Send(_layer, "setDevice:", device);
            _ = ObjC.Send(_layer, "setPixelFormat:", PixelFormatBgra8Unorm);

            // Readable, so the startup draw check can read the frame back.
            _ = ObjC.Send(_layer, "setFramebufferOnly:", 0);
            _queue = ObjC.Send(device, "newCommandQueue");
            if (_queue == 0)
            {
                throw new InvalidOperationException("No Metal command queue.");
            }

            nint queue = _queue;
            Own(() => ObjC.Send(queue, "release"));
            Context = Own(GRContext.CreateMetal(new GRMtlBackendContext { DeviceHandle = device, QueueHandle = _queue })
                ?? throw new InvalidOperationException("Skia could not create a Metal context."));
            Own(ReleaseDrawable);
        }

        /// <inheritdoc/>
        public override bool AcquireFrame()
        {
            if (!GetDrawableSize(out int width, out int height))
            {
                return false;
            }

            ClearSurface();
            ReleaseDrawable();
            _pool = ObjC.Send(ObjC.Send(ObjC.Class("NSAutoreleasePool"), "alloc"), "init");
            Width = width;
            Height = height;
            ObjC.SetSize(_layer, ObjC.Selector("setDrawableSize:"), new ObjC.Size(width, height));
            _drawable = ObjC.Send(_layer, "nextDrawable");
            if (_drawable == 0)
            {
                ReleaseDrawable();
                return false;
            }

            nint texture = ObjC.Send(_drawable, "texture");
            SetSurface(new GRBackendRenderTarget(width, height, new GRMtlTextureInfo(texture)), GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
            return true;
        }

        /// <inheritdoc/>
        /// <exception cref="GraphicsDeviceLostException">The presentation command failed.</exception>
        public override void Present()
        {
            CheckThread();
            if (_drawable == 0)
            {
                throw new InvalidOperationException("No Metal drawable to present.");
            }

            nint command = ObjC.Send(_queue, "commandBuffer");
            if (command == 0)
            {
                throw new InvalidOperationException("Metal could not create a command buffer.");
            }

            _ = ObjC.Send(command, "presentDrawable:", _drawable);
            _ = ObjC.Send(command, "commit");
            _ = ObjC.Send(command, "waitUntilCompleted");
            nint status = ObjC.Send(command, "status");
            if (status == CommandBufferStatusError)
            {
                throw new GraphicsDeviceLostException("The Metal presentation command failed.");
            }

            if (status != CommandBufferStatusCompleted)
            {
                throw new InvalidOperationException($"Metal presentation ended in command buffer status {status}.");
            }

            ClearSurface();
            ReleaseDrawable();
        }

        private void ReleaseDrawable()
        {
            _drawable = 0;
            if (_pool != 0)
            {
                _ = ObjC.Send(_pool, "drain");
                _pool = 0;
            }
        }

        /// <summary>The Objective-C runtime and Metal entry points the device uses.</summary>
        private static partial class ObjC
        {
            [LibraryImport("/System/Library/Frameworks/Metal.framework/Metal")]
            internal static partial nint MTLCreateSystemDefaultDevice();

            [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
            internal static partial nint Class(string name);

            [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
            internal static partial nint Selector(string name);

            /// <summary>Passes a <c>CGSize</c>, which the pointer-sized overloads cannot.</summary>
            [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            internal static partial void SetSize(nint receiver, nint selector, Size size);

            internal static nint Send(nint receiver, string selector)
            {
                return Send0(receiver, Selector(selector));
            }

            internal static nint Send(nint receiver, string selector, nint value)
            {
                return Send1(receiver, Selector(selector), value);
            }

            [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            private static partial nint Send0(nint receiver, nint selector);

            [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
            private static partial nint Send1(nint receiver, nint selector, nint value);

            /// <summary>A <c>CGSize</c>.</summary>
            [StructLayout(LayoutKind.Sequential)]
            internal readonly struct Size(double width, double height)
            {
                private readonly double _width = width;
                private readonly double _height = height;
            }
        }
    }
}
