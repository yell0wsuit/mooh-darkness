using System;
using System.IO;

using Mooh.Core.Content;
using Mooh.Core.Session;
using Mooh.Desktop.Platform.Audio;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Audio;
using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using SDL3;

namespace Mooh.Desktop
{
    /// <summary>Starts the desktop game.</summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            DesktopOptions options;
            try
            {
                options = DesktopOptions.Parse(args);
            }
            catch (ArgumentException failure)
            {
                Console.Error.WriteLine(failure.Message);
                return 2;
            }

            string assets = Path.Combine(AppContext.BaseDirectory, "assets");
            LoadedLevelPack pack = JsonLevelPackLoader.Load(Path.Combine(assets, "levels", "manifest.json"));
            if (options.LevelIndex >= pack.Levels.Count)
            {
                Console.Error.WriteLine($"The pack has {pack.Levels.Count} levels.");
                return 2;
            }

            AnimationLibrary library;
            using (FileStream stream = File.OpenRead(Path.Combine(assets, "animations", "animations.json")))
            {
                library = AnimationLibrary.Load(stream);
            }

            SoundCatalog catalog;
            using (FileStream stream = File.OpenRead(Path.Combine(assets, "sounds", "Sound.ini")))
            {
                catalog = SoundCatalog.Load(stream);
            }

            StringTable strings;
            using (FileStream stream = File.OpenRead(Path.Combine(assets, "text", "strings.json")))
            {
                strings = StringTable.Load(stream);
            }

            GameFont levelFont;
            using (FileStream stream = File.OpenRead(Path.Combine(assets, "fonts", GameFont.FileName(HudPresenter.LevelFontName))))
            {
                levelFont = GameFont.Load(HudPresenter.LevelFontName, stream);
            }

            using GameFont ownedLevelFont = levelFont;
            using SpriteMasks masks = new(Path.Combine(assets, "sprites"));
            string data = SaveDataLocation.DefaultDirectory();
            PlayerProfileStore? store = options.Screen is null ? new(data) : null;
            using GameShell shell = new(assets, pack, library, strings, levelFont, store?.Load() ?? new PlayerProfile(), store, masks);
            if (options.Screen is null && (options.DirectLaunch || options.Script is not null || options.QuitAfterSeconds is not null))
            {
                shell.StartDirect(options.LevelIndex, options.Difficulty);
            }
            if (options.Screen is string screen)
            {
                shell.InspectScreen(screen);
            }
            try
            {
                // A machine without a playback device still plays the game, silently.
                using IAudioOutput audio = (IAudioOutput?)SdlAudio.TryOpen(out string? error) ?? new SilentAudio();
                if (error is not null)
                {
                    Console.Error.WriteLine($"Audio is off: {error}");
                }

                SoundBoard sounds = new(catalog, audio, Path.Combine(assets, "sounds"), new Random());
                using MusicDirector music = new(audio, Path.Combine(assets, "music"));
                using SceneRenderer scene = new(Path.Combine(assets, "sprites"));
                using HudRenderer hud = new(Path.Combine(assets, "fonts"));
                using SdlApplication application = new(options, shell, scene, sounds, music, hud);
                application.Run();
                return 0;
            }
            finally
            {
                SDL.Quit();
            }
        }
    }
}
