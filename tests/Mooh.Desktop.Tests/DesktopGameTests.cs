using System;
using System.IO;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Player;
using Mooh.Core.Session;
using Mooh.Core.Simulation;
using Mooh.Desktop.Platform.Input;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Verifies the desktop game drives the session, the presenter and the camera.</summary>
    public sealed class DesktopGameTests
    {
        /// <summary>Verifies time moves the level and the presenter lists its sprites.</summary>
        [Fact]
        public void TimeMovesTheLevelAndItsSprites()
        {
            DesktopGame game = Create();

            game.Update(default, [], 0.5);

            Assert.NotEmpty(game.Presenter.Sprites);
            Assert.True(game.Session.Level.World.Entities.Any());
        }

        /// <summary>Verifies the screen panels follow the level from its first frame.</summary>
        [Fact]
        public void ThePanelsFollowTheLevel()
        {
            DesktopGame game = Create();

            game.Update(default, [], 0.016);

            Assert.Contains(game.Hud.Sprites, sprite => sprite.Frame == "PNrmHealth");
        }

        /// <summary>Verifies the level's title drops in as the level starts.</summary>
        [Fact]
        public void TheTitleDropsInAsTheLevelStarts()
        {
            DesktopGame game = Create();

            for (int frame = 0; frame < 60; frame++)
            {
                game.Update(default, [], 0.016);
            }

            Assert.Equal("Level1", string.Concat(game.Hud.Texts.Where(text => text.Font == HudPresenter.LevelFontName).Select(text => text.Text)));
        }

        /// <summary>Verifies the pause stops the level's time.</summary>
        [Fact]
        public void ThePauseStopsTheLevel()
        {
            DesktopGame game = Create();
            game.Update(default, [ControlCommand.TogglePause], 0.016);
            PlayerActionKind? entering = game.Session.Level.World.Player!.PlayerBody!.Action?.Kind;

            game.Update(default, [], 5);

            Assert.True(game.IsPaused);
            Assert.Equal(entering, game.Session.Level.World.Player!.PlayerBody!.Action?.Kind);
            Assert.Equal(PlayerActionKind.LevelEntry, entering);
        }

        /// <summary>Verifies a long frame is clamped, so a stall does not leap the level forward.</summary>
        [Fact]
        public void ALongFrameIsClamped()
        {
            DesktopGame game = Create();

            game.Update(default, [], 10);

            Assert.Equal(PlayerActionKind.LevelEntry, game.Session.Level.World.Player!.PlayerBody!.Action?.Kind);
        }

        /// <summary>Verifies a lost level is replayed after the outcome has shown.</summary>
        [Fact]
        public void ALostLevelIsReplayed()
        {
            DesktopGame game = Create();
            for (int frame = 0; frame < 50; frame++)
            {
                game.Update(default, [], 0.05);
            }

            _ = game.Session.Simulation.ApplyDamage(new Core.Combat.DamageRequest(
                game.Session.Level.World.Player!.Id,
                game.Session.Level.World.Player!.Id,
                Core.Combat.DamageKind.Fire,
                1000));

            for (int frame = 0; frame < 600 && game.Session.State == GameSessionState.Playing; frame++)
            {
                game.Update(default, [], 0.05);
            }

            Assert.Equal(GameSessionState.LevelFailed, game.Session.State);
            Assert.Equal("Sorry,youlost:(", string.Concat(game.Hud.Texts.Where(text => text.Font == HudPresenter.LevelFontName).Select(text => text.Text)));
            for (int frame = 0; frame < 100 && game.Session.State != GameSessionState.Playing; frame++)
            {
                game.Update(default, [], 0.05);
            }

            Assert.Equal(GameSessionState.Playing, game.Session.State);
            Assert.Equal(PlayerActionKind.LevelEntry, game.Session.Level.World.Player!.PlayerBody!.Action?.Kind);
        }

        private static DesktopGame Create()
        {
            string assets = Path.Combine(AppContext.BaseDirectory, "assets");
            LoadedLevelPack pack = JsonLevelPackLoader.Load(Path.Combine(assets, "levels", "manifest.json"));
            using FileStream stream = File.OpenRead(Path.Combine(assets, "animations", "animations.json"));
            using FileStream strings = File.OpenRead(Path.Combine(assets, "text", "strings.bin"));
            using FileStream font = File.OpenRead(Path.Combine(assets, "fonts", HudPresenter.LevelFontName + ".fnt"));
            return new DesktopGame(
                new GameSession(pack, DifficultySpeeds.Standard),
                AnimationLibrary.Load(stream),
                new Random(1),
                StringTable.Load(strings),
                BitmapFont.Load(HudPresenter.LevelFontName, font));
        }
    }
}
