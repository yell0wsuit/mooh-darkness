using System;
using System.IO;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Session;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Hud;
using Mooh.Presentation.Screens;
using Mooh.Presentation.Text;

using SDL3;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Verifies reachable screens, persistence and pause flow through the desktop shell.</summary>
    public sealed class GameShellTests
    {
        /// <summary>Startup reaches the map and prelevel screen without a decorative loading delay.</summary>
        [Fact]
        public void IntroLeadsToMainAndNewGameOpensMapWithoutLoadingDelay()
        {
            GameShell shell = Create();
            Assert.Equal(GameScreen.Intro, shell.Screen);
            for (int i = 0; i < 51; i++)
            {
                shell.Update(default, [], 0.1);
            }

            Assert.Equal(GameScreen.Main, shell.Screen);
            for (int i = 0; i < 4; i++)
            {
                shell.Update(default, [], 0.1);
            }

            shell.HandleKey(SDL.Keycode.F2, true);
            Assert.Equal(GameScreen.Map, shell.Screen);
            shell.HandleKey(SDL.Keycode.Return, true);
            Assert.Equal(GameScreen.PreLevel, shell.Screen);
            for (int i = 0; i < 4; i++)
            {
                shell.Update(default, [], 0.1);
            }

            shell.HandleKey(SDL.Keycode.Return, true);
            Assert.Equal(GameScreen.Playing, shell.Screen);
            Assert.Equal(0, shell.Game!.Session.LevelIndex);
        }

        /// <summary>The pause screen keeps the same simulation session when resumed.</summary>
        [Fact]
        public void PauseMenuFreezesGameplayAndResumeReturnsToTheSameSession()
        {
            GameShell shell = Create();
            shell.StartDirect(0, Core.Simulation.DifficultySpeeds.Standard);
            GameSession session = shell.Game!.Session;
            shell.HandleKey(SDL.Keycode.Escape, true);
            Assert.Equal(GameScreen.Pause, shell.Screen);
            for (int i = 0; i < 4; i++)
            {
                shell.Update(default, [], 0.1);
            }

            shell.HandleKey(SDL.Keycode.Escape, true);
            Assert.Equal(GameScreen.Playing, shell.Screen);
            Assert.Same(session, shell.Game.Session);
        }

        /// <summary>Canceling the restart confirmation returns directly to the same live game.</summary>
        [Fact]
        public void CancelRestartResumesGameplay()
        {
            GameShell shell = Create();
            shell.StartDirect(0, Core.Simulation.DifficultySpeeds.Standard);
            GameSession session = shell.Game!.Session;
            shell.HandleKey(SDL.Keycode.F2, true);
            Assert.Equal(GameScreen.Restart, shell.Screen);
            for (int i = 0; i < 4; i++)
            {
                shell.Update(default, [], 0.1);
            }

            shell.HandleKey(SDL.Keycode.Escape, true);
            Assert.Equal(GameScreen.Playing, shell.Screen);
            Assert.False(shell.Game.IsPaused);
            Assert.Same(session, shell.Game.Session);
        }

        /// <summary>Continue starts a fresh saved level while keeping the saved cumulative score.</summary>
        [Fact]
        public void ContinueRestartsTheSavedLevelWithItsCumulativeScore()
        {
            GameShell shell = Create(new PlayerProfile { SavedGame = new ResumeCheckpoint(17, 0, 1234) });
            shell.InspectScreen("main");
            for (int i = 0; i < 4; i++)
            {
                shell.Update(default, [], 0.1);
            }

            MenuButton button = Enumerable.Single(shell.Menu.Buttons, b => b.Id == "continue");
            shell.MouseMove((button.Left + button.Right) / 2, (button.Top + button.Bottom) / 2);
            shell.MouseButton(true);
            shell.MouseButton(false);
            Assert.Equal(GameScreen.PreLevel, shell.Screen);
            Assert.Equal(17, shell.Game!.Session.LevelIndex);
            Assert.Equal(1234, shell.Game.Session.TotalScore);
            Assert.Equal(0, shell.Game.Session.LevelScore);
            Assert.Equal(100, shell.Game.Session.Level.World.Player!.HealthBody!.Current);
            Assert.Equal(5, shell.Game.Session.Level.World.Player!.PlayerBody!.Progress.Grenades);
            Assert.Equal(0, shell.Game.PlaySeconds);
        }

        /// <summary>Timed runs cannot replace the original game's save slot.</summary>
        [Fact]
        public void TimedModeClearsTheContinueSlot()
        {
            GameShell shell = Create(new PlayerProfile { SavedGame = new ResumeCheckpoint(0, 0, 12) });
            shell.StartDirect(0, Core.Simulation.DifficultySpeeds.Timed);
            shell.SaveProgress();
            Assert.Null(shell.Profile.SavedGame);
        }

        /// <summary>The name editor replaces its initial text and then overwrites at the cursor.</summary>
        [Fact]
        public void HighScoreEntryUsesOriginalOverwriteAndCursorRules()
        {
            PlayerProfile profile = new();
            profile.HighScores[1] = [new("Empty", 0, 0), new("Empty", 0, 0), new("Empty", 0, 0), new("Empty", 0, 0)];
            GameShell shell = Create(profile);
            shell.StartDirect(0, Core.Simulation.DifficultySpeeds.Standard);
            shell.HandleKey(SDL.Keycode.Escape, true);
            Settle(shell);
            Click(shell, "end");
            Assert.Equal(GameScreen.ScoreEntry, shell.Screen);
            Settle(shell);
            shell.HandleText("Alice");
            shell.HandleKey(SDL.Keycode.Left, true);
            shell.HandleText("X");
            shell.HandleKey(SDL.Keycode.Home, false);
            shell.HandleKey(SDL.Keycode.Delete, true);
            shell.HandleKey(SDL.Keycode.End, false);
            shell.HandleKey(SDL.Keycode.Backspace, true);
            shell.HandleKey(SDL.Keycode.Return, true);
            Assert.Equal(GameScreen.HighScores, shell.Screen);
            Assert.Equal("lic", shell.Profile.HighScores[1][0].Name);
        }

        /// <summary>Sound adjustments retain panel entry state and contents follow its outgoing slide.</summary>
        [Fact]
        public void OptionsUpdatesInPlaceAndRetainsItsOutgoingContents()
        {
            GameShell shell = Create();
            shell.InspectScreen("options");
            Settle(shell);
            _ = shell.Elements;
            Click(shell, "sound-down");
            Assert.Equal(0.875, shell.Profile.Options.SoundVolume);
            Assert.True(shell.Menu.IsReady);
            Assert.Contains(shell.Elements, e => e is ScreenText { Text: "88", Alignment: TextAlignment.Center });
            _ = shell.Elements;
            Click(shell, "back");
            Assert.Contains(shell.Elements, e => e is ScreenText { Text: "88" });
        }

        /// <summary>A canceled gesture or focus loss cannot activate a pending menu click.</summary>
        [Fact]
        public void CancelingPointerDoesNotStartNewGame()
        {
            GameShell shell = Create();
            shell.InspectScreen("main");
            Settle(shell);
            MenuButton button = Enumerable.Single(shell.Menu.Buttons, button => button.Id == "new");
            shell.MouseMove((button.Left + button.Right) / 2, (button.Top + button.Bottom) / 2);
            shell.MouseButton(true);
            shell.CancelPointer();
            shell.MouseButton(false);
            Assert.Equal(GameScreen.Main, shell.Screen);
        }

        /// <summary>The legacy RGB565 toggle is no longer exposed in options.</summary>
        [Fact]
        public void OptionsDoNotOfferLegacyColorDepth()
        {
            GameShell shell = Create();
            shell.Profile.Options.FullColor = false;
            shell.InspectScreen("options");
            Settle(shell);
            Assert.DoesNotContain(shell.Menu.Buttons, button => button.Id is "color" or "color-check");
        }

        /// <summary>Display checkboxes take effect when leaving options, as in the original menu.</summary>
        [Fact]
        public void DisplayOptionsApplyWhenReturningFromThePanel()
        {
            GameShell shell = Create();
            shell.InspectScreen("options");
            Settle(shell);
            Click(shell, "fullscreen-check");
            Assert.False(shell.Profile.Options.Fullscreen);
            Assert.True(shell.Menu.IsReady);
            shell.HandleKey(SDL.Keycode.Escape, true);
            Assert.Equal(GameScreen.Main, shell.Screen);
            Assert.True(shell.Profile.Options.Fullscreen);
        }

        /// <summary>A previous pause panel cannot receive clicks once gameplay resumes.</summary>
        [Fact]
        public void GameplayClicksCannotActivateThePreviousPausePanel()
        {
            GameShell shell = Create();
            shell.StartDirect(0, Core.Simulation.DifficultySpeeds.Standard);
            shell.HandleKey(SDL.Keycode.Escape, true);
            Settle(shell);
            MenuButton previousButton = Assert.Single(shell.Menu.Buttons, button => button.Id == "end");
            shell.HandleKey(SDL.Keycode.Escape, true);
            Assert.Empty(shell.Menu.Buttons);
            Assert.Empty(shell.Menu.Elements);
            shell.MouseMove((previousButton.Left + previousButton.Right) / 2, (previousButton.Top + previousButton.Bottom) / 2);
            shell.MouseButton(true);
            shell.MouseButton(false);
            Assert.Equal(GameScreen.Playing, shell.Screen);
            Assert.Null(shell.Profile.SavedGame);
        }

        /// <summary>Leaving the progression map submits the run instead of skipping the score screen.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LeavingProgressionMapSubmitsTheScore(bool useMouse)
        {
            PlayerProfile profile = new();
            profile.HighScores[1] = [new("Empty", 0, 0), new("Empty", 0, 0), new("Empty", 0, 0), new("Empty", 0, 0)];
            GameShell shell = Create(profile);
            shell.StartDirect(0, Core.Simulation.DifficultySpeeds.Standard);
            shell.Update(new Core.Player.PlayerInputState(false, false, false, false, true), [], 0.016);
            shell.Update(default, [], 0.016);
            for (int i = 0; i < 60; i++)
            {
                shell.Update(default, [], 0.05);
            }

            foreach (Core.Entities.Entity entity in shell.Game!.Session.Level.World.Entities.ToArray())
            {
                if (entity.Interactions.HasFlag(Core.Interactions.InteractionMask.LevelEndLock))
                {
                    Assert.True(shell.Game.Session.Level.World.Remove(entity.Id));
                }
            }

            for (int i = 0; i < 150 && shell.Screen == GameScreen.Playing; i++)
            {
                shell.Update(default, [], 0.05);
            }

            Assert.Equal(GameScreen.Statistics, shell.Screen);
            Settle(shell);
            shell.HandleKey(SDL.Keycode.Return, true);
            Assert.Equal(GameScreen.Map, shell.Screen);
            int score = shell.Game.Session.TotalScore;
            if (useMouse)
            {
                shell.MouseMove(80, 570);
                shell.MouseButton(true);
                shell.MouseButton(false);
            }
            else
            {
                shell.HandleKey(SDL.Keycode.Escape, true);
            }

            Assert.Equal(GameScreen.ScoreEntry, shell.Screen);
            Assert.Equal("gameend", shell.TrackName);
            Assert.Equal(score, shell.Profile.HighScores[1][0].Score);
            Assert.Equal(1, shell.Profile.SavedGame!.LevelIndex);
        }

        /// <summary>Runs below the ranking cutoff still show their last score.</summary>
        [Fact]
        public void NonQualifyingRunDisplaysTheLastScore()
        {
            GameShell shell = Create();
            shell.StartDirect(0, Core.Simulation.DifficultySpeeds.Standard);
            shell.HandleKey(SDL.Keycode.Escape, true);
            Settle(shell);
            Click(shell, "end");
            Assert.Equal(GameScreen.HighScores, shell.Screen);
            Assert.Contains(shell.Elements, e => e is ScreenText { Font: "MenuFntEncTitSel" });
        }

        /// <summary>The title and main menu share the original menu-music cue.</summary>
        [Fact]
        public void IntroAndMainShareTheMenuTrack()
        {
            GameShell shell = Create();
            Assert.Equal("menu", shell.TrackName);
            shell.HandleKey(SDL.Keycode.Return, true);
            Assert.Equal("menu", shell.TrackName);
            Settle(shell);
            shell.HandleKey(SDL.Keycode.Escape, true);
            Assert.Equal(GameScreen.Outro, shell.Screen);
            Assert.Equal("menu", shell.TrackName);
        }

        /// <summary>The completion panel provides a visible Continue button instead of a keypress prompt.</summary>
        [Fact]
        public void CompletionUsesContinueButtonWithoutAnyKeyPrompt()
        {
            GameShell shell = Create();
            shell.InspectScreen("complete");
            Settle(shell);
            Assert.DoesNotContain(shell.Elements, element => element is ScreenText text && text.Text.Contains("Press any key", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(shell.Menu.Buttons, button => button.Id == "finish-run");
            Click(shell, "finish-run");
            Assert.Equal(GameScreen.Main, shell.Screen);
        }

        /// <summary>Both completion shortcuts retain score submission when the keypress gate is removed.</summary>
        [Theory]
        [InlineData(SDL.Keycode.Return)]
        [InlineData(SDL.Keycode.Escape)]
        public void CompletionShortcutsSubmitTheFinishedRun(SDL.Keycode key)
        {
            PlayerProfile profile = new();
            profile.HighScores[1] = [new("Empty", 0, 0), new("Empty", 0, 0), new("Empty", 0, 0), new("Empty", 0, 0)];
            GameShell shell = Create(profile);
            shell.StartDirect(0, Core.Simulation.DifficultySpeeds.Standard);
            shell.InspectScreen("complete");
            Settle(shell);
            shell.HandleKey(key, true);
            Assert.Equal(GameScreen.ScoreEntry, shell.Screen);
        }

        /// <summary>Opening a gameplay dialog must not animate the dismissed level introduction back onto the screen.</summary>
        [Theory]
        [InlineData(SDL.Keycode.F2)]
        [InlineData(SDL.Keycode.Escape)]
        public void GameplayDialogsDoNotResurrectTheLevelIntroduction(SDL.Keycode key)
        {
            GameShell shell = Create();
            shell.InspectScreen("prelevel");
            Settle(shell);
            string previousTitle = Assert.Single(shell.Elements.OfType<ScreenText>(), text => text.Font == "MenuFntTit").Text;
            Click(shell, "begin");
            Assert.Equal(GameScreen.Playing, shell.Screen);
            shell.Update(default, [], 0.1);
            shell.HandleKey(key, true);
            Assert.DoesNotContain(shell.Elements.OfType<ScreenText>(), text => text.Text == previousTitle);
            _ = Assert.Single(shell.Elements.OfType<ScreenText>(), text => text.Font == "MenuFntTit");
        }

        /// <summary>Resuming gameplay dismisses the last dialog before another gameplay dialog is opened.</summary>
        [Theory]
        [InlineData(SDL.Keycode.F2, SDL.Keycode.Escape)]
        [InlineData(SDL.Keycode.Escape, SDL.Keycode.F2)]
        public void ResumedGameplayDoesNotRetainItsPreviousDialog(SDL.Keycode first, SDL.Keycode second)
        {
            GameShell shell = Create();
            shell.StartDirect(0, Core.Simulation.DifficultySpeeds.Standard);
            shell.HandleKey(first, true);
            Settle(shell);
            string previousTitle = Assert.Single(shell.Elements.OfType<ScreenText>(), text => text.Font == "MenuFntTit").Text;
            shell.HandleKey(SDL.Keycode.Escape, true);
            Assert.Equal(GameScreen.Playing, shell.Screen);
            shell.HandleKey(second, true);
            Assert.DoesNotContain(shell.Elements.OfType<ScreenText>(), text => text.Text == previousTitle);
        }

        /// <summary>The map separates the main menu and level introduction rather than replaying a hidden panel transition.</summary>
        [Fact]
        public void StartingFromTheMapDoesNotRestoreTheMainMenu()
        {
            GameShell shell = Create();
            shell.InspectScreen("main");
            Settle(shell);
            string previousTitle = Assert.Single(shell.Elements.OfType<ScreenText>(), text => text.Font == "MenuFntTit").Text;
            shell.HandleKey(SDL.Keycode.F2, true);
            Assert.Equal(GameScreen.Map, shell.Screen);
            shell.HandleKey(SDL.Keycode.Return, true);
            Assert.Equal(GameScreen.PreLevel, shell.Screen);
            Assert.DoesNotContain(shell.Elements.OfType<ScreenText>(), text => text.Text == previousTitle);
        }

        private static void Settle(GameShell shell)
        {
            for (int i = 0; i < 4; i++)
            {
                shell.Update(default, [], 0.1);
            }
        }

        private static void Click(GameShell shell, string id)
        {
            MenuButton button = Enumerable.Single(shell.Menu.Buttons, b => b.Id == id);
            shell.MouseMove((button.Left + button.Right) / 2, (button.Top + button.Bottom) / 2);
            shell.MouseButton(true);
            shell.MouseButton(false);
        }

        private static GameShell Create(PlayerProfile? profile = null)
        {
            string assets = Path.Combine(AppContext.BaseDirectory, "assets");
            using FileStream animation = File.OpenRead(Path.Combine(assets, "animations", "animations.json"));
            using FileStream text = File.OpenRead(Path.Combine(assets, "text", "strings.json"));
            using FileStream font = File.OpenRead(Path.Combine(assets, "fonts", HudPresenter.LevelFontName + ".fnt"));
            return new GameShell(assets, JsonLevelPackLoader.Load(Path.Combine(assets, "levels", "manifest.json")),
                AnimationLibrary.Load(animation), StringTable.Load(text), BitmapFont.Load(HudPresenter.LevelFontName, font),
                profile ?? new PlayerProfile(), null);
        }
    }
}
