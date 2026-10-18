using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies the recovered barrel blast, <c>Game.ObjExplCross</c>: the barrel's own cell is
    /// reached at once, and after <c>CNT_GPrmExplCrossDelay</c> four arms each reach up to
    /// <c>CNT_GPrmExplCrossLen</c> cells, one every <c>CNT_GPrmExplDelay</c>, stopping at the first
    /// cell where a still, visible occupant survives.
    /// </summary>
    public sealed class BarrelCrossBlastTests
    {
        /// <summary>Verifies the arms reach three cells each on the recovered cadence and never a diagonal.</summary>
        [Fact]
        public void ArmsReachThreeCellsOnTheRecoveredCadence()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            Entity barrel = AddBarrel(world, new GridPosition(4, 4));

            IReadOnlyList<SimulationEvent> start = simulation.Detonate(barrel.Id);
            IReadOnlyList<SimulationEvent> beforeArms = simulation.Advance(TimeSpan.FromMilliseconds(149));
            IReadOnlyList<SimulationEvent> first = simulation.Advance(TimeSpan.FromMilliseconds(1));
            IReadOnlyList<SimulationEvent> second = simulation.Advance(TimeSpan.FromMilliseconds(200));
            IReadOnlyList<SimulationEvent> third = simulation.Advance(TimeSpan.FromMilliseconds(200));
            IReadOnlyList<SimulationEvent> after = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(
                [
                    new ExplosionStarted(barrel.Id, new GridPosition(4, 4)),
                    new EntityDestroyed(barrel.Id, barrel.Id),
                    new ExplosionCellReached(barrel.Id, new GridPosition(4, 4)),
                ],
                start);
            Assert.Empty(Reached(beforeArms));
            Assert.Equal([new(3, 4), new(4, 3), new(5, 4)], Reached(first));
            Assert.Equal([new(2, 4), new(4, 2), new(6, 4)], Reached(second));
            Assert.Equal([new(1, 4), new(4, 1), new(7, 4)], Reached(third));
            Assert.Empty(Reached(after));
        }

        /// <summary>Verifies an arm reaches and locks a surviving wall but goes no further.</summary>
        [Fact]
        public void AnArmStopsAtTheFirstSurvivor()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            Entity barrel = AddBarrel(world, new GridPosition(4, 4));
            world.Add(LevelMaterializer.CreateRuntimeObject(
                EntityId.New(),
                new GridPosition(6, 4),
                "CellsMetal01",
                DifficultySpeeds.Standard));

            _ = simulation.Detonate(barrel.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            GridPosition[] reached = Reached(events);
            Assert.Contains(new GridPosition(6, 4), reached);
            Assert.DoesNotContain(new GridPosition(7, 4), reached);
            Assert.Contains(new GridPosition(1, 4), reached);
        }

        /// <summary>Verifies an arm destroys explodable terrain and carries on through the freed cell.</summary>
        [Fact]
        public void AnArmCarriesOnThroughDestroyedTerrain()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            Entity barrel = AddBarrel(world, new GridPosition(4, 4));
            Entity earth = LevelMaterializer.CreateRuntimeObject(
                EntityId.New(),
                new GridPosition(5, 4),
                "CellsEat01",
                DifficultySpeeds.Standard);
            world.Add(earth);

            _ = simulation.Detonate(barrel.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Contains(events, item => item == new EntityDestroyed(earth.Id, barrel.Id));
            Assert.Contains(new GridPosition(7, 4), Reached(events));
        }

        /// <summary>
        /// Verifies a landing barrel cross-blasts straight after its landing shake. Its
        /// <c>OnLand</c> is <c>ObjExplCross</c> itself rather than <c>ExplWork</c>, so no 3x3 cell
        /// is ever reached.
        /// </summary>
        [Fact]
        public void ALandingBarrelCrossBlasts()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            Entity barrel = AddBarrel(world, new GridPosition(4, 1));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(3));

            int landed = events.ToList().FindIndex(item => item == new Landed(barrel.Id, Direction.Down));
            int started = events.ToList().FindIndex(item => item == new ExplosionStarted(barrel.Id, new GridPosition(4, 4)));
            Assert.InRange(landed, 0, started - 1);
            Assert.DoesNotContain(new GridPosition(3, 3), Reached(events));
            Assert.Contains(new GridPosition(4, 1), Reached(events));
        }

        /// <summary>Verifies a 3x3 blast reaching a barrel sets off its cross blast.</summary>
        [Fact]
        public void ABlastReachingABarrelSetsItOff()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            Entity barrel = AddBarrel(world, new GridPosition(4, 4));
            Entity bomb = new(
                EntityId.New(),
                new GridPosition(3, 4),
                InteractionMask.None,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            world.Add(bomb);

            _ = simulation.Detonate(bomb.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Contains(events, item => item == new ExplosionStarted(barrel.Id, new GridPosition(4, 4)));
            Assert.Contains(new GridPosition(7, 4), Reached(events));
        }

        /// <summary>Verifies a player in an arm is hurt by the blast.</summary>
        [Fact]
        public void APlayerInAnArmIsHurt()
        {
            WorldGrid world = new(9, 5);
            Entity player = new(
                EntityId.New(),
                new GridPosition(6, 4),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Down),
                healthBody: new HealthBody(100, 100, TimeSpan.FromMilliseconds(650)));
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world);
            Entity barrel = AddBarrel(world, new GridPosition(4, 4));

            _ = simulation.Detonate(barrel.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(400));

            Assert.Contains(events, item => item == new DamageApplied(
                player.Id,
                barrel.Id,
                DamageKind.Explosion,
                DifficultySpeeds.Standard.ExplosionDamage,
                100 - DifficultySpeeds.Standard.ExplosionDamage));
        }

        private static GridPosition[] Reached(IEnumerable<SimulationEvent> events)
        {
            return [.. events.OfType<ExplosionCellReached>().Select(reached => reached.Position)];
        }

        private static Entity AddBarrel(WorldGrid world, GridPosition position)
        {
            Entity barrel = LevelMaterializer.CreateRuntimeObject(
                EntityId.New(),
                position,
                "OBarel",
                DifficultySpeeds.Standard);
            world.Add(barrel);
            return barrel;
        }

        private static (SimulationWorld Simulation, WorldGrid World) Create()
        {
            WorldGrid world = new(9, 5);
            return (CreateSimulation(world), world);
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                Undrained.Standard);
        }
    }
}
