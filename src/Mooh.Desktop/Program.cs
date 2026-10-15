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
            using (FileStream stream = File.OpenRead(Path.Combine(assets, "text", "strings.bin")))
            {
                strings = StringTable.Load(stream);
            }

            BitmapFont levelFont;
            using (FileStream stream = File.OpenRead(Path.Combine(assets, "fonts", HudPresenter.LevelFontName + ".fnt")))
            {
                levelFont = BitmapFont.Load(HudPresenter.LevelFontName, stream);
            }

            using SpriteMasks masks = new(Path.Combine(assets, "sprites"));
            DesktopGame game = new(new GameSession(pack, options.Difficulty, options.LevelIndex), library, new Random(), strings, levelFont, masks);
            try
            {
                // A machine without a playback device still plays the game, silently.
                using IAudioOutput audio = (IAudioOutput?)SdlAudio.TryOpen(out string? error) ?? new SilentAudio();
                if (error is not null)
                {
                    Console.Error.WriteLine($"Audio is off: {error}");
                }

                SoundBoard sounds = new(catalog, audio, Path.Combine(assets, "sounds"), new Random());
                using MusicPlayer? music = MusicPlayer.Start(audio, Path.Combine(assets, "music"), "game");
                using SceneRenderer scene = new(Path.Combine(assets, "sprites"));
                using HudRenderer hud = new(Path.Combine(assets, "fonts"));
                using SdlApplication application = new(options, game, scene, sounds, music, hud);
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
