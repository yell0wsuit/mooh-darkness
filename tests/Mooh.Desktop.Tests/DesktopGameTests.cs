using System;
using System.Collections.Generic;
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
        /// <summary>The host updates the completion walk and waits three seconds before moving on.</summary>
        [Fact]
        public void AWinPlaysTheDoorSequenceBeforeMovingOn()
        {
            DesktopGame game = Create();
            game.Update(new PlayerInputState(false, false, false, false, true), [], 0.016);
            game.Update(default, [], 0.016);
            for (int frame = 0; frame < 60; frame++)
            {
                game.Update(default, [], 0.05);
            }

            foreach (Core.Entities.Entity entity in game.Session.Level.World.Entities.ToArray())
            {
                if (entity.Interactions.HasFlag(Core.Interactions.InteractionMask.LevelEndLock))
                {
                    Assert.True(game.Session.Level.World.Remove(entity.Id));
                }
            }

            for (int frame = 0; frame < 100 && game.Session.State == GameSessionState.Playing; frame++)
            {
                game.Update(default, [], 0.05);
            }

            Assert.Equal(GameSessionState.LevelCompleted, game.Session.State);
            Assert.Equal(PlayerActionKind.LevelExit, game.Session.Level.World.Player!.PlayerBody!.Action!.Kind);
            for (int frame = 0; frame < 20; frame++)
            {
                game.Update(default, [], 0.05);
            }

            Assert.Equal(PlayerActionKind.LevelExited, game.Session.Level.World.Player!.PlayerBody!.Action!.Kind);
            Assert.DoesNotContain(game.Presenter.Sprites, sprite => sprite.Entity == game.Session.Level.World.Player!.Id);
            for (int frame = 0; frame < 30; frame++)
            {
                game.Update(default, [], 0.05);
            }

            Assert.Equal(0, game.Session.LevelIndex);
            Assert.Equal(GameSessionState.LevelCompleted, game.Session.State);
            for (int frame = 0; frame < 12; frame++)
            {
                game.Update(default, [], 0.05);
            }

            Assert.Equal(1, game.Session.LevelIndex);
            Assert.Equal(GameSessionState.Playing, game.Session.State);
        }

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

            game.Update(new PlayerInputState(false, false, false, false, true), [], 0.016);
            game.Update(default, [], 0.016);
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
            game.Update(new PlayerInputState(false, false, false, false, true), [], 0.016);
            game.Update(default, [], 0.016);
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

        /// <summary>Verifies the fly-in holds gameplay and the level title until the view is built.</summary>
        [Fact]
        public void TheFlyInHoldsGameplayAndTheTitle()
        {
            DesktopGame game = Create();
            double remaining = game.Session.Level.World.Player!.PlayerBody!.Action!.RemainingSeconds;

            for (int frame = 0; frame < 6; frame++)
            {
                game.Update(default, [], 0.1);
            }

            Assert.Equal(remaining, game.Session.Level.World.Player!.PlayerBody!.Action!.RemainingSeconds);
            Assert.DoesNotContain(game.Presenter.Sprites, sprite => sprite.Entity == game.Session.Level.World.Player!.Id);
            Assert.DoesNotContain(game.Hud.Texts, text => text.Font == HudPresenter.LevelFontName);
        }

        /// <summary>Verifies a skip waits for release and does not consume ammunition or advance gameplay.</summary>
        [Fact]
        public void ReleasingTheSkipKeyFinishesTheFlyIn()
        {
            DesktopGame game = Create();
            PlayerInputState fire = new(false, false, false, false, true);
            double remaining = game.Session.Level.World.Player!.PlayerBody!.Action!.RemainingSeconds;

            game.Update(fire, [], 0.1);
            Assert.DoesNotContain(game.Presenter.Sprites, sprite => sprite.Entity == game.Session.Level.World.Player!.Id);
            game.Update(default, [], 0.1);

            Assert.False(game.IsBuilding);
            Assert.Equal(remaining, game.Session.Level.World.Player!.PlayerBody!.Action!.RemainingSeconds);
            game.Update(default, [], 0.1);
            Assert.True(game.Session.Level.World.Player!.PlayerBody!.Action!.RemainingSeconds < remaining);
        }

        /// <summary>Verifies paused bonus timers expire and thaw monsters without advancing the world.</summary>
        [Fact]
        public void PausingStillExpiresTheIceBonus()
        {
            List<LevelObject> objects =
            [
                new(1, "OPlayer", 0, new LevelPosition(1, 1), [], new Dictionary<string, System.Text.Json.JsonElement>()),
                new(2, "BIce", 0, new LevelPosition(2, 1), [], new Dictionary<string, System.Text.Json.JsonElement>()),
                new(3, "MGrL", 0, new LevelPosition(6, 1), [], new Dictionary<string, System.Text.Json.JsonElement>()),
                new(4, "CellsMetal01", 0, new LevelPosition(4, 1), [], new Dictionary<string, System.Text.Json.JsonElement>()),
            ];
            for (int x = 0; x < 8; x++)
            {
                objects.Add(new LevelObject(100 + x, "CellsMetal01", 0, new LevelPosition(x, 2), [], new Dictionary<string, System.Text.Json.JsonElement>()));
            }

            DesktopGame game = Create(new LevelDocument(1, null, new LevelSize(8, 4), new EditorPreview(0, 0, 8, 4), objects));
            game.Update(new PlayerInputState(false, false, false, false, true), [], 0.016);
            game.Update(default, [], 0.016);
            for (int frame = 0; frame < 20; frame++)
            {
                game.Update(default, [], 0.1);
            }

            for (int frame = 0; frame < 6; frame++)
            {
                game.Update(new PlayerInputState(false, false, true, false), [], 0.1);
            }

            Core.Entities.Entity player = game.Session.Level.World.Player!;
            Core.Entities.Entity monster = game.Session.Level.GetEntity(3);
            Assert.True(monster.GreenMonsterBody!.IsFrozen);
            game.Update(default, [ControlCommand.TogglePause], 0.016);
            double? motionProgress = player.Motion?.Progress;
            double elapsed = game.ElapsedSeconds;
            double health = player.HealthBody!.Current;
            TimeSpan bonus = player.PlayerBody!.Progress.IceRemaining;
            game.Update(default, [], 0.1);
            Assert.InRange(player.PlayerBody.Progress.IceRemaining,
                bonus - TimeSpan.FromMilliseconds(101), bonus - TimeSpan.FromMilliseconds(99));

            bool heardOff = false;
            for (int frame = 0; frame < 460; frame++)
            {
                game.Update(default, [], 0.1);
                heardOff |= game.Sounds.Any(cue => cue.Alias == "IceOn");
            }

            Assert.Equal(TimeSpan.Zero, player.PlayerBody.Progress.IceRemaining);
            Assert.False(monster.GreenMonsterBody.IsFrozen);
            Assert.True(heardOff);
            Assert.Equal(motionProgress, player.Motion?.Progress);
            Assert.Equal(elapsed, game.ElapsedSeconds);
            Assert.Equal(health, player.HealthBody.Current);
        }

        /// <summary>Verifies pausing mid-spin preserves the drawn world transform.</summary>
        [Fact]
        public void PausingPreservesTheWorldSpin()
        {
            List<LevelObject> objects =
            [
                new(1, "OPlayer", 0, new LevelPosition(1, 1), [], new Dictionary<string, System.Text.Json.JsonElement>()),
                new(2, "BIce", 0, new LevelPosition(2, 1), [], new Dictionary<string, System.Text.Json.JsonElement>()),
                new(3, "OWRotL", 0, new LevelPosition(3, 1), [], new Dictionary<string, System.Text.Json.JsonElement>()),
            ];
            for (int x = 0; x < 8; x++)
            {
                objects.Add(new LevelObject(100 + x, "CellsMetal01", 0, new LevelPosition(x, 2), [], new Dictionary<string, System.Text.Json.JsonElement>()));
            }

            DesktopGame game = Create(new LevelDocument(1, null, new LevelSize(8, 4), new EditorPreview(0, 0, 8, 4), objects));
            PlayerInputState fire = new(false, false, false, false, true);
            game.Update(fire, [], 0.016);
            game.Update(default, [], 0.016);
            for (int frame = 0; frame < 20; frame++)
            {
                game.Update(default, [], 0.1);
            }

            for (int frame = 0; frame < 6; frame++)
            {
                game.Update(new PlayerInputState(false, false, true, false), [], 0.1);
            }

            for (int frame = 0; frame < 100 && game.Session.Simulation.WorldRotationInFlight is null; frame++)
            {
                game.Update(fire, [], 0.02);
            }

            _ = Assert.NotNull(game.Session.Simulation.WorldRotationInFlight);
            game.Update(default, [], 0.2);
            Core.Entities.Entity floor = game.Session.Level.GetEntity(100);
            Presentation.Scene.SceneSprite before = Assert.Single(game.Presenter.Sprites, sprite => sprite.Entity == floor.Id);

            game.Update(default, [ControlCommand.TogglePause], 0);

            Assert.Equal(before, Assert.Single(game.Presenter.Sprites, sprite => sprite.Entity == floor.Id));
        }

        private static DesktopGame Create(LevelDocument? level = null)
        {
            string assets = Path.Combine(AppContext.BaseDirectory, "assets");
            LoadedLevelPack pack = JsonLevelPackLoader.Load(Path.Combine(assets, "levels", "manifest.json"));
            if (level is not null)
            {
                pack = pack with
                {
                    Levels = [level]
                };
            }

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
