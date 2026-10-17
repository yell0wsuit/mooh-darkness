using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Audio;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// The level's fly-in before play, <c>__GSTLvlInInit</c> and <c>__GSTLvlIn</c>: every object in
    /// view flies in from the view's edge to its cell, nearest to the player first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original view covers 6 columns and 5 rows around the player. An expanded camera
    /// includes its full visible area and generated scenery, sharing the original launch
    /// window. Objects are ordered by their distance in steps from the player. Each picks at
    /// random a side it lies toward, starts on the opposite edge of the view and flies straight
    /// to its cell. The original view clips its starting edges to the level; an expanded view
    /// includes the exterior. The first launches at <c>CNT_LvlBldDTime</c> =
    /// 750 ms and the rest follow 40 ms apart, the gap closing toward 15 ms and the speed rising
    /// from 0.02 toward 0.06 cells/ms over the list. On landing an object jerks 3 to 5 px along
    /// its flight on the <c>CNT_GPrmFallThisStiffK</c> spring and plays <c>SndLvlBld</c>. The
    /// player stays hidden until every object has landed or a key puts them all in place.
    /// </para>
    /// <para>
    /// An object already on the edge it would start from lands at its launch; the original would
    /// send it away from its cell, leaving the fly-in to a key press.
    /// </para>
    /// </remarks>
    public sealed class LevelBuild
    {
        private const double FirstLaunchMilliseconds = 750;
        private const double FirstGapMilliseconds = 40;
        private const double LastGapMilliseconds = 15;
        private const double FirstSpeed = 0.02;
        private const double LastSpeed = 0.06;
        private const int ColumnsAround = 6;
        private const int RowsAround = 5;

        private readonly Dictionary<EntityId, Flight> _flights = [];
        private readonly Dictionary<GridPosition, Flight> _sceneryFlights = [];
        private readonly List<Flight> _allFlights = [];
        private readonly WorldGrid _world;
        private readonly GridPosition _center;
        private readonly int _referenceCount;
        private readonly Random _sceneryRandom = new(0);
        private (int Left, int Top, int Right, int Bottom)? _plannedBounds;
        private ExteriorScenery? _plannedScenery;
        private readonly EntityId? _player;
        private readonly HashSet<EntityId> _entryDoors = [];
        private readonly string _landingSound;
        private readonly Random _random;
        private List<SoundCue> _sounds = [];
        private double _milliseconds;

        /// <summary>Plans the fly-in of a level at its start.</summary>
        /// <param name="world">The level's world.</param>
        /// <param name="library">The recovered sound names.</param>
        /// <param name="random">The source of the sides and the jerks.</param>
        /// <param name="camera">The expanded view to construct, or the original view when omitted.</param>
        /// <param name="scenery">The generated exterior to include in the fly-in.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public LevelBuild(WorldGrid world, AnimationLibrary library, Random random, SceneCamera? camera = null, ExteriorScenery? scenery = null)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(library);
            ArgumentNullException.ThrowIfNull(random);
            _random = random;
            _world = world;
            _landingSound = library.Sound("SndLvlBld");
            if (world.Player is not Entity player)
            {
                IsDone = true;
                return;
            }

            _player = player.Id;
            _center = player.Position;
            _referenceCount = world.Entities.Count(entity => entity.Id != player.Id && entity.DoorBody is null
                && Math.Abs(entity.Position.X - _center.X) <= ColumnsAround
                && Math.Abs(entity.Position.Y - _center.Y) <= RowsAround);
            IncludeView(camera, scenery);
            IsDone = _allFlights.Count == 0;
        }

        /// <summary>Schedules newly exposed cells during a resize, preserving all existing flights.</summary>
        /// <param name="camera">The current expanded view, or the original view when omitted.</param>
        /// <param name="scenery">The exterior to construct alongside authored objects.</param>
        public void IncludeView(SceneCamera? camera, ExteriorScenery? scenery)
        {
            if (IsDone || _player is null)
            {
                return;
            }
            GridPosition center = _center;
            int left = center.X - ColumnsAround;
            int top = center.Y - RowsAround;
            int right = center.X + ColumnsAround;
            int bottom = center.Y + RowsAround;
            if (camera is not null)
            {
                (double baseX, double baseY) = camera.Base;
                left = Math.Min(left, (int)Math.Floor((-camera.HorizontalMargin - baseX) / SceneCamera.CellWidth) - 2);
                top = Math.Min(top, (int)Math.Floor((-camera.VerticalMargin - baseY) / SceneCamera.CellHeight) - 2);
                right = Math.Max(right, (int)Math.Ceiling((800 + camera.HorizontalMargin - baseX) / SceneCamera.CellWidth) + 2);
                bottom = Math.Max(bottom, (int)Math.Ceiling((600 + camera.VerticalMargin - baseY) / SceneCamera.CellHeight) + 2);
            }
            (int Left, int Top, int Right, int Bottom) bounds = (left, top, right, bottom);
            if (_plannedBounds is { } planned && planned == bounds && ReferenceEquals(_plannedScenery, scenery))
            {
                return;
            }
            _plannedBounds = bounds;
            _plannedScenery = scenery;
            List<(EntityId? Id, GridPosition Cell)> inView = [];
            foreach (Entity entity in _world.Entities)
            {
                // The session creates this door eagerly; the original creates it in GameInit,
                // after construction. Keep it out of the flights and hidden until then.
                if (entity.DoorBody is not null)
                {
                    _ = _entryDoors.Add(entity.Id);
                    continue;
                }

                if (entity.Id != _player && !_flights.ContainsKey(entity.Id)
                    && entity.Position.X >= left && entity.Position.X <= right
                    && entity.Position.Y >= top && entity.Position.Y <= bottom)
                {
                    inView.Add((entity.Id, entity.Position));
                }
            }

            if (camera is not null && scenery is not null)
            {
                foreach (SceneSprite sprite in scenery.Layout(camera))
                {
                    GridPosition cell = new((int)sprite.X, (int)sprite.Y);
                    if (!_sceneryFlights.ContainsKey(cell))
                    {
                        inView.Add((null, cell));
                    }
                }
            }

            if (inView.Count == 0)
            {
                return;
            }

            if (camera is null)
            {
                left = Math.Max(0, left);
                top = Math.Max(0, top);
                right = Math.Min(_world.Width - 1, right);
                bottom = Math.Min(_world.Height - 1, bottom);
            }
            double launch = Math.Max(FirstLaunchMilliseconds, _milliseconds + FirstGapMilliseconds);
            // More visible cells share the original view's launch window instead of
            // multiplying the wait on ultrawide and tall screens.
            double scale = camera is null ? 1 : Math.Min(1, Math.Max(1, _referenceCount) / (double)inView.Count);
            double gap = FirstGapMilliseconds * scale;
            double speed = FirstSpeed;
            double gapStep = (LastGapMilliseconds - FirstGapMilliseconds) * scale / inView.Count;
            double speedStep = (LastSpeed - FirstSpeed) / inView.Count;
            foreach ((EntityId? id, GridPosition cell) in inView.OrderBy(item => Math.Abs(item.Cell.X - center.X) + Math.Abs(item.Cell.Y - center.Y)))
            {
                List<Direction> sides = [];
                if (cell.X <= center.X)
                {
                    sides.Add(Direction.Left);
                }

                if (center.X <= cell.X)
                {
                    sides.Add(Direction.Right);
                }

                if (cell.Y <= center.Y)
                {
                    sides.Add(Direction.Up);
                }

                if (center.Y <= cell.Y)
                {
                    sides.Add(Direction.Down);
                }

                Random random = id is null ? _sceneryRandom : _random;
                Direction side = sides[random.Next(sides.Count)];
                (double startX, double startY) = side switch
                {
                    Direction.Left => (right, cell.Y),
                    Direction.Right => (left, cell.Y),
                    Direction.Up => (cell.X, bottom),
                    Direction.Down => (cell.X, top),
                    _ => (cell.X, cell.Y),
                };
                Flight flight = new(cell, side, Math.Round(launch), speed, startX, startY, random, id is not null);
                if (id is EntityId entityId)
                {
                    _flights[entityId] = flight;
                }
                else
                {
                    _sceneryFlights[cell] = flight;
                }
                _allFlights.Add(flight);
                launch += gap;
                gap += gapStep;
                speed += speedStep;
            }
        }

        /// <summary>Gets whether every object has landed, so play can start.</summary>
        public bool IsDone
        {
            get; private set;
        }

        /// <summary>Gets the landings' sounds of the last step.</summary>
        public IReadOnlyList<SoundCue> Sounds => _sounds;

        /// <summary>Moves the fly-in on.</summary>
        /// <param name="seconds">The elapsed time.</param>
        public void Advance(double seconds)
        {
            double milliseconds = seconds * 1000;
            _milliseconds += milliseconds;
            List<SoundCue> sounds = [];
            foreach (Flight flight in _allFlights)
            {
                if (flight.Landed)
                {
                    flight.Jerk.Advance(milliseconds);
                    continue;
                }

                double flying = _milliseconds - flight.LaunchMilliseconds;
                if (flying < 0)
                {
                    continue;
                }

                GridPosition step = new GridPosition(0, 0).Offset(flight.Side);
                double distance = Math.Abs(flight.Cell.X - flight.StartX) + Math.Abs(flight.Cell.Y - flight.StartY);
                double travelled = flight.Speed * flying;
                if (travelled < distance)
                {
                    flight.Shown = true;
                    (flight.X, flight.Y) = (flight.StartX + (step.X * travelled), flight.StartY + (step.Y * travelled));
                    continue;
                }

                Land(flight);
                double jerk = 3 + (flight.Random.NextDouble() * 2);
                flight.Jerk.Push(step.X * jerk, step.Y * jerk);
                if (flight.PlaySound)
                {
                    sounds.Add(new SoundCue(_landingSound, flight.Cell.X, flight.Cell.Y));
                }
            }

            _sounds = sounds;
            IsDone = _allFlights.All(flight => flight.Landed);
        }

        /// <summary>Puts every object in place at once, as a key press does, <c>LvlBldObjFit</c>.</summary>
        public void Finish()
        {
            _sounds = [];
            foreach (Flight flight in _allFlights)
            {
                Land(flight);
                flight.Jerk = new DampedSpring(0.01, -0.02);
            }
            IsDone = true;
        }

        /// <summary>Places or hides one generated tile using the same flight and landing spring as authored objects.</summary>
        internal SceneSprite? PresentScenery(SceneSprite sprite)
        {
            return !_sceneryFlights.TryGetValue(new GridPosition((int)sprite.X, (int)sprite.Y), out Flight? flight)
                ? IsDone ? sprite : null
                : !flight.Shown ? null : flight.Landed
                ? sprite with
                {
                    X = sprite.X + (flight.Jerk.X / SceneCamera.CellWidth),
                    Y = sprite.Y + (flight.Jerk.Y / SceneCamera.CellHeight)
                }
                : sprite with
                {
                    X = flight.X,
                    Y = flight.Y,
                    Pass = 1
                };
        }

        /// <summary>Drops completed scenery's cell keys once world rotation remaps the exterior.</summary>
        internal void ForgetSceneryFlights()
        {
            _sceneryFlights.Clear();
        }

        /// <summary>Hides planned objects until launch and unplanned offscreen objects until construction finishes.</summary>
        internal bool Hides(EntityId id)
        {
            return id == _player || _entryDoors.Contains(id) ? !IsDone
                : _flights.TryGetValue(id, out Flight? flight) ? !flight.Shown : !IsDone;
        }

        /// <summary>Gets where an object is drawn during the fly-in, or <see langword="null"/> where it rests.</summary>
        internal (double X, double Y)? PositionOf(Entity entity)
        {
            (double x, double y) = DrawnPosition.Of(entity);
            return !_flights.TryGetValue(entity.Id, out Flight? flight)
                ? null
                : flight.Landed
                    ? flight.Jerk.X == 0 && flight.Jerk.Y == 0
                        ? null
                        : (x + (flight.Jerk.X / SceneCamera.CellWidth), y + (flight.Jerk.Y / SceneCamera.CellHeight))
                    : (flight.X, flight.Y);
        }

        /// <summary>Gets whether an object is in flight, which draws it over the rest, <c>RPass = 1</c>.</summary>
        internal bool IsFlying(EntityId id)
        {
            return _flights.TryGetValue(id, out Flight? flight) && flight.Shown && !flight.Landed;
        }

        private static void Land(Flight flight)
        {
            flight.Landed = true;
            flight.Shown = true;
            (flight.X, flight.Y) = (flight.Cell.X, flight.Cell.Y);
        }

        private sealed class Flight(GridPosition cell, Direction side, double launch, double speed, double startX, double startY, Random random, bool playSound)
        {
            public Random Random { get; } = random;

            public bool PlaySound { get; } = playSound;
            public GridPosition Cell { get; } = cell;

            public Direction Side { get; } = side;

            public double LaunchMilliseconds { get; } = launch;

            public double Speed { get; } = speed;

            public double StartX { get; } = startX;

            public double StartY { get; } = startY;

            public double X { get; set; } = startX;

            public double Y { get; set; } = startY;

            public bool Shown
            {
                get; set;
            }

            public bool Landed
            {
                get; set;
            }

            /// <summary>The landing jerk, <c>CNT_GPrmFallThisStiffK</c>: ω 0.01 and λ −0.02 per millisecond.</summary>
            public DampedSpring Jerk { get; set; } = new(0.01, -0.02);
        }
    }
}
