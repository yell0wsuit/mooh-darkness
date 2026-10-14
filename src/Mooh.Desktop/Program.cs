using System;
using System.IO;

using Mooh.Core.Content;
using Mooh.Core.Session;
using Mooh.Presentation.Animation;

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

            DesktopGame game = new(new GameSession(pack, options.Difficulty, options.LevelIndex), library, new Random());
            try
            {
                using SceneRenderer scene = new(Path.Combine(assets, "sprites"));
                using SdlApplication application = new(options, game, scene);
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
