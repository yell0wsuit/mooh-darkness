using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

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
    /// <param name="shell">The game and screen flow to run.</param>
    /// <param name="scene">The scene renderer.</param>
    /// <param name="sounds">The sound board the game's cues play on.</param>
    /// <param name="music">The music, kept fed every frame, or <see langword="null"/> when silent.</param>
    /// <param name="hud">The panel renderer.</param>
    internal sealed class SdlApplication(DesktopOptions options, GameShell shell, SceneRenderer scene, SoundBoard sounds, MusicDirector music, HudRenderer hud) : IDisposable
    {
        /// <summary>The level's backdrop until the original's parallax layers are drawn.</summary>
        private static readonly SKColor Backdrop = new(24, 30, 46);

        private readonly KeyboardControls _controls = new();
        private readonly Stopwatch _clock = new();
        private GraphicsSelection<SdlGraphicsDevice>? _selection;
        private bool _sdlStarted;
        private bool _quit;
        private int _frames;
        private ScreenRenderer? _screens;
        private readonly FramePresentation _presentation = new();
        private bool? _fullscreen;
        private bool _textInput;

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
            _ = SDL.HideCursor();
            _screens = new ScreenRenderer(Path.Combine(AppContext.BaseDirectory, "assets", "screens"), scene, hud);
            _clock.Start();
            TimeSpan previous = _clock.Elapsed;
            while (!_quit)
            {
                PumpEvents();
                TimeSpan now = _clock.Elapsed;
                PlayerInputState input = options.Script is null ? _controls.PlayerInput : options.ScriptedInput(shell.Game?.ElapsedSeconds ?? 0);
                if (shell.Game is DesktopGame currentGame)
                {
                    _ = SDL.GetWindowSize(device.Window, out int viewWidth, out int viewHeight);
                    if (viewWidth > 0 && viewHeight > 0)
                    {
                        currentGame.Camera.HorizontalMargin = FramePresentation.HorizontalMargin(viewWidth, viewHeight);
                        currentGame.Camera.VerticalMargin = FramePresentation.VerticalMargin(viewWidth, viewHeight);
                    }
                }
                shell.Update(input, _controls.TakeCommands(), (now - previous).TotalSeconds, _controls.AnyKeyHeld);
                sounds.Volume = shell.Profile.Options.SoundVolume;
                sounds.Play(shell.Sounds, shell.Game?.Camera ?? new Presentation.Scene.SceneCamera());
                music.Update(shell.TrackName, shell.Profile.Options.MusicVolume);
                if (_fullscreen != shell.Profile.Options.Fullscreen)
                {
                    _fullscreen = shell.Profile.Options.Fullscreen;
                    _ = SDL.SetWindowFullscreen(device.Window, _fullscreen.Value);
                }
                bool textInput = shell.Screen == GameScreen.ScoreEntry;
                if (_textInput != textInput)
                {
                    _textInput = textInput;
                    _ = textInput ? SDL.StartTextInput(device.Window) : SDL.StopTextInput(device.Window);
                }
                _quit |= shell.ShouldQuit;
                previous = now;
                DrawFrame(device);
            }
        }

        /// <summary>Releases the atlases' window and SDL.</summary>
        public void Dispose()
        {
            shell.SaveProgress();
            _screens?.Dispose();
            _presentation.Dispose();
            _ = SDL.ShowCursor();
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
                GraphicsBackendKind.Vulkan => new VulkanDevice(),
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
                    GameScreen before = shell.Screen;
                    shell.HandleKey(e.Key.Key, !e.Key.Repeat || shell.Screen == GameScreen.ScoreEntry);
                    if (before != shell.Screen)
                    {
                        _controls.ReleaseAll();
                    }
                    else if (shell.Screen == GameScreen.Playing)
                    {
                        _controls.KeyDown(e.Key.Key, e.Key.Repeat);
                    }
                }
                else if (type == SDL.EventType.KeyUp)
                {
                    shell.HandleKey(e.Key.Key, false);
                    if (shell.Screen == GameScreen.Playing)
                    {
                        _controls.KeyUp(e.Key.Key);
                    }
                }
                else if (type == SDL.EventType.TextInput)
                {
                    shell.HandleText(Marshal.PtrToStringUTF8(e.Text.Text) ?? string.Empty);
                }
                else if (type == SDL.EventType.MouseMotion)
                {
                    _ = SDL.GetWindowSize(_selection!.Device.Window, out int width, out int height);
                    double scale = Math.Min(width / 800.0, height / 600.0);
                    shell.MouseMove((e.Motion.X - ((width - (800 * scale)) / 2)) / scale,
                        (e.Motion.Y - ((height - (600 * scale)) / 2)) / scale);
                }
                else if (type is SDL.EventType.MouseButtonDown or SDL.EventType.MouseButtonUp)
                {
                    GameScreen before = shell.Screen;
                    shell.MouseButton(type == SDL.EventType.MouseButtonDown, e.Button.Button == 3);
                    if (before != shell.Screen)
                    {
                        _controls.ReleaseAll();
                    }
                }
                else if (type == SDL.EventType.WindowFocusLost)
                {
                    _controls.ReleaseAll();
                    shell.CancelPointer();
                }
            }
        }

        private void DrawFrame(SdlGraphicsDevice device)
        {
            if (!device.AcquireFrame())
            {
                return;
            }

            _presentation.Draw(device.Canvas, device.Width, device.Height, shell.ShowsWorld,
                true, (target, margin) =>
                {
                    float verticalMargin = shell.ShowsWorld ? FramePresentation.VerticalMargin(device.Width, device.Height) : 0;
                    target.Clear(Backdrop);
                    if (shell.ShowsWorld && shell.Game is DesktopGame game)
                    {
                        game.Camera.HorizontalMargin = margin;
                        game.Camera.VerticalMargin = verticalMargin;
                        scene.DrawBackdrop(target, game.Backdrop, game.Camera);
                        scene.Draw(target, game.Presenter.Sprites, game.Camera);
                        hud.Draw(target, game, scene);
                    }
                    _screens!.Draw(target, shell.Elements, margin, verticalMargin);
                });
            device.Flush();
            _frames++;
            if ((options.QuitAfterFrames is int limit && _frames >= limit)
                || (options.QuitAfterSeconds is double seconds && shell.ElapsedSeconds >= seconds))
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
