using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Simulation;
using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Runs a small simulated level under a scene harness and a screen-panel presenter.</summary>
    internal sealed class HudRun
    {
        private static readonly Lazy<StringTable> Strings = new(() =>
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "text", "strings.json"));
            return StringTable.Load(stream);
        });

        private static readonly Lazy<GameFont> LevelFont = new(() =>
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "fonts", GameFont.FileName(HudPresenter.LevelFontName)));
            return GameFont.Load(HudPresenter.LevelFontName, stream);
        });

        public HudRun(LevelDocument level, DifficultySpeeds? speeds = null)
        {
            Harness = new SceneHarness(level, speeds);
            Hud = new HudPresenter(Harness.Level.World, Strings.Value, new Random(1), LevelFont.Value);
        }

        public SceneHarness Harness
        {
            get;
        }

        public HudPresenter Hud
        {
            get;
        }

        public int Score
        {
            get; set;
        }

        public List<SimulationEvent> Events { get; } = [];

        /// <summary>Gets or sets the camera the monster arrows are placed by, if any.</summary>
        public Scene.SceneCamera? Camera
        {
            get; set;
        }

        /// <summary>Gets the falling characters of the level texts, in draw order.</summary>
        public IReadOnlyList<HudText> LevelCharacters => [.. Hud.Texts.Where(text => text.Font == HudPresenter.LevelFontName)];

        public void Step(double seconds)
        {
            Harness.Step(seconds);
            Hud.Update(Harness.Level.World, [.. Harness.LastEvents, .. Events], Score, seconds, Camera);
            Events.Clear();
        }

        public void StepFor(double seconds, double step = 0.016)
        {
            for (double elapsed = 0; elapsed < seconds - 1e-9; elapsed += step)
            {
                Step(Math.Min(step, seconds - elapsed));
            }
        }

        public void StepUntil(Func<bool> condition)
        {
            for (double elapsed = 0; !condition(); elapsed += 0.016)
            {
                Assert.True(elapsed < 5, "The condition never held.");
                Step(0.016);
            }
        }

        public HudSprite Sprite(string frame)
        {
            return Hud.Sprites.Single(sprite => sprite.Frame == frame);
        }
    }
}
