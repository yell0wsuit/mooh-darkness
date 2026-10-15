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
    /// The objects within 6 columns and 5 rows of the player (the screen's cells around
    /// <c>CNT_WrdBase</c>) are ordered by their distance in steps from the player. Each picks at
    /// random a side it lies toward, starts on the opposite edge of the view (clipped to the
    /// level) and flies straight to its cell. The first launches at <c>CNT_LvlBldDTime</c> =
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
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public LevelBuild(WorldGrid world, AnimationLibrary library, Random random)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(library);
            ArgumentNullException.ThrowIfNull(random);
            _random = random;
            _landingSound = library.Sound("SndLvlBld");
            if (world.Player is not Entity player)
            {
                return;
            }

            _player = player.Id;
            GridPosition center = player.Position;
            List<(int Distance, Entity Entity)> inView = [];
            foreach (Entity entity in world.Entities)
            {
                // The session creates this door eagerly; the original creates it in GameInit,
                // after construction. Keep it out of the flights and hidden until then.
                if (entity.DoorBody is not null)
                {
                    _ = _entryDoors.Add(entity.Id);
                    continue;
                }

                int dx = entity.Position.X - center.X;
                int dy = entity.Position.Y - center.Y;
                if (entity.Id != player.Id
                    && dx >= -ColumnsAround && dx < ColumnsAround + 1
                    && dy >= -RowsAround && dy < RowsAround + 1)
                {
                    inView.Add((Math.Abs(dx) + Math.Abs(dy), entity));
                }
            }

            if (inView.Count == 0)
            {
                return;
            }

            (int left, int top, int right, int bottom) = (
                Math.Max(0, center.X - ColumnsAround),
                Math.Max(0, center.Y - RowsAround),
                Math.Min(world.Width - 1, center.X + ColumnsAround),
                Math.Min(world.Height - 1, center.Y + RowsAround));
            double launch = FirstLaunchMilliseconds;
            double gap = FirstGapMilliseconds;
            double speed = FirstSpeed;
            double gapStep = (LastGapMilliseconds - FirstGapMilliseconds) / inView.Count;
            double speedStep = (LastSpeed - FirstSpeed) / inView.Count;
            foreach ((_, Entity entity) in inView.OrderBy(item => item.Distance))
            {
                GridPosition cell = entity.Position;
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

                Direction side = sides[random.Next(sides.Count)];
                (double startX, double startY) = side switch
                {
                    Direction.Left => (right, cell.Y),
                    Direction.Right => (left, cell.Y),
                    Direction.Up => (cell.X, bottom),
                    Direction.Down => (cell.X, top),
                    _ => (cell.X, cell.Y),
                };
                _flights[entity.Id] = new Flight(cell, side, Math.Round(launch), speed, startX, startY);
                launch += gap;
                gap += gapStep;
                speed += speedStep;
            }
        }

        /// <summary>Gets whether every object has landed, so play can start.</summary>
        public bool IsDone => _flights.Values.All(flight => flight.Landed);

        /// <summary>Gets the landings' sounds of the last step.</summary>
        public IReadOnlyList<SoundCue> Sounds => _sounds;

        /// <summary>Moves the fly-in on.</summary>
        /// <param name="seconds">The elapsed time.</param>
        public void Advance(double seconds)
        {
            double milliseconds = seconds * 1000;
            _milliseconds += milliseconds;
            List<SoundCue> sounds = [];
            foreach (Flight flight in _flights.Values)
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
                double jerk = 3 + (_random.NextDouble() * 2);
                flight.Jerk.Push(step.X * jerk, step.Y * jerk);
                sounds.Add(new SoundCue(_landingSound, flight.Cell.X, flight.Cell.Y));
            }

            _sounds = sounds;
        }

        /// <summary>Puts every object in place at once, as a key press does, <c>LvlBldObjFit</c>.</summary>
        public void Finish()
        {
            _sounds = [];
            foreach (Flight flight in _flights.Values.Where(flight => !flight.Landed))
            {
                Land(flight);
            }
        }

        /// <summary>Gets whether an entity is not drawn yet: the player until the end, an object until it launches.</summary>
        internal bool Hides(EntityId id)
        {
            return id == _player || _entryDoors.Contains(id) ? !IsDone : _flights.TryGetValue(id, out Flight? flight) && !flight.Shown;
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

        private sealed class Flight(GridPosition cell, Direction side, double launch, double speed, double startX, double startY)
        {
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
            public DampedSpring Jerk { get; } = new(0.01, -0.02);
        }
    }
}
