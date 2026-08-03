using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies the recovered <c>OWRotL</c>/<c>OWRotR</c> behavior: a hand strike enters
    /// <c>ST_Work</c>, whose <c>AEnd</c> runs <c>GMod.WorldRotStart</c>, and the world then spins
    /// through a quarter turn with the whole object cycle replaced by <c>WorldRotCycle</c>.
    /// </summary>
    public sealed class WorldRotationObjectTests
    {
        /// <summary>
        /// The recovered <c>ST_Work</c> animation: twenty frames at 50 ms, the same in both
        /// rotator types.
        /// </summary>
        private static readonly TimeSpan Windup = TimeSpan.FromMilliseconds(1000);

        /// <summary>
        /// The recovered spin. <c>CNT_GPrm_WorldRotSp</c> is 0.002 and the native
        /// <c>WorldRotStart</c> divides it by <c>pi/2</c> before accumulating it per millisecond,
        /// so the turn completes after <c>pi/2 / 0.002</c> milliseconds.
        /// </summary>
        private static readonly TimeSpan Spin = TimeSpan.FromMilliseconds(Math.PI / 2 / 0.002);

        /// <summary>
        /// Verifies a hand strike is what starts a rotator. The type carries
        /// <c>PlCtrl=GoTo_Work</c> but <c>RPass=0</c>, and <c>DoCtrl</c> only dispatches
        /// <c>PlCtrl</c> to same-cell occupants with a negative <c>RPass</c>, so the only path in
        /// is <c>_AEnd_Str</c>'s dispatch over the struck cell.
        /// </summary>
        [Fact]
        public void StrikingARotatorStartsTheRecoveredWindup()
        {
            WorldGrid world = new(7, 5);
            Entity player = NewPlayer(new GridPosition(1, 1), Direction.Right);
            Entity rotator = NewRotator(new GridPosition(2, 1), WorldRotationDirection.Left);
            world.Add(player);
            world.Add(rotator);
            SimulationWorld simulation = CreateSimulation(world);

            List<SimulationEvent> events = StrikeOnce(simulation, TimeSpan.FromMilliseconds(200));

            Assert.Contains(events, item => item is PlayerStruck);
            Assert.Null(simulation.WorldRotationInFlight);
            Assert.Equal(7, world.Width);
            Assert.True(rotator.WorldRotationBody!.WindupRemainingSeconds is > 0);
        }

        /// <summary>
        /// Verifies the world is still untouched at the end of the windup and the spin has only
        /// just begun, so the 1000 ms are the object's animation rather than the turn itself.
        /// </summary>
        [Fact]
        public void TheWorldIsUnchangedUntilTheWindupEnds()
        {
            WorldGrid world = new(7, 5);
            SimulationWorld simulation = CreateStruckWorld(world, WorldRotationDirection.Left, out _);

            _ = simulation.Advance(Windup - TimeSpan.FromMilliseconds(1));

            Assert.Null(simulation.WorldRotationInFlight);
            Assert.Equal(7, world.Width);

            _ = simulation.Advance(TimeSpan.FromMilliseconds(2));

            Assert.Equal(WorldRotationDirection.Left, simulation.WorldRotationInFlight);
            Assert.Equal(7, world.Width);
        }

        /// <summary>
        /// Verifies the spin lasts the recovered duration and the world turns exactly when it ends.
        /// </summary>
        [Fact]
        public void TheWorldTurnsWhenTheRecoveredSpinEnds()
        {
            WorldGrid world = new(7, 5);
            SimulationWorld simulation = CreateStruckWorld(world, WorldRotationDirection.Left, out _);

            _ = simulation.Advance(Windup + Spin - TimeSpan.FromMilliseconds(1));

            Assert.Equal(7, world.Width);
            Assert.Equal(WorldRotationDirection.Left, simulation.WorldRotationInFlight);

            _ = simulation.Advance(TimeSpan.FromMilliseconds(2));

            Assert.Equal(5, world.Width);
            Assert.Equal(7, world.Height);
            Assert.Null(simulation.WorldRotationInFlight);
        }

        /// <summary>
        /// Verifies the start and completion of the spin are both reported, so a renderer can draw
        /// it and the runner can assert it.
        /// </summary>
        [Fact]
        public void TheSpinIsReportedAsOrderedEvents()
        {
            WorldGrid world = new(7, 5);
            SimulationWorld simulation = CreateStruckWorld(
                world,
                WorldRotationDirection.Left,
                out Entity rotator);

            List<SimulationEvent> events = [.. simulation.Advance(Windup + Spin + TimeSpan.FromMilliseconds(1))];

            WorldRotationStarted started = Assert.Single(events.OfType<WorldRotationStarted>());
            Assert.Equal(rotator.Id, started.EntityId);
            Assert.Equal(WorldRotationDirection.Left, started.Direction);
            Assert.Equal(Spin.TotalMilliseconds, started.Duration.TotalMilliseconds, 6);
            WorldRotationCompleted completed = Assert.Single(events.OfType<WorldRotationCompleted>());
            Assert.Equal(WorldRotationDirection.Left, completed.Direction);
            Assert.Equal(5, completed.Width);
            Assert.Equal(7, completed.Height);
            Assert.True(
                events.IndexOf(started) < events.IndexOf(completed),
                "The spin must be reported before it completes.");
        }

        /// <summary>
        /// Verifies <c>OWRotR</c> turns the other way. <c>GameObjWorldRotRight</c> passes
        /// <c>bLeft=False</c>, which the native reads as a negative speed and so selects the
        /// opposite remap.
        /// </summary>
        [Fact]
        public void ARightRotatorTurnsTheOtherWay()
        {
            WorldGrid world = new(7, 5);
            SimulationWorld simulation = CreateStruckWorld(
                world,
                WorldRotationDirection.Right,
                out Entity rotator);

            _ = simulation.Advance(Windup + Spin + TimeSpan.FromMilliseconds(1));

            // The rotator sits at (2, 1) and a right turn maps it to (y, width - 1 - x).
            Assert.Equal(new GridPosition(1, 4), rotator.Position);
        }

        /// <summary>
        /// Verifies the whole simulation is frozen while the world spins. The recovered
        /// <c>Game.ModCycle</c> replaces the entire object cycle with <c>WorldRotCycle</c>, so no
        /// object moves, no timer runs and the world clock itself never advances.
        /// </summary>
        [Fact]
        public void NothingAdvancesWhileTheWorldSpins()
        {
            WorldGrid world = new(7, 5);
            Entity rock = new(
                EntityId.New(),
                new GridPosition(5, 0),
                InteractionMask.BlocksMovement,
                gravityBody: new GravityBody(DifficultySpeeds.Standard.NormalSpeed, canSlide: false));
            world.Add(rock);
            SimulationWorld simulation = CreateStruckWorld(world, WorldRotationDirection.Left, out _);

            // Reach the spin, then let the rock fall a little before it is frozen.
            _ = simulation.Advance(Windup);
            GridPosition frozenAt = rock.Position;
            double frozenProgress = rock.Motion?.Progress ?? 0;

            _ = simulation.Advance(Spin - TimeSpan.FromMilliseconds(2));

            Assert.Equal(frozenAt, rock.Position);
            Assert.Equal(frozenProgress, rock.Motion?.Progress ?? 0);
        }

        /// <summary>
        /// Verifies the rotator returns to <c>ST_Stay</c> and can be struck again, which is the
        /// recovered <c>WorldRotLeft</c>'s trailing <c>StateSet(ST_Stay)</c>. Seven of the sixteen
        /// levels that place a rotator place more than one.
        /// </summary>
        [Fact]
        public void ARotatorCanBeUsedAgain()
        {
            WorldGrid world = new(7, 5);
            SimulationWorld simulation = CreateStruckWorld(
                world,
                WorldRotationDirection.Left,
                out Entity rotator);
            _ = simulation.Advance(Windup + Spin + TimeSpan.FromMilliseconds(1));

            Assert.Equal(5, world.Width);
            Assert.Null(rotator.WorldRotationBody!.WindupRemainingSeconds);
        }

        /// <summary>
        /// Verifies a cell's lock travels with it, because the original copies each cell's state
        /// into the reallocated array rather than rebuilding it. The world clock is frozen for the
        /// whole spin, so a lock taken before the turn still has its full remainder after it.
        /// </summary>
        [Fact]
        public void ACellLockTravelsWithItsCell()
        {
            WorldGrid world = new(7, 5);
            SimulationWorld simulation = CreateStruckWorld(world, WorldRotationDirection.Left, out _);
            world.Add(NewRock(new GridPosition(4, 1)));
            world.Add(NewLedge(new GridPosition(4, 2)));

            // A left turn maps (5, 1) to (3, 5), which is the cell the rock ends up standing over,
            // so a lock taken here must land there rather than staying at the stale key.
            world.LockCell(new GridPosition(5, 1), TimeSpan.FromMilliseconds(2000));

            _ = simulation.Advance(Windup + Spin + TimeSpan.FromMilliseconds(1));
            Entity rock = world.GetOccupants(new GridPosition(3, 4)).Single();

            Assert.Null(rock.Motion);

            // The world clock is frozen for the whole spin, so the lock still has its remainder.
            _ = simulation.Advance(TimeSpan.FromMilliseconds(1500));

            Assert.NotNull(rock.Motion);
        }

        /// <summary>
        /// Verifies gravity is re-evaluated against the rotated level, which is the whole point of
        /// the mechanic: gravity itself never changes direction, so a rotation is what moves the
        /// level relative to it.
        /// </summary>
        [Fact]
        public void ObjectsFallAgainstTheRotatedLevel()
        {
            WorldGrid world = new(7, 5);
            SimulationWorld simulation = CreateStruckWorld(world, WorldRotationDirection.Left, out _);
            Entity rock = NewRock(new GridPosition(4, 1));
            world.Add(rock);
            world.Add(NewLedge(new GridPosition(4, 2)));

            _ = simulation.Advance(Windup);

            Assert.Null(rock.Motion);

            _ = simulation.Advance(Spin + TimeSpan.FromMilliseconds(1));

            // A left turn maps the rock to (3, 4) and its ledge to (2, 4), so the ledge is now
            // beside the rock rather than under it and nothing holds it up. The rock commits its
            // destination cell the moment it starts falling, so it already reads as (3, 5).
            Assert.NotNull(rock.Motion);
            Assert.Equal(Direction.Down, rock.Motion.TravelDirection);
            Assert.Equal(new GridPosition(3, 5), rock.Position);
            Assert.Equal(new GridPosition(2, 4), world.GetOccupants(new GridPosition(2, 4)).Single().Position);
        }

        /// <summary>
        /// Verifies both placed rotator types materialize with their recovered turn direction.
        /// </summary>
        [Theory]
        [InlineData("OWRotL", WorldRotationDirection.Left)]
        [InlineData("OWRotR", WorldRotationDirection.Right)]
        public void PlacedRotatorsMaterializeWithTheirDirection(
            string archivalType,
            WorldRotationDirection expected)
        {
            Entity entity = LevelMaterializer.CreateRuntimeObject(
                EntityId.New(),
                new GridPosition(1, 1),
                archivalType,
                DifficultySpeeds.Standard);

            Assert.Equal(expected, entity.WorldRotationBody!.Direction);
        }

        /// <summary>
        /// Verifies the corpus reach the rotator work covers. The summary recorded eighteen levels;
        /// the committed corpus actually places thirty rotators across sixteen levels, with eight
        /// of them in <c>ma_33</c> alone.
        /// </summary>
        [Fact]
        public void ThePlacedRotatorCorpusIsExactlyKnown()
        {
            string manifestPath = Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "levels",
                "manifest.json");
            LoadedLevelPack pack = JsonLevelPackLoader.Load(manifestPath);

            MaterializedLevel[] levels = [.. pack.Levels.Select(LevelMaterializer.Materialize)];
            int rotators = levels
                .SelectMany(level => level.World.Entities)
                .Count(entity => entity.WorldRotationBody is not null);
            int levelsWithRotators = levels
                .Count(level => level.World.Entities.Any(entity => entity.WorldRotationBody is not null));

            Assert.Equal(30, rotators);
            Assert.Equal(16, levelsWithRotators);
        }

        /// <summary>
        /// Runs the fire control long enough to land one strike, then releases it, so a single
        /// rotator trigger can be observed without the control repeating.
        /// </summary>
        private static List<SimulationEvent> StrikeOnce(SimulationWorld simulation, TimeSpan duration)
        {
            simulation.SetPlayerInput(Fire);
            List<SimulationEvent> events = [.. simulation.Advance(TimeSpan.FromMilliseconds(10))];
            simulation.SetPlayerInput(default);
            events.AddRange(simulation.Advance(duration));
            return events;
        }

        /// <summary>
        /// Builds a world whose rotator has already been struck and is part way through its
        /// <c>ST_Work</c> windup, with the strike itself out of the way.
        /// </summary>
        private static SimulationWorld CreateStruckWorld(
            WorldGrid world,
            WorldRotationDirection direction,
            out Entity rotator)
        {
            Entity player = NewPlayer(new GridPosition(1, 1), Direction.Right);
            rotator = NewRotator(new GridPosition(2, 1), direction);
            world.Add(player);
            world.Add(rotator);
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(Fire);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(10));
            simulation.SetPlayerInput(default);
            return simulation;
        }

        private static PlayerInputState Fire => new(false, false, false, false, true);

        private static Entity NewRock(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                gravityBody: new GravityBody(DifficultySpeeds.Standard.NormalSpeed, canSlide: false));
        }

        private static Entity NewLedge(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked);
        }

        private static Entity NewRotator(GridPosition position, WorldRotationDirection direction)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.RoundSupport,
                worldRotationBody: new WorldRotationBody(direction));
        }

        private static Entity NewPlayer(GridPosition position, Direction facing)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(DifficultySpeeds.Standard.PlayerSpeed, 1.5, facing),
                healthBody: new HealthBody(100, 100, TimeSpan.FromMilliseconds(650)));
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                DifficultySpeeds.Standard);
        }
    }
}
