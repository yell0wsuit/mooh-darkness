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
    internal sealed class DesktopGame(GameSession session, AnimationLibrary library, Random random, StringTable? strings = null, BitmapFont? levelFont = null)
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
        private double _outcomeSeconds;
        private bool _started;

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

        /// <summary>Gets the game time played so far: the clamped frame times while not paused.</summary>
        public double ElapsedSeconds
        {
            get; private set;
        }

        /// <summary>Gets the sounds this frame cued; none while paused or between levels.</summary>
        public IReadOnlyList<SoundCue> Sounds { get; private set; } = [];

        /// <summary>Gets a value indicating whether the game is paused.</summary>
        public bool IsPaused
        {
            get; private set;
        }

        /// <summary>Runs one frame.</summary>
        /// <param name="input">The held directions and fire key.</param>
        /// <param name="commands">The keyboard commands given since the last frame.</param>
        /// <param name="seconds">The wall-clock time since the last frame.</param>
        public void Update(PlayerInputState input, IReadOnlyList<ControlCommand> commands, double seconds)
        {
            ArgumentNullException.ThrowIfNull(commands);
            Sounds = [];
            List<SimulationEvent> events = [];
            foreach (ControlCommand command in commands)
            {
                if (command.Kind == ControlCommandKind.TogglePause)
                {
                    IsPaused = !IsPaused;
                }
                else if (!IsPaused && Session.State == GameSessionState.Playing)
                {
                    events.AddRange(command.Kind == ControlCommandKind.NextWeapon
                        ? Session.Simulation.SelectNextWeapon()
                        : Session.Simulation.SelectWeapon(command.Weapon!.Value));
                }
            }

            if (!_started)
            {
                _started = true;
                StartLevelPresentation();
            }

            if (IsPaused)
            {
                return;
            }

            seconds = Math.Clamp(seconds, 0, LongestFrameSeconds);
            ElapsedSeconds += seconds;
            GridPosition? pivot = Session.Simulation.WorldRotationPivot;
            if (Session.State == GameSessionState.Playing)
            {
                Session.SetPlayerInput(input);
                events.AddRange(Session.Advance(TimeSpan.FromSeconds(seconds)));
            }
            else if ((_outcomeSeconds += seconds) >= OutcomeSeconds)
            {
                MoveOn();
                return;
            }

            FollowWorldRotation(events, pivot);
            SimulationWorld simulation = Session.Simulation;
            WorldSpin? spin = simulation.WorldRotationInFlight is WorldRotationDirection turning
                && simulation.WorldRotationPivot is GridPosition center
                ? WorldSpin.Of(center, turning, simulation.WorldRotationProgress)
                : null;
            Presenter.Update(Session.Level.World, events, seconds, spin);
            Hud.Update(Session.Level.World, events, Session.TotalScore, seconds);
            Sounds = Presenter.Sounds;
            Backdrop.Advance(seconds);
            if (Session.Level.World.Player is Entity player)
            {
                (double x, double y) = DrawnPosition.Of(player);
                Camera.Follow(x, y, seconds);
            }
        }

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
            Presenter = new ScenePresenter(_library, _random);
            Hud = new HudPresenter(Session.Level.World, _strings, _random, _levelFont);
            Hud.ShowLevelTitle(Session.LevelIndex + 1);
            Camera = new SceneCamera();
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

            Presenter.Update(Session.Level.World, [], 0);
            Hud.Update(Session.Level.World, [], Session.TotalScore, 0);
        }
    }
}
