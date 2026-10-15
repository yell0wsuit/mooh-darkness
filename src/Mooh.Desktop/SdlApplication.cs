using System;
using System.Diagnostics;
using System.IO;

using Mooh.Core.Player;
using Mooh.Desktop.Platform.Graphics;
using Mooh.Desktop.Platform.Input;

using SDL3;

using SkiaSharp;

namespace Mooh.Desktop
{
    /// <summary>
    /// Runs the game in an SDL window drawn by Skia on the first renderer that passes a draw check,
    /// following contre-jour-dx's <c>SdlApplication</c>: events, an update with the real elapsed
    /// time, and a draw per frame, paced by vsync.
    /// </summary>
    /// <param name="options">The command line.</param>
    /// <param name="game">The game to run.</param>
    /// <param name="scene">The scene renderer.</param>
    /// <param name="sounds">The sound board the game's cues play on.</param>
    /// <param name="music">The music, kept fed every frame, or <see langword="null"/> when silent.</param>
    /// <param name="hud">The panel renderer.</param>
    internal sealed class SdlApplication(DesktopOptions options, DesktopGame game, SceneRenderer scene, SoundBoard sounds, MusicPlayer? music, HudRenderer hud) : IDisposable
    {
        /// <summary>The level's backdrop until the original's parallax layers are drawn.</summary>
        private static readonly SKColor Backdrop = new(24, 30, 46);

        private readonly KeyboardControls _controls = new();
        private readonly Stopwatch _clock = new();
        private GraphicsSelection<SdlGraphicsDevice>? _selection;
        private bool _sdlStarted;
        private bool _quit;
        private int _frames;

        /// <summary>Opens the window and runs until it is closed or the frame limit is reached.</summary>
        /// <exception cref="InvalidOperationException">SDL could not start.</exception>
        /// <exception cref="AggregateException">No renderer worked.</exception>
        public void Run()
        {
            if (!SDL.InitSubSystem(SDL.InitFlags.Video))
            {
                throw new InvalidOperationException($"SDL could not start: {SDL.GetError()}");
            }

            _sdlStarted = true;
            _selection = BackendSelector.Attempt(
                BackendSelector.PreferenceOrder(BackendSelector.CurrentPlatform(), options.Renderer),
                CreateDevice,
                ValidateDevice);
            SdlGraphicsDevice device = _selection.Device;
            foreach (RendererFailure failure in _selection.Failures)
            {
                Console.Error.WriteLine($"Renderer {failure.Kind} passed over: {failure.Failure.Message}");
            }

            Console.WriteLine($"Renderer {device.Kind}: {device.Adapter}");
            _ = SDL.SetWindowTitle(device.Window, $"{SdlGraphicsDevice.WindowTitle} | {device.Kind}");
            _ = SDL.SetWindowMinimumSize(device.Window, SdlGraphicsDevice.LogicalWidth / 2, SdlGraphicsDevice.LogicalHeight / 2);
            _ = SDL.ShowWindow(device.Window);
            _clock.Start();
            TimeSpan previous = _clock.Elapsed;
            while (!_quit)
            {
                PumpEvents();
                TimeSpan now = _clock.Elapsed;
                PlayerInputState input = options.Script is null ? _controls.PlayerInput : options.ScriptedInput(game.ElapsedSeconds);
                game.Update(input, _controls.TakeCommands(), (now - previous).TotalSeconds, _controls.AnyKeyHeld);
                sounds.Play(game.Sounds, game.Camera);
                music?.Pump();
                previous = now;
                DrawFrame(device);
            }
        }

        /// <summary>Releases the atlases' window and SDL.</summary>
        public void Dispose()
        {
            _selection?.Dispose();
            if (_sdlStarted)
            {
                SDL.QuitSubSystem(SDL.InitFlags.Video);
            }
        }

        private static SdlGraphicsDevice CreateDevice(GraphicsBackendKind kind, CandidateLifetime lifetime)
        {
            SdlGraphicsDevice device = kind switch
            {
                GraphicsBackendKind.Metal => new MetalDevice(),
                GraphicsBackendKind.OpenGL => new SdlGlDevice(),
                GraphicsBackendKind.Software => new SdlSoftwareDevice(),
                _ => throw new PlatformNotSupportedException($"The {kind} renderer is not available."),
            };
            _ = lifetime.Own(device);
            device.Initialize();
            return device;
        }

        /// <summary>
        /// Requires a candidate to draw a shaded frame that reads back: a driver that fails to build
        /// a program draws nothing while Skia reports success.
        /// </summary>
        private static void ValidateDevice(SdlGraphicsDevice device)
        {
            if (!device.AcquireFrame())
            {
                throw new InvalidOperationException("The window has nothing to draw into.");
            }

            DrawCheck.Draw(device.Canvas, device.Width, device.Height);
            device.Flush();
            SKPointI at = DrawCheck.Sample(device.Width, device.Height);
            using (SKBitmap frame = device.ReadPixels())
            {
                SKColor sample = frame.GetPixel(at.X, at.Y);
                if (!DrawCheck.Drew(sample))
                {
                    throw new InvalidOperationException($"The renderer accepted a shaded draw but read back {sample}.");
                }
            }

            device.Canvas.Clear(DrawCheck.Background);
            device.Flush();
            device.Present();
        }

        private void PumpEvents()
        {
            while (SDL.PollEvent(out SDL.Event e))
            {
                // Deliberately not a switch: the populate-switch fixer would expand one over every
                // event type.
                SDL.EventType type = (SDL.EventType)e.Type;
                if (type is SDL.EventType.Quit or SDL.EventType.WindowCloseRequested)
                {
                    _quit = true;
                }
                else if (type == SDL.EventType.KeyDown)
                {
                    _controls.KeyDown(e.Key.Key, e.Key.Repeat);
                }
                else if (type == SDL.EventType.KeyUp)
                {
                    _controls.KeyUp(e.Key.Key);
                }
                else if (type == SDL.EventType.WindowFocusLost)
                {
                    _controls.ReleaseAll();
                }
            }
        }

        private void DrawFrame(SdlGraphicsDevice device)
        {
            if (!device.AcquireFrame())
            {
                return;
            }

            SKCanvas canvas = device.Canvas;
            canvas.ResetMatrix();
            canvas.Clear(SKColors.Black);
            float scale = Math.Min(device.Width / (float)SdlGraphicsDevice.LogicalWidth, device.Height / (float)SdlGraphicsDevice.LogicalHeight);
            int saved = canvas.Save();
            canvas.Translate(
                (device.Width - (SdlGraphicsDevice.LogicalWidth * scale)) / 2,
                (device.Height - (SdlGraphicsDevice.LogicalHeight * scale)) / 2);
            canvas.Scale(scale);
            canvas.ClipRect(SKRect.Create(0, 0, SdlGraphicsDevice.LogicalWidth, SdlGraphicsDevice.LogicalHeight));
            canvas.Clear(Backdrop);
            scene.DrawBackdrop(canvas, game.Backdrop, game.Camera);
            scene.Draw(canvas, game.Presenter.Sprites, game.Camera);
            hud.Draw(canvas, game, scene);
            canvas.RestoreToCount(saved);
            device.Flush();
            _frames++;
            if ((options.QuitAfterFrames is int limit && _frames >= limit)
                || (options.QuitAfterSeconds is double seconds && game.ElapsedSeconds >= seconds))
            {
                SaveScreenshot(device);
                _quit = true;
            }

            device.Present();
        }

        private void SaveScreenshot(SdlGraphicsDevice device)
        {
            if (options.ScreenshotPath is not string path)
            {
                return;
            }

            using SKBitmap frame = device.ReadPixels();
            using SKData png = frame.Encode(SKEncodedImageFormat.Png, 100);
            using FileStream stream = File.Create(path);
            png.SaveTo(stream);
            Console.WriteLine($"Saved {path}");
        }
    }
}
