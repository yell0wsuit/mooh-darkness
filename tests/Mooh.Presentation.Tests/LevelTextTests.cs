using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Levels;
using Mooh.Core.Simulation;
using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the level texts of <c>TxtAdd</c>: each character drops in from above in turn,
    /// jiggles at its place, and falls away after its hold.
    /// </summary>
    public sealed class LevelTextTests
    {
        private static double TitleStartX()
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "fonts", GameFont.FileName(HudPresenter.LevelFontName)));
            using GameFont font = GameFont.Load(HudPresenter.LevelFontName, stream);
            (int left, _, int right, _) = font.Bounds("Level 3");
            return Math.Floor(400 - ((right - left) / 2.0));
        }

        /// <summary>
        /// Verifies the title's first character appears at once at y −20, at the start of a line
        /// centered using the bundled Comic Neue glyph bounds.
        /// </summary>
        [Fact]
        public void TheTitleStartsAboveTheScreen()
        {
            HudRun run = new(SceneHarness.BuildLevel(1));

            run.Hud.ShowLevelTitle(3);
            run.Step(0.016);

            Assert.Equal([new HudText("L", TitleStartX(), -20, TextAlignment.Left, HudPresenter.LevelFontName)], run.LevelCharacters);
        }

        /// <summary>
        /// Verifies every character with a glyph has dropped in within a second and jiggles within
        /// 4 px of the line at y 250.
        /// </summary>
        [Fact]
        public void TheTitleSettlesOnItsLine()
        {
            HudRun run = new(SceneHarness.BuildLevel(1));

            run.Hud.ShowLevelTitle(3);
            run.StepFor(1);

            IReadOnlyList<HudText> characters = run.LevelCharacters;
            Assert.Equal("Level3", string.Concat(characters.Select(character => character.Text)));
            Assert.All(characters, character => Assert.InRange(character.Y, 245, 254));
            Assert.InRange(characters[0].X, TitleStartX() - 5, TitleStartX() + 4);
        }

        /// <summary>Verifies the title falls off the screen once its two seconds are up.</summary>
        [Fact]
        public void TheTitleFallsAwayAfterItsHold()
        {
            HudRun run = new(SceneHarness.BuildLevel(1));

            run.Hud.ShowLevelTitle(3);
            run.StepFor(3.5);

            Assert.Empty(run.LevelCharacters);
        }

        /// <summary>
        /// Verifies an announced win drops in its line, <c>GameScrWiner</c>, and sends the title
        /// away at once.
        /// </summary>
        [Fact]
        public void AnAnnouncedWinDropsInItsLine()
        {
            HudRun run = new(SceneHarness.BuildLevel(1));
            run.Hud.ShowLevelTitle(3);
            run.StepFor(1);

            run.Events.Add(new LevelOutcomeAnnounced(run.Harness.Player.Id, LevelOutcome.Won, TimedOut: false));
            run.StepFor(1.5);

            Assert.Equal("Youmadeit:)", string.Concat(run.LevelCharacters.Select(character => character.Text)));
        }

        /// <summary>Verifies a timeout drops in <c>GameScrTimeOut</c>.</summary>
        [Fact]
        public void AnAnnouncedTimeoutDropsInItsLine()
        {
            HudRun run = new(SceneHarness.BuildLevel(1));

            run.Events.Add(new LevelOutcomeAnnounced(run.Harness.Player.Id, LevelOutcome.Lost, TimedOut: true));
            run.StepFor(1);

            Assert.Equal("Timeout:(", string.Concat(run.LevelCharacters.Select(character => character.Text)));
        }

        /// <summary>
        /// Verifies treasure destroyed other than by eating drops in <c>GameScrLostTreas</c>, as
        /// <c>Game.ObjKill</c> does when it forfeits the bonus.
        /// </summary>
        [Fact]
        public void LostTreasureDropsInItsLine()
        {
            HudRun run = new(SceneHarness.BuildLevel(1, ("OCoin01", 5, 1), ("WBomb", 4, 1)));
            run.Step(0.016);
            Assert.True(run.Harness.Player.PlayerBody!.Progress.IsTreasureBonusAvailable);

            run.Harness.Detonate(run.Harness.Level.World.Entities.Single(entity => entity.ArchivalIdentity?.Kind == EntityKind.Grenade));
            run.StepFor(1.5);

            Assert.Equal("Treasurelost:(", string.Concat(run.LevelCharacters.Select(character => character.Text)));
        }
    }
}
