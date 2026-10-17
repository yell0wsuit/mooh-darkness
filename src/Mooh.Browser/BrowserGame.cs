using System;
using System.IO;

using Mooh.Core.Content;
using Mooh.Core.Session;
using Mooh.Core.Simulation;
using Mooh.Desktop;
using Mooh.Desktop.Platform.Input;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Audio;
using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

namespace Mooh.Browser
{
    /// <summary>Runs the shared game shell with browser input, storage and audio.</summary>
    internal sealed class BrowserGame : IDisposable
    {
        private readonly GameShell _shell;
        private readonly GameFont _levelFont;
        private readonly KeyboardControls _controls = new();
        private readonly SceneRenderer _scene;
        private readonly HudRenderer _hud;
        private readonly ScreenRenderer _screens;
        private readonly FramePresentation _presentation = new();
        private readonly SoundBoard _sounds;
        private readonly SpriteMasks _masks;
        private readonly WebAudioOutput _audio;
        private double _previous;
        private bool _active = true;
        private int _cssWidth = 800;
        private int _cssHeight = 600;
        private string _reportedScreen = string.Empty;
        private bool _fullscreen;
        private bool _stopped;
        private bool _rightButton;

        public BrowserGame(string assets, WebAudioOutput audio)
        {
            _audio = audio;
            using FileStream animations = File.OpenRead(Path.Combine(assets, "animations", "animations.json"));
            using FileStream strings = File.OpenRead(Path.Combine(assets, "text", "strings.json"));
            using FileStream font = File.OpenRead(Path.Combine(assets, "fonts", GameFont.FileName(HudPresenter.LevelFontName)));
            using FileStream catalog = File.OpenRead(Path.Combine(assets, "sounds", "Sound.ini"));
            TextProfileStore store = new(() => StorageInterop.Read("MoohDarkness.player-profile.v1"),
                json => StorageInterop.Write("MoohDarkness.player-profile.v1", json));
            _masks = new SpriteMasks(Path.Combine(assets, "sprites"));
            _levelFont = GameFont.Load(HudPresenter.LevelFontName, font);
            _shell = new GameShell(assets, JsonLevelPackLoader.Load(Path.Combine(assets, "levels", "manifest.json")),
                AnimationLibrary.Load(animations), StringTable.Load(strings), _levelFont,
                store.Load(), store, _masks);
            foreach (string pair in PageInterop.Query().TrimStart('?').Split('&'))
            {
                string[] parts = pair.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }
                if (parts[0] == "level" && int.TryParse(parts[1], out int level) && level is >= 1 and <= 150)
                {
                    _shell.StartDirect(level - 1, DifficultySpeeds.Standard);
                }
                if (parts[0] == "screen")
                {
                    _shell.InspectScreen(Uri.UnescapeDataString(parts[1]));
                }
            }
            _scene = new SceneRenderer(Path.Combine(assets, "sprites"));
            _hud = new HudRenderer(Path.Combine(assets, "fonts"));
            _screens = new ScreenRenderer(Path.Combine(assets, "screens"), _scene, _hud);
            _sounds = new SoundBoard(SoundCatalog.Load(catalog), audio, Path.Combine(assets, "sounds"), new Random());
        }

        public void Resize(int width, int height)
        {
            _cssWidth = Math.Max(1, width);
            _cssHeight = Math.Max(1, height);
        }

        public void SetActive(bool active)
        {
            _active = active;
            _previous = 0;
            _controls.ReleaseAll();
            _shell.CancelPointer();
            _rightButton = false;
            AudioInterop.Suspended(!active);
            if (!active)
            {
                _shell.SaveProgress();
            }
        }

        public void Key(in HostEvent input)
        {
            if (!_active || !BrowserKeys.Values.TryGetValue(input.Word1, out SDL3.SDL.Keycode key))
            {
                return;
            }
            bool down = input.Word0 != 0;
            bool repeat = input.Word2 != 0;
            GameScreen before = _shell.Screen;
            _shell.HandleKey(key, down && (!repeat || _shell.Screen == GameScreen.ScoreEntry));
            if (before != _shell.Screen)
            {
                _controls.ReleaseAll();
            }
            else if (_shell.Screen == GameScreen.Playing)
            {
                if (down)
                {
                    _controls.KeyDown(key, repeat);
                }
                else
                {
                    _controls.KeyUp(key);
                }
            }
        }

        public void Text(int codePoint)
        {
            if (_active && codePoint is >= 32 and <= 0x10FFFF)
            {
                _shell.HandleText(char.ConvertFromUtf32(codePoint));
            }
        }

        public void Pointer(in HostEvent input)
        {
            if (!_active)
            {
                return;
            }
            double x = BitConverter.Int32BitsToSingle(input.Word2);
            double y = BitConverter.Int32BitsToSingle(input.Word3);
            double scale = Math.Min(_cssWidth / 800.0, _cssHeight / 600.0);
            _shell.MouseMove((x - ((_cssWidth - (800 * scale)) / 2)) / scale,
                (y - ((_cssHeight - (600 * scale)) / 2)) / scale);
            int phase = input.Word0 & 255;
            GameScreen before = _shell.Screen;
            if (phase == 0)
            {
                _rightButton = (input.Word4 & 2) != 0;
                _shell.MouseButton(true, _rightButton);
            }
            else if (phase == 2)
            {
                _shell.MouseButton(false, _rightButton);
                _rightButton = false;
            }
            else if (phase == 3)
            {
                _shell.CancelPointer();
                _rightButton = false;
            }
            if (before != _shell.Screen)
            {
                _controls.ReleaseAll();
            }
        }

        public void Frame(double timestamp, SkiaSurface surface)
        {
            if (_stopped)
            {
                return;
            }
            double seconds = _active && _previous > 0 ? Math.Clamp((timestamp - _previous) / 1000, 0, 0.1) : 0;
            _previous = timestamp;
            if (_shell.Game is DesktopGame current)
            {
                current.Camera.HorizontalMargin = FramePresentation.HorizontalMargin(surface.Width, surface.Height);
                current.Camera.VerticalMargin = FramePresentation.VerticalMargin(surface.Width, surface.Height);
            }
            if (_active)
            {
                _shell.Update(_controls.PlayerInput, _controls.TakeCommands(), seconds, _controls.AnyKeyHeld);
                _sounds.Volume = _shell.Profile.Options.SoundVolume;
                _sounds.Play(_shell.Sounds, _shell.Game?.Camera ?? new Presentation.Scene.SceneCamera());
                AudioInterop.Music(_shell.TrackName, _shell.Profile.Options.MusicVolume);
            }
            if (_shell.Profile.Options.Fullscreen != _fullscreen)
            {
                _fullscreen = _shell.Profile.Options.Fullscreen;
                PageInterop.SetFullscreen(_fullscreen);
            }
            string screen = _shell.Screen.ToString();
            if (screen != _reportedScreen)
            {
                _reportedScreen = screen;
                PageInterop.SetScreen(screen);
            }
            _presentation.Draw(surface.Canvas, surface.Width, surface.Height, _shell.ShowsWorld,
                true, (canvas, margin) =>
                {
                    float vertical = _shell.ShowsWorld ? FramePresentation.VerticalMargin(surface.Width, surface.Height) : 0;
                    if (_shell.ShowsWorld && _shell.Game is DesktopGame game)
                    {
                        game.Camera.HorizontalMargin = margin;
                        game.Camera.VerticalMargin = vertical;
                        _scene.DrawBackdrop(canvas, game.Backdrop, game.Camera);
                        _scene.Draw(canvas, game.Presenter.Sprites, game.Camera);
                        _hud.Draw(canvas, game, _scene);
                    }
                    _screens.Draw(canvas, _shell.Elements, margin, vertical);
                });
            surface.Flush();
            if (_shell.ShouldQuit)
            {
                SetActive(false);
                _stopped = true;
                PageInterop.Finished();
            }
        }

        public void Dispose()
        {
            SetActive(false);
            AudioInterop.Music(string.Empty, 0);
            _presentation.Dispose();
            _screens.Dispose();
            _hud.Dispose();
            _scene.Dispose();
            _masks.Dispose();
            _shell.Dispose();
            _levelFont.Dispose();
            _audio.Dispose();
        }
    }
}
