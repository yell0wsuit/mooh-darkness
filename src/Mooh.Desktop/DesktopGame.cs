using System;
using System.Collections.Generic;

using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Player;
using Mooh.Core.Session;
using Mooh.Core.Simulation;
using Mooh.Desktop.Platform.Input;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Audio;
using Mooh.Presentation.Hud;
using Mooh.Presentation.Scene;
using Mooh.Presentation.Sprites;
using Mooh.Presentation.Text;

namespace Mooh.Desktop
{
    /// <summary>
    /// Drives a game session from the keyboard and wall-clock time, and keeps its presentation —
    /// the animated scene and the camera — in step with it.
    /// </summary>
    /// <param name="session">The session to play.</param>
    /// <param name="library">The recovered animation tables.</param>
    /// <param name="random">The source of the presentation's random choices.</param>
    /// <param name="strings">The recovered text table the help panel and level texts read, if loaded.</param>
    /// <param name="levelFont">The font the level texts are laid out in, if loaded.</param>
    /// <param name="masks">The sprites' solid pixels, which the pickup sparks need, if loaded.</param>
    /// <param name="automaticProgression">Whether the standalone gameplay driver advances without shell screens.</param>
    internal sealed class DesktopGame(
        GameSession session,
        AnimationLibrary library,
        Random random,
        StringTable? strings = null,
        BitmapFont? levelFont = null,
        ISpriteMask? masks = null,
        bool automaticProgression = true)
    {
        /// <summary>
        /// The longest frame the game accepts. A stall — a window drag, a debugger — would otherwise
        /// leap the level forward on the next frame.
        /// </summary>
        private const double LongestFrameSeconds = 0.1;

        /// <summary>How long a decided level stays on screen before the next one, or a replay, starts.</summary>
        private const double OutcomeSeconds = 2;

        /// <summary>The size of the <c>BackA_1_01</c> backdrop image.</summary>
        private static readonly (int Width, int Height) BackdropImageSize = (1024, 768);

        private readonly AnimationLibrary _library = library;
        private readonly Random _random = random;
        private readonly StringTable? _strings = strings;
        private readonly BitmapFont? _levelFont = levelFont;
        private readonly ISpriteMask? _masks = masks;
        private double _outcomeSeconds;
        private bool _started;
        private bool _skipBuild;
        private LevelBuild? _build;

        /// <summary>Gets the session.</summary>
        public GameSession Session { get; } = session;

        /// <summary>Gets the animated scene of the current level.</summary>
        public ScenePresenter Presenter { get; private set; } = new(library, random);

        /// <summary>Gets the current level's backdrop.</summary>
        public Backdrop Backdrop { get; private set; } = new(BackdropStyle.Normal, 1, 1, BackdropImageSize, random);

        /// <summary>Gets the screen panels of the current level.</summary>
        public HudPresenter Hud { get; private set; } = new(session.Level.World, strings, random, levelFont);

        /// <summary>Gets the camera over the current level.</summary>
        public SceneCamera Camera { get; private set; } = new();

        /// <summary>Gets active level time, excluding construction, pause and outcome screens.</summary>
        public double PlaySeconds
        {
            get; private set;
        }

        /// <summary>Gets elapsed presentation time while gameplay is not paused.</summary>
        public double ElapsedSeconds
        {
            get; private set;
        }

        /// <summary>Gets the sounds this frame cued, including a bonus expiring while paused.</summary>
        public IReadOnlyList<SoundCue> Sounds { get; private set; } = [];

        /// <summary>Gets whether the level is still flying into view before play.</summary>
        public bool IsBuilding => !_started || _build?.IsDone == false;

        /// <summary>Gets whether the outcome sequence has finished and the shell may change screens.</summary>
        public bool OutcomeReady
        {
            get; private set;
        }

        /// <summary>Gets a value indicating whether the game is paused.</summary>
        public bool IsPaused
        {
            get; private set;
        }

        /// <summary>Runs one frame.</summary>
        /// <param name="input">The held directions and fire key.</param>
        /// <param name="commands">The keyboard commands given since the last frame.</param>
        /// <param name="seconds">The wall-clock time since the last frame.</param>
        /// <param name="anyKeyHeld">Whether any keyboard key is held, including keys outside the player controls.</param>
        public void Update(PlayerInputState input, IReadOnlyList<ControlCommand> commands, double seconds, bool anyKeyHeld = false)
        {
            ArgumentNullException.ThrowIfNull(commands);
            Sounds = [];
            if (!_started)
            {
                _started = true;
                StartLevelPresentation();
            }

            List<SimulationEvent> events = [];
            foreach (ControlCommand command in commands)
            {
                if (command.Kind == ControlCommandKind.TogglePause)
                {
                    IsPaused = !IsPaused;
                }
                else if (!IsPaused && !IsBuilding && Session.State == GameSessionState.Playing)
                {
                    events.AddRange(command.Kind == ControlCommandKind.NextWeapon
                        ? Session.Simulation.SelectNextWeapon()
                        : Session.Simulation.SelectWeapon(command.Weapon!.Value));
                }
            }

            seconds = Math.Clamp(seconds, 0, LongestFrameSeconds);
            if (IsPaused)
            {
                events.AddRange(Session.Simulation.AdvanceBonusTimers(TimeSpan.FromSeconds(seconds)));
                _build?.Advance(0);
                Presenter.Update(Session.Level.World, events, 0, CurrentSpin, _build, bonusSeconds: seconds);
                Sounds = Presenter.Sounds;
                Backdrop.Advance(seconds);
                Hud.Update(Session.Level.World, events, Session.TotalScore, seconds, Camera);
                return;
            }

            ElapsedSeconds += seconds;
            GridPosition? pivot = Session.Simulation.WorldRotationPivot;
            if (IsBuilding)
            {
                bool held = anyKeyHeld || input != default;
                if (_skipBuild && !held)
                {
                    _build!.Finish();
                }

                _skipBuild |= held;
                _build!.Advance(seconds);
                if (_build.IsDone)
                {
                    Hud.ShowLevelTitle(Session.LevelIndex + 1);
                }
            }
            else if (Session.State == GameSessionState.Playing)
            {
                _build?.Advance(seconds);
                PlaySeconds += seconds;
                Session.SetPlayerInput(input);
                events.AddRange(Session.Advance(TimeSpan.FromSeconds(seconds)));
            }
            else
            {
                events.AddRange(Session.AdvanceOutcome(TimeSpan.FromSeconds(seconds)));
                double wait = Session.State is GameSessionState.LevelCompleted or GameSessionState.CampaignCompleted
                    ? 3 : OutcomeSeconds;
                if ((_outcomeSeconds += seconds) >= wait)
                {
                    OutcomeReady = true;
                    if (automaticProgression)
                    {
                        MoveOn();
                        return;
                    }
                }
            }

            FollowWorldRotation(events, pivot);
            Presenter.Update(Session.Level.World, events, seconds, CurrentSpin, _build);
            Sounds = Presenter.Sounds;
            Backdrop.Advance(seconds);
            if (Session.Level.World.Player is Entity player)
            {
                (double x, double y) = DrawnPosition.Of(player);
                Camera.Follow(x, y, seconds);
            }

            Camera.Shake = Presenter.Shake;

            Hud.Update(Session.Level.World, events, Session.TotalScore, seconds, Camera);
        }

        /// <summary>Gets the same world transform for active and paused frames.</summary>
        private WorldSpin? CurrentSpin => Session.Simulation.WorldRotationInFlight is WorldRotationDirection turning
            && Session.Simulation.WorldRotationPivot is GridPosition center
                ? WorldSpin.Of(center, turning, Session.Simulation.WorldRotationProgress)
                : null;

        /// <summary>
        /// Keeps the view still across a turn, as <c>WorldRotStopCBack</c> does: the world base
        /// moves by however far the turn moved the rotator, and the backdrop refits the new size.
        /// </summary>
        private void FollowWorldRotation(IReadOnlyList<SimulationEvent> events, GridPosition? pivot)
        {
            foreach (WorldRotationCompleted turned in events.OfType<WorldRotationCompleted>())
            {
                if (pivot is GridPosition before)
                {
                    GridPosition after = Session.Level.World.GetEntity(turned.EntityId).Position;
                    Camera.Shift(
                        -(after.X - before.X) * SceneCamera.CellWidth,
                        -(after.Y - before.Y) * SceneCamera.CellHeight);
                }

                Backdrop.Resize(turned.Width, turned.Height);
            }
        }

        /// <summary>
        /// Starts the next level after a win and replays the level after a loss. A finished
        /// campaign stays on its last frame.
        /// </summary>
        private void MoveOn()
        {
            if (Session.State == GameSessionState.LevelCompleted)
            {
                Session.AdvanceToNextLevel();
            }
            else if (Session.State == GameSessionState.LevelFailed)
            {
                Session.RestartLevel();
            }
            else
            {
                return;
            }

            StartLevelPresentation();
        }

        private void StartLevelPresentation()
        {
            _outcomeSeconds = 0;
            PlaySeconds = 0;
            OutcomeReady = false;
            Presenter = new ScenePresenter(_library, _random, _strings, _masks);
            Hud = new HudPresenter(Session.Level.World, _strings, _random, _levelFont);
            _skipBuild = false;
            _build = new LevelBuild(Session.Level.World, _library, _random);
            if (_build.IsDone)
            {
                Hud.ShowLevelTitle(Session.LevelIndex + 1);
            }
            Camera = new SceneCamera
            {
                HorizontalMargin = Camera.HorizontalMargin
            };
            LevelDocument level = Session.Level.Source;
            Backdrop = new Backdrop(
                Backdrop.StyleOf(level.Objects?.Select(item => item.Type ?? string.Empty) ?? [], _library),
                level.Size.Width,
                level.Size.Height,
                BackdropImageSize,
                _random);
            if (Session.Level.World.Player is Entity player)
            {
                (double x, double y) = DrawnPosition.Of(player);
                Camera.Snap(x, y);
            }

            Presenter.Update(Session.Level.World, [], 0, build: _build);
            Hud.Update(Session.Level.World, [], Session.TotalScore, 0, Camera);
        }
    }
}
