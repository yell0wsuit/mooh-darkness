using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Player;
using Mooh.Core.Session;
using Mooh.Core.Simulation;
using Mooh.Desktop.Platform.Input;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Audio;
using Mooh.Presentation.Screens;
using Mooh.Presentation.Sprites;
using Mooh.Presentation.Text;

using SDL3;

namespace Mooh.Desktop
{
    /// <summary>The screens reached by the shipped game.</summary>
    internal enum GameScreen
    {
        Intro, Main, Instructions, Map, PreLevel, Playing, Pause, Options, Restart, HighScores, ScoreEntry, Statistics, Completion, Outro
    }

    /// <summary>Coordinates the original menu, map, gameplay and completion flow.</summary>
    internal sealed class GameShell
    {
        private readonly LoadedLevelPack _pack;
        private readonly AnimationLibrary _library;
        private readonly StringTable _strings;
        private readonly BitmapFont _levelFont;
        private readonly BitmapFont _scoreFont;
        private readonly ISpriteMask? _masks;
        private readonly PlayerProfileStore? _store;
        private readonly Random _random = new();
        private readonly MenuBackground _background;
        private readonly MapPresentation _map;
        private readonly InstructionsPresentation _instructions;
        private readonly List<SoundCue> _pendingSounds = [];
        private readonly List<ScreenElement> _extras = [];
        private double _screenSeconds;
        private double _mouseX = 400;
        private double _mouseY = 300;
        private bool _mouseDown;
        private bool _optionsInGame;
        private bool _fullscreenChoice;
        private bool _fullColorChoice;
        private bool _mapCanChange;
        private bool _resume;
        private int _difficulty;
        private int _scoreRank;
        private string _playerName = string.Empty;
        private int _namePosition;
        private bool _firstNameCharacter;
        private GameSession? _preparedSession;
        private int _loadSteps;

        public GameShell(string assets, LoadedLevelPack pack, AnimationLibrary library, StringTable strings,
            BitmapFont levelFont, PlayerProfile profile, PlayerProfileStore? store, ISpriteMask? masks = null)
        {
            _pack = pack;
            _library = library;
            _strings = strings;
            _levelFont = levelFont;
            Profile = profile;
            _store = store;
            _masks = masks;
            using FileStream page = File.OpenRead(Path.Combine(assets, "sprites", "SpriteMenuPrt.seq"));
            using FileStream font = File.OpenRead(Path.Combine(assets, "fonts", "MenuFntBtnNrm.fnt"));
            using FileStream tooltip = File.OpenRead(Path.Combine(assets, "fonts", "MenuFontToolTip.fnt"));
            using FileStream scoreFont = File.OpenRead(Path.Combine(assets, "fonts", "MenuFntEncTitSel.fnt"));
            _scoreFont = BitmapFont.Load("MenuFntEncTitSel", scoreFont);
            Menu = new MenuPanelPresentation(SpritePage.Load("SpriteMenuPrt", page), BitmapFont.Load("MenuFntBtnNrm", font), BitmapFont.Load("MenuFontToolTip", tooltip), _random);
            using FileStream waypoints = File.OpenRead(Path.Combine(assets, "map", "map.waypoints"));
            _map = new MapPresentation(MapWaypoints.Load(waypoints), [.. pack.Manifest.Levels!.Select(l => "map-previews/" + l.Name + ".jpg")], Localize);
            _background = new MenuBackground(_random);
            _instructions = new InstructionsPresentation(Localize);
        }

        public PlayerProfile Profile
        {
            get;
        }
        public GameScreen Screen { get; private set; } = GameScreen.Intro;
        public DesktopGame? Game
        {
            get; private set;
        }
        public MenuPanelPresentation Menu
        {
            get;
        }
        public double ElapsedSeconds
        {
            get; private set;
        }
        public bool ShouldQuit
        {
            get; private set;
        }
        public IReadOnlyList<SoundCue> Sounds { get; private set; } = [];
        public bool ShowsWorld => (Game is not null && Screen is GameScreen.Playing or GameScreen.Pause or GameScreen.Restart or GameScreen.Statistics or GameScreen.PreLevel) || (Game is not null && Screen == GameScreen.Options && _optionsInGame);
        public string TrackName
        {
            get => Screen == GameScreen.Intro ? string.Empty
            : Screen == GameScreen.Outro ? field
            : ShowsWorld || (Screen == GameScreen.Map && !_mapCanChange) ? "game" : field; private set;
        } = "menu";

        public IReadOnlyList<ScreenElement> Elements
        {
            get
            {
                List<ScreenElement> result = [];
                if (Screen is GameScreen.Intro or GameScreen.Outro)
                {
                    result.Add(new ScreenImage(Screen == GameScreen.Intro ? "Intro.jpg" : "Outro.jpg", 0, 0, 800, 600));
                    result.Add(new ScreenSprite(Screen == GameScreen.Intro ? "SpriteIntRo" : "SpriteOutRo", "Bk_OLogo_BTO"));
                    if (Screen == GameScreen.Outro)
                    {
                        result.Add(new ScreenSprite("SpriteOutRo", "Bk_OLogo_Int"));
                        result.Add(new ScreenSprite("SpriteOutRo", "Bk_OTxtDe_Pow"));
                    }
                    return result;
                }
                if (Screen == GameScreen.Instructions)
                {
                    return _instructions.Elements;
                }

                if (Screen == GameScreen.Map)
                {
                    result.AddRange(_map.Elements);
                    result.Add(new ScreenText(Localize("Map2MainMenu"), 60, 570, Alignment: TextAlignment.Left));
                    result.Add(new ScreenText(Localize("MapLvlStart"), 740, 570, Alignment: TextAlignment.Right));
                }
                else if (Screen != GameScreen.Playing)
                {
                    if (!ShowsWorld)
                    {
                        result.Add(new ScreenImage("MenuBack.jpg", 0, 0, 800, 600));
                        result.Add(new ScreenText(Localize("AppBuildTxt").Replace("%s", "1.0 (Internal)", StringComparison.Ordinal), 792, 16, "VersionFont", TextAlignment.Right));
                    }

                    if (ShowsWorld)
                    {
                        result.Add(new ScreenTile("SpriteMenuPrt", "OBackBack"));
                    }

                    RefreshPanelContents();
                    result.AddRange(Menu.Elements);
                }
                if (Screen != GameScreen.Playing)
                {
                    result.Add(_background.Cursor(_mouseX, _mouseY, _mouseDown));
                }

                return result;
            }
        }

        public void StartDirect(int levelIndex, DifficultySpeeds difficulty)
        {
            _difficulty = difficulty == DifficultySpeeds.Timed ? 2 : difficulty == DifficultySpeeds.DifficultyZero ? 0 : 1;
            StartSession(new GameSession(_pack, difficulty, levelIndex), false);
        }

        /// <summary>Opens a shell screen for deterministic rendering checks.</summary>
        internal void InspectScreen(string name)
        {
            if (name == "main")
            {
                ShowMain();
            }
            else if (name == "options")
            {
                ShowMain();
                ShowOptions();
            }
            else if (name == "scores")
            {
                ShowScores(false);
            }
            else if (name == "instructions")
            {
                BeginInstructions(false);
            }
            else if (name == "map")
            {
                BeginInstructions(false);
                FinishInstructions();
            }
            else if (name is "prelevel" or "pause" or "statistics")
            {
                StartSession(new GameSession(_pack, DifficultySpeeds.DifficultyZero), name == "prelevel");
                if (name == "pause")
                {
                    ShowPause();
                }

                if (name == "statistics")
                {
                    ShowStatistics();
                }
            }
            else if (name == "complete")
            {
                ShowCompletion();
            }
            else if (name == "outro")
            {
                Screen = GameScreen.Outro;
                _screenSeconds = 0;
            }
            else if (name != "intro")
            {
                throw new ArgumentException("Unknown screen '" + name + "'.", nameof(name));
            }
        }

        public void Update(PlayerInputState input, IReadOnlyList<ControlCommand> commands, double seconds, bool anyKeyHeld = false)
        {
            seconds = Math.Clamp(seconds, 0, 0.1);
            ElapsedSeconds += seconds;
            _screenSeconds += seconds;
            _background.Advance(seconds);
            RefreshPanelContents();
            Menu.Advance(seconds);
            _instructions.Advance(seconds);
            List<SoundCue> sounds = [.. _pendingSounds];
            _pendingSounds.Clear();
            if (Screen == GameScreen.Intro && _screenSeconds >= 5)
            {
                ShowMain();
            }
            else if (Screen == GameScreen.Outro && _screenSeconds >= 5)
            {
                ShouldQuit = true;
            }
            else if (Screen == GameScreen.Instructions && !_instructions.IsReady && _screenSeconds > (_loadSteps + 1) * 0.028)
            {
                _loadSteps++;
                _instructions.SetProgress(_loadSteps / 177.0, _loadSteps >= 177);
            }
            else if (Screen == GameScreen.Playing && Game is not null)
            {
                Game.Update(input, [.. commands.Where(c => c.Kind != ControlCommandKind.TogglePause)], seconds, anyKeyHeld);
                sounds.AddRange(Game.Sounds);
                if (Game.OutcomeReady)
                {
                    if (Game.Session.State == GameSessionState.LevelFailed)
                    {
                        EndRun();
                    }
                    else if (Game.Session.State == GameSessionState.CampaignCompleted)
                    {
                        Profile.MarkLevelPassed(Game.Session.LevelIndex);
                        Profile.SavedGame = null;
                        ShowCompletion();
                        Persist();
                    }
                    else if (Game.Session.State == GameSessionState.LevelCompleted)
                    {
                        Profile.MarkLevelPassed(Game.Session.LevelIndex);
                        SaveProgress();
                        ShowStatistics();
                        Persist();
                    }
                }
            }
            else if (ShowsWorld && Game is not null)
            {
                Game.Update(default, [], seconds);
                sounds.AddRange(Game.Sounds);
            }
            Sounds = sounds;
        }

        public void HandleKey(SDL.Keycode key, bool pressed)
        {
            if (!pressed)
            {
                if (Screen == GameScreen.ScoreEntry && Menu.IsReady)
                {
                    if (key == SDL.Keycode.Home)
                    {
                        _namePosition = 0;
                    }

                    if (key == SDL.Keycode.End)
                    {
                        _namePosition = Math.Min(_playerName.Length, 13);
                    }
                }
                return;
            }

            if (Screen == GameScreen.Intro)
            {
                ShowMain();
                return;
            }
            if (Screen == GameScreen.Outro)
            {
                ShouldQuit = true;
                return;
            }
            if (Screen == GameScreen.Instructions)
            {
                if (_instructions.IsReady)
                {
                    FinishInstructions();
                }

                return;
            }
            if (Screen is not GameScreen.Playing and not GameScreen.Map && !Menu.IsReady)
            {
                return;
            }

            if (Screen == GameScreen.Completion)
            {
                EndRun();
                return;
            }
            if (Screen == GameScreen.ScoreEntry)
            {
                if (key == SDL.Keycode.Left)
                {
                    _namePosition = Math.Max(0, _namePosition - 1);
                }
                else if (key == SDL.Keycode.Right)
                {
                    _namePosition = Math.Min(Math.Min(_namePosition + 1, _playerName.Length), 13);
                }
                else if (key == SDL.Keycode.Delete && _namePosition < _playerName.Length)
                {
                    _playerName = _playerName.Remove(_namePosition, 1);
                }
                else if (key == SDL.Keycode.Backspace && _playerName.Length > 0)
                {
                    if (_namePosition > 0)
                    {
                        _namePosition--;
                    }

                    _playerName = _playerName.Remove(_namePosition, 1);
                }
                else if (key is SDL.Keycode.Return or SDL.Keycode.Escape)
                {
                    SaveName();
                }

                if (key is SDL.Keycode.Left or SDL.Keycode.Right or SDL.Keycode.Delete or SDL.Keycode.Backspace or SDL.Keycode.Home or SDL.Keycode.End)
                {
                    _firstNameCharacter = false;
                }

                return;
            }
            if (Screen == GameScreen.Playing)
            {
                if (key is SDL.Keycode.Escape or SDL.Keycode.P or SDL.Keycode.Pause)
                {
                    ShowPause();
                }
                else if (key == SDL.Keycode.F2 && _difficulty != 2)
                {
                    ShowRestart();
                }

                return;
            }
            if (Screen == GameScreen.Map)
            {
                if (key is SDL.Keycode.Return or SDL.Keycode.F2)
                {
                    StartMapLevel();
                }
                else if (key == SDL.Keycode.Escape)
                {
                    LeaveMap();
                }
                return;
            }
            if (key is SDL.Keycode.Up or SDL.Keycode.Left)
            {
                Menu.Navigate(-1);
                Cue("MenuBtnChange");
            }
            else if (key is SDL.Keycode.Down or SDL.Keycode.Right or SDL.Keycode.Tab)
            {
                Menu.Navigate(1);
                Cue("MenuBtnChange");
            }
            else if (key is SDL.Keycode.Return or SDL.Keycode.Space)
            {
                Activate(Menu.Activate());
            }
            else if (key == SDL.Keycode.F10 && Screen is GameScreen.Main or GameScreen.Pause)
            {
                ShowOptions();
            }
            else if (key == SDL.Keycode.F2)
            {
                if (Screen == GameScreen.Main)
                {
                    BeginInstructions(false);
                }
                else if (Screen == GameScreen.Pause)
                {
                    ShowRestart();
                }
                else if (Screen == GameScreen.Restart)
                {
                    Activate("restart");
                }
                else if (Screen == GameScreen.Statistics)
                {
                    Activate("next");
                }
                else if (Screen == GameScreen.PreLevel)
                {
                    Resume();
                }
            }
            else if (key is SDL.Keycode.Escape or SDL.Keycode.P or SDL.Keycode.Pause)
            {
                if (Screen == GameScreen.Main)
                {
                    Exit();
                }
                else if (Screen == GameScreen.Pause)
                {
                    Resume();
                }
                else if (Screen == GameScreen.Restart)
                {
                    Resume();
                }
                else if (Screen == GameScreen.Options)
                {
                    ApplyDisplayOptions();
                    Persist();
                    if (_optionsInGame)
                    {
                        ShowPause();
                    }
                    else
                    {
                        ShowMain();
                    }
                }
                else if (Screen == GameScreen.Statistics)
                {
                    Activate("next");
                }
                else if (Screen == GameScreen.PreLevel)
                {
                    Resume();
                }
                else
                {
                    ShowMain();
                }
            }
        }

        /// <summary>Applies text input to the original overwrite-mode name editor.</summary>
        public void HandleText(string text)
        {
            if (Screen != GameScreen.ScoreEntry || !Menu.IsReady)
            {
                return;
            }

            foreach (char character in text)
            {
                if (char.IsControl(character) || character > 255 || !_scoreFont.Has(character))
                {
                    continue;
                }

                if (_firstNameCharacter)
                {
                    _playerName = character.ToString();
                    _namePosition = 1;
                    _firstNameCharacter = false;
                }
                else
                {
                    string suffix = _namePosition + 1 < _playerName.Length
                        ? _playerName[(_namePosition + 1)..Math.Min(_playerName.Length, 13)] : string.Empty;
                    _playerName = _playerName[.._namePosition] + character + suffix;
                    _namePosition = Math.Min(_namePosition + 1, 13);
                }
            }
        }

        private void RefreshPanelContents()
        {
            List<ScreenElement> contents = [.. _extras.Select(element => element switch
            {
                ScreenText label => label with { X = label.X - 400, Y = label.Y - 300 },
                ScreenSprite sprite => sprite with { X = sprite.X - 400, Y = sprite.Y - 300 },
                _ => element,
            })];
            if (Screen == GameScreen.Options)
            {
                foreach ((string id, double volume) in new[] { ("sound-cycle", Profile.Options.SoundVolume), ("music-cycle", Profile.Options.MusicVolume) })
                {
                    (double x, double y) = Menu.GetButtonAnchor(id);
                    contents.Add(new ScreenText(Math.Round(volume * 100).ToString(CultureInfo.InvariantCulture), x - 228, y - Menu.PanelY, "MenuFntBtnNrm", TextAlignment.Center));
                }
            }
            if (Screen == GameScreen.PreLevel)
            {
                int frame = (int)(_screenSeconds / 0.05) % 20;
                contents.Add(new ScreenSprite("SpriteMonGreen", "OWlkLeft" + frame.ToString("00", CultureInfo.InvariantCulture), -120, -8));
                contents.Add(new ScreenSprite("SpriteMonBlue", "OWlkLeft" + (frame + 10).ToString("00", CultureInfo.InvariantCulture), 122, -20));
            }
            if (Screen == GameScreen.ScoreEntry)
            {
                double y = -71 + (_scoreRank * 55);
                contents.Add(new ScreenText(_playerName, -220, y, "MenuFntEncTitSel", TextAlignment.Left));
                if (((long)(ElapsedSeconds * 1000) & 640) != 0)
                {
                    contents.Add(new ScreenText("|", -220 + _scoreFont.Width(_playerName[.._namePosition]), y, "MenuFntEncTitSel", TextAlignment.Left));
                }
            }
            Menu.Contents = contents;
        }

        public void MouseMove(double x, double y)
        {
            _mouseX = x;
            _mouseY = y;
            Menu.MovePointer(x, y);
        }

        public void MouseButton(bool pressed, bool right = false)
        {
            _mouseDown = pressed;
            if (_mouseX < 0 || _mouseX > 800 || _mouseY < 0 || _mouseY > 600)
            {
                return;
            }

            if (right && !pressed && Screen == GameScreen.Playing)
            {
                ShowPause();
                return;
            }
            if (right)
            {
                return;
            }

            if (Screen is GameScreen.Intro or GameScreen.Instructions or GameScreen.Completion)
            {
                if (pressed)
                {
                    HandleKey(SDL.Keycode.Return, true);
                }

                return;
            }
            if (Screen == GameScreen.Playing)
            {
                if (!pressed && Game?.Session.Level.World.Player is { } player)
                {
                    Game.Hud.Update(Game.Session.Level.World, [new HelpTextRequested(player.Id, "HlpUseKey")], Game.Session.TotalScore, 0, Game.Camera);
                }
                return;
            }
            if (Screen == GameScreen.Outro)
            {
                return;
            }

            if (Screen == GameScreen.Map)
            {
                if (!pressed)
                {
                    return;
                }

                if (_mouseY >= 540 && _mouseX < 150)
                {
                    LeaveMap();
                }
                else if (_mouseY >= 540 && _mouseX > 650)
                {
                    StartMapLevel();
                }
                else if (_map.SelectNearest(_mouseX, _mouseY))
                {
                    Cue("MenuClick");
                }

                return;
            }
            if (pressed)
            {
                Menu.Press();
            }
            else
            {
                Activate(Menu.Release());
            }
        }

        public void SaveProgress()
        {
            if (Game is not null && Game.Session.State != GameSessionState.CampaignCompleted)
            {
                Profile.SavedGame = _difficulty == 2 ? null : new ResumeCheckpoint(Game.Session.LevelIndex, _difficulty, Game.Session.TotalScore);
            }

            Persist();
        }

        private void Activate(string? action)
        {
            if (action is null)
            {
                return;
            }

            Cue("MenuBtnClick");
            switch (action)
            {
                case "begin":
                    Resume();
                    break;
                case "new":
                    BeginInstructions(false);
                    break;
                case "continue":
                    BeginInstructions(true);
                    break;
                case "scores":
                    ShowScores(false);
                    break;
                case "options":
                    ShowOptions();
                    break;
                case "exit":
                    Exit();
                    break;
                case "resume":
                    Resume();
                    break;
                case "restart-confirm":
                    ShowRestart();
                    break;
                case "restart":
                    Game!.Session.RestartLevel();
                    StartSession(Game.Session);
                    break;
                case "end":
                    EndRun();
                    break;
                case "back":
                    if (Screen == GameScreen.Options)
                    {
                        ApplyDisplayOptions();
                    }

                    if (Screen == GameScreen.Restart)
                    {
                        Resume();
                    }
                    else if (_optionsInGame && Screen == GameScreen.Options)
                    {
                        ShowPause();
                    }
                    else
                    {
                        ShowMain();
                    }

                    break;
                case "next":
                    Game!.Session.AdvanceToNextLevel();
                    _mapCanChange = false;
                    _map.Update(Game.Session.LevelIndex, Profile.PassedLevels.ToHashSet(), false);
                    Screen = GameScreen.Map;
                    _screenSeconds = 0;
                    Persist();
                    break;
                case "sound-cycle":
                    ChangeVolume(false, Profile.Options.SoundVolume <= 0 ? 1 : -0.125);
                    break;
                case "music-cycle":
                    ChangeVolume(true, Profile.Options.MusicVolume <= 0 ? 1 : -0.125);
                    break;
                case "sound-down":
                    ChangeVolume(false, -0.125);
                    break;
                case "sound-up":
                    ChangeVolume(false, 0.125);
                    break;
                case "music-down":
                    ChangeVolume(true, -0.125);
                    break;
                case "music-up":
                    ChangeVolume(true, 0.125);
                    break;
                case "fullscreen-check":
                case "fullscreen":
                    _fullscreenChoice = !_fullscreenChoice;
                    Menu.SetButtonLabel("fullscreen-check", _fullscreenChoice ? "\u0001" : "\u0002");
                    break;
                case "color-check":
                case "color":
                    _fullColorChoice = !_fullColorChoice;
                    Menu.SetButtonLabel("color-check", _fullColorChoice ? "\u0001" : "\u0002");
                    break;
                case "clear-scores":
                    ShowScores(true);
                    break;
                case "clear":
                    Profile.HighScores = new PlayerProfile().HighScores;
                    Persist();
                    ShowScores(true);
                    break;
                case "save-name":
                    SaveName();
                    break;
                default:
                    break;
            }
        }

        private void ShowMain()
        {
            Screen = GameScreen.Main;
            TrackName = "menu";
            _screenSeconds = 0;
            _extras.Clear();
            List<MenuItem> items = [Item("new", "BtnMainMenuStartGame", 0, 0)];
            double y = 60;
            if (Profile.SavedGame is not null)
            {
                items.Add(Item("continue", "BtnMainMenuLoadGame", 0, y));
                y += 60;
            }
            y += 10;
            items.Add(Item("scores", "BtnMainMenuStartHScore", 0, y));
            items.Add(Item("options", "BtnMainMenuStartOptions", 0, y + 60));
            items.Add(Item("exit", "BtnMainMenuStartExit", 0, y + 130));
            Menu.Show(MenuPanelKind.Main, Localize("MenuTitleMain"), items);
        }

        private void BeginInstructions(bool resume)
        {
            _resume = resume;
            Screen = GameScreen.Instructions;
            _screenSeconds = 0;
            _instructions.SetProgress(0);
            _loadSteps = 0;
            if (resume && Profile.SavedGame is ResumeCheckpoint checkpoint)
            {
                _difficulty = checkpoint.Difficulty;
                _preparedSession = new GameSession(_pack, DifficultyOf(checkpoint.Difficulty), Math.Clamp(checkpoint.LevelIndex, 0, _pack.Levels.Count - 1), startingScore: checkpoint.TotalScore);
            }
            else
            {
                _difficulty = 0;
                _preparedSession = new GameSession(_pack, DifficultySpeeds.DifficultyZero);
            }
        }

        private void FinishInstructions()
        {
            if (_resume)
            {
                StartSession(_preparedSession!);
                return;
            }
            _mapCanChange = true;
            _map.Update(0, Profile.PassedLevels.ToHashSet(), true);
            Screen = GameScreen.Map;
            _screenSeconds = 0;
        }

        private void LeaveMap()
        {
            if (_mapCanChange)
            {
                ShowMain();
            }
            else
            {
                EndRun();
            }
        }

        private void StartMapLevel()
        {
            if (_mapCanChange)
            {
                StartSession(new GameSession(_pack, DifficultyOf(_difficulty), _map.SelectedIndex));
            }
            else
            {
                StartSession(Game!.Session);
            }
        }

        private void StartSession(GameSession session, bool showPreLevel = true)
        {
            Game = new DesktopGame(session, _library, _random, _strings, _levelFont, _masks, automaticProgression: false);
            Screen = GameScreen.Playing;
            _screenSeconds = 0;
            if (showPreLevel)
            {
                ShowPreLevel();
            }
        }

        private void ShowPreLevel()
        {
            Game!.Update(default, [ControlCommand.TogglePause], 0);
            Screen = GameScreen.PreLevel;
            _extras.Clear();
            Menu.Show(MenuPanelKind.PreLevel, "Level " + (Game.Session.LevelIndex + 1).ToString(CultureInfo.InvariantCulture), [Item("begin", "MenuPreLvlBtnNext", 0, 151)], null, null);
            AddLines(Localize("MenuPreLvlText"), 400, 160);
            int green = Game.Session.Level.World.Entities.Count(e => e.GreenMonsterBody is not null);
            int blue = Game.Session.Level.World.Entities.Count(e => e.BlueMonsterBody is not null);
            _extras.Add(new ScreenText(green.ToString(CultureInfo.InvariantCulture) + " x", 245, 290, "MenuFntEncTxt", TextAlignment.Right));
            _extras.Add(new ScreenText(blue.ToString(CultureInfo.InvariantCulture) + " x", 486, 286, "MenuFntEncTxt", TextAlignment.Right));
        }

        private void ShowPause()
        {
            if (Game is null)
            {
                return;
            }

            if (!Game.IsPaused)
            {
                Game.Update(default, [ControlCommand.TogglePause], 0);
            }

            Screen = GameScreen.Pause;
            _extras.Clear();
            _screenSeconds = 0;
            List<MenuItem> items = [Item("resume", "BtnInGameBack", 100, 0), Item("options", "BtnInGameOptions", 100, 60)];
            if (_difficulty != 2)
            {
                items.Add(Item("restart-confirm", "BtnInGameReStart", 100, 130));
            }

            items.Add(Item("end", "BtnInGameExitGame", 100, _difficulty == 2 ? 130 : 190));
            Menu.Show(MenuPanelKind.Main, Localize("MenuTitleInGame"), items, 0, 0);
        }

        private void Resume()
        {
            if (Game!.IsPaused)
            {
                Game.Update(default, [ControlCommand.TogglePause], 0);
            }

            Screen = GameScreen.Playing;
        }

        private void ShowRestart()
        {
            if (Game is null || _difficulty == 2)
            {
                return;
            }

            if (!Game.IsPaused)
            {
                Game.Update(default, [ControlCommand.TogglePause], 0);
            }

            Screen = GameScreen.Restart;
            _extras.Clear();
            Menu.Show(MenuPanelKind.Message, Localize("GameRstTit"),
                [Item("back", "GameRstBtnCnt", 0, 40), Item("restart", "GameRstBtnRst", 0, 110)], null, null);
            AddLines(Localize("GameRstTxt"), 400, 250);
        }

        private void ShowOptions()
        {
            _optionsInGame = Screen == GameScreen.Pause || (Screen == GameScreen.Options && _optionsInGame);
            Screen = GameScreen.Options;
            _fullscreenChoice = Profile.Options.Fullscreen;
            _fullColorChoice = Profile.Options.FullColor;
            _extras.Clear();
            List<MenuItem> items = [
                Item("sound-cycle", "BtnOptSndVol", -70, 0),
                new("sound-down", "-", Localize("BtnOptSndVolDecTTip"), 40, 0, true, false),
                new("sound-up", "+", Localize("BtnOptSndVolIncTTip"), 150, 0, true, false),
                Item("music-cycle", "BtnOptMusVol", -70, 60),
                new("music-down", "-", Localize("BtnOptMusVolDecTTip"), 40, 60, true, false),
                new("music-up", "+", Localize("BtnOptMusVolIncTTip"), 150, 60, true, false)];
            if (!_optionsInGame)
            {
                items.Add(Item("fullscreen", "BtnOptFullScr", -20, 130));
                items.Add(new("fullscreen-check", Profile.Options.Fullscreen ? "\u0001" : "\u0002", Localize("BtnOptFullScrTTip"), 150, 130));
                items.Add(Item("color", "BtnOptFullColor", -20, 190));
                items.Add(new("color-check", Profile.Options.FullColor ? "\u0001" : "\u0002", Localize("BtnOptFullColorTTip"), 150, 190));
                items.Add(Item("clear-scores", "BtnOptClearScore", 0, 250));
            }
            items.Add(Item("back", "BtnOptBack", 0, _optionsInGame ? 130 : 310));
            Menu.Show(MenuPanelKind.Main, Localize("MenuTitleOpt"), items, -10, 10);
        }

        private void ApplyDisplayOptions()
        {
            if (!_optionsInGame)
            {
                Profile.Options.Fullscreen = _fullscreenChoice;
                Profile.Options.FullColor = _fullColorChoice;
            }
            Persist();
        }

        private void ChangeVolume(bool music, double delta)
        {
            if (music)
            {
                Profile.Options.MusicVolume = Math.Clamp(Profile.Options.MusicVolume + delta, 0, 1);
            }
            else
            {
                Profile.Options.SoundVolume = Math.Clamp(Profile.Options.SoundVolume + delta, 0, 1);
            }

            Persist();
        }

        private void ShowStatistics()
        {
            Screen = GameScreen.Statistics;
            _extras.Clear();
            _screenSeconds = 0;
            if (!Game!.IsPaused)
            {
                Game.Update(default, [ControlCommand.TogglePause], 0);
            }

            Menu.Show(MenuPanelKind.Statistics, Localize("MenuLStatTit"), [Item("next", "MenuLStatBtnNext", 0, 186)], null, null);
            PlayerProgress progress = Game.Session.Level.World.Player!.PlayerBody!.Progress;
            string[] values = [$"{Game.Session.LevelIndex + 1} (of {Game.Session.LevelCount})", Game.Session.TotalScore.ToString(CultureInfo.InvariantCulture),
                Game.Session.LevelScore.ToString(CultureInfo.InvariantCulture), $"{(int)Math.Ceiling(Game.PlaySeconds) / 60} min. {(int)Math.Ceiling(Game.PlaySeconds) % 60:00} sec.",
                $"{progress.TreasureCollected} of {progress.TotalTreasure}"];
            string[] labels = ["MenuLStatLabLev", "MenuLStatLabScore", "MenuLStatLabLScore", "MenuLStatLabLTime", "MenuLStatLabTreas"];
            double[] ys = [210, 235, 280, 305, 330];
            for (int i = 0; i < values.Length; i++)
            {
                _extras.Add(new ScreenText(Localize(labels[i]), 280, ys[i], "MenuFntEncTxt", TextAlignment.Right));
                _extras.Add(new ScreenText(values[i], 290, ys[i], "MenuFntEncTxt", TextAlignment.Left));
            }
            _extras.Add(new ScreenSprite("SpriteMenuPrt", "OPrt_Sep", 173, 247));
            _extras.Add(new ScreenSprite("SpriteMenuPrt", "OPrt_Sep", 173, 350));
            AddLines(Localize("MenuLStatTTip" + _random.Next(1, 8)), 290, 375);
        }

        private void ShowCompletion()
        {
            Screen = GameScreen.Completion;
            TrackName = "gameend";
            _extras.Clear();
            _screenSeconds = 0;
            Menu.Show(MenuPanelKind.Completion, Localize("MenuTitleCongrat"), []);
            AddLines(Localize("CongratTxtLast"), 293, 214);
            _extras.Add(new ScreenText(Localize("GameInstrKey"), 400, 565, "MenuFntEncTxt"));
        }

        private void EndRun()
        {
            TrackName = "gameend";
            if (Game is not null)
            {
                if (Game.Session.State != GameSessionState.CampaignCompleted)
                {
                    SaveProgress();
                }

                _playerName = Profile.LastPlayerName;
                int? rank = Profile.SubmitHighScore(_difficulty, _playerName, Game.Session.TotalScore, Game.Session.LevelIndex);
                _scoreRank = rank ?? -1;
                _namePosition = 0;
                _firstNameCharacter = true;
                ShowScores(false, rank.HasValue, rank.HasValue ? null : Game.Session.TotalScore);
                Persist();
            }
            else
            {
                ShowMain();
            }
        }

        private void ShowScores(bool allowClear, bool edit = false, int? lastScore = null)
        {
            Screen = edit ? GameScreen.ScoreEntry : GameScreen.HighScores;
            _extras.Clear();
            _screenSeconds = 0;
            List<MenuItem> buttons = [Item(edit ? "save-name" : "back", edit ? "BtnHScoreEditSave" : "BtnHScoreShowBack", 0, 142)];
            if (allowClear)
            {
                buttons.Add(Item("clear", "BtnHScoreShowClear", 0, 199));
            }

            Menu.Show(MenuPanelKind.HighScores, Localize(edit ? "MenuTitleHScoreEdit" : "MenuTitleHScoreShow"), buttons, null, null);
            List<HighScoreEntry> table = Profile.HighScores[edit ? _difficulty : 0];
            for (int i = 0; i < table.Count; i++)
            {
                if (!edit || i != _scoreRank)
                {
                    _extras.Add(new ScreenText(table[i].Name, 180, 229 + (i * 55), "MenuFntEncTit", TextAlignment.Left));
                }

                _extras.Add(new ScreenText(table[i].Score.ToString(CultureInfo.InvariantCulture), 607, 229 + (i * 55), edit && i == _scoreRank ? "MenuFntEncTitSel" : "MenuFntEncTit", TextAlignment.Right));
            }
            if (lastScore.HasValue)
            {
                _extras.Add(new ScreenText(Localize("BtnHScoreShowLastScoreTxt").Replace("%d", lastScore.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal), 400, 177, "MenuFntEncTitSel"));
            }
        }

        private void SaveName()
        {
            Profile.LastPlayerName = _playerName;
            if (_scoreRank >= 0)
            {
                Profile.HighScores[_difficulty][_scoreRank] = Profile.HighScores[_difficulty][_scoreRank] with
                {
                    Name = _playerName
                };
            }

            Persist();
            ShowScores(false);
        }

        private void Exit()
        {
            SaveProgress();
            Screen = GameScreen.Outro;
            _screenSeconds = 0;
        }
        private void Persist()
        {
            _store?.Save(Profile);
        }
        private string Localize(string key)
        {
            return _strings.TryGetText(key, out string text) ? text : string.Empty;
        }

        private MenuItem Item(string id, string key, double x, double y)
        {
            return new(id, Localize(key), Localize(key + "TTip"), x, y);
        }

        private void Cue(string alias)
        {
            _pendingSounds.Add(new SoundCue(alias, 0, 0, true));
        }
        private void AddLines(string text, double x, double y)
        {
            foreach (string line in text.Split('\n'))
            {
                _extras.Add(new ScreenText(line, x, y, "MenuFntEncTxt"));
                y += 20;
            }
        }
        private static DifficultySpeeds DifficultyOf(int value)
        {
            return value == 0 ? DifficultySpeeds.DifficultyZero : value == 2 ? DifficultySpeeds.Timed : DifficultySpeeds.Standard;
        }
    }
}
