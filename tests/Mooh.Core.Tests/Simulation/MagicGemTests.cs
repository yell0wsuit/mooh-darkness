using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Motion;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies the recovered magic gem, <c>_DoMagic</c>: the gem dies and works each cell of its
    /// 3x3 after its Chebyshev distance times 200 ms. <c>MagicWork</c> leaves a treasure cell
    /// alone, otherwise kills the cell's explodable occupants and fills the cell with a random
    /// diamond if nothing still and visible remains, under a 500 ms kill lock.
    /// </summary>
    public sealed class MagicGemTests
    {
        /// <summary>Treasure protects its committed cell while a movement segment is still in progress.</summary>
        [Fact]
        public void MovingTreasureIsLeftAlone()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            _ = Add(world, "OMagic", 4, 2);
            Entity coin = Add(world, "OCoin01", 5, 1);
            KinematicsSystem.Start(coin, MotionCause.PlayerPushed, Direction.Right, 0.01);
            _ = Add(world, "ORock01", 4, 0);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(800));

            Assert.Contains(coin, world.Entities);
            Assert.DoesNotContain(new GridPosition(5, 1), SpawnedCells(events));
        }

        /// <summary>A shadow occupant still prevents a diamond from spawning in its cell.</summary>
        [Fact]
        public void MagicDoesNotSpawnIntoAMessageTable()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            _ = Add(world, "OMagic", 4, 2);
            Entity table = Add(world, "OTable", 5, 2);
            _ = Add(world, "ORock01", 4, 0);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(800));

            Assert.Contains(table, world.Entities);
            Assert.DoesNotContain(table.Position, SpawnedCells(events));
        }

        /// <summary>Verifies a landing shake works the center at once and the ring 200 ms later.</summary>
        [Fact]
        public void AShakeFillsTheSurroundingsWithDiamonds()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            Entity gem = Add(world, "OMagic", 4, 2);
            Entity rock = Add(world, "ORock01", 4, 0);

            List<SimulationEvent> events = [.. simulation.Advance(TimeSpan.FromMilliseconds(560))];
            GridPosition[] centerWave = SpawnedCells(events);
            events.AddRange(simulation.Advance(TimeSpan.FromMilliseconds(200)));

            Assert.Contains(events, item => item == new EntityDestroyed(gem.Id, gem.Id));
            Assert.Equal([new(4, 2)], centerWave);
            Assert.Equal(
                [new(4, 2), new(3, 1), new(4, 1), new(5, 1), new(3, 2), new(5, 2)],
                SpawnedCells(events));
            Assert.Contains(events, item => item == new EntityDestroyed(rock.Id, gem.Id));
            Assert.DoesNotContain(events, item => item is ExplosionCellReached);
            _ = Assert.Single(events, item => item is MagicActivated);
        }

        /// <summary>Verifies treasure cells are skipped and still terrain is kept, not filled.</summary>
        [Fact]
        public void TreasureAndTerrainCellsAreLeftAlone()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            _ = Add(world, "OMagic", 4, 2);
            Entity coin = Add(world, "OCoin01", 5, 2);
            Entity earth = Add(world, "CellsEat01", 3, 2);
            _ = Add(world, "ORock01", 4, 0);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(800));

            GridPosition[] spawned = SpawnedCells(events);
            Assert.DoesNotContain(new GridPosition(5, 2), spawned);
            Assert.Contains(coin, world.Entities);
            Assert.Contains(events, item => item is EntityDestroyed destroyed && destroyed.EntityId == earth.Id);
            Assert.Contains(new GridPosition(3, 2), spawned);
            Assert.DoesNotContain(new GridPosition(4, 3), spawned);
        }

        /// <summary>Verifies a blast destroys a gem outright, because its <c>OnExpl</c> is <c>ObjKill</c>.</summary>
        [Fact]
        public void ABlastDestroysTheGemWithoutMagic()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            Entity gem = Add(world, "OMagic", 4, 2);
            Entity bomb = new(
                EntityId.New(),
                new GridPosition(5, 2),
                InteractionMask.None,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            world.Add(bomb);

            _ = simulation.Detonate(bomb.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(600));

            Assert.Empty(SpawnedCells(events));
            Assert.DoesNotContain(gem, world.Entities);
        }

        /// <summary>Verifies a landing gem works its own surroundings.</summary>
        [Fact]
        public void ALandingGemWorksItsSurroundings()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            Entity gem = Add(world, "OMagic", 4, 0);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(2));

            Assert.Contains(events, item => item == new EntityDestroyed(gem.Id, gem.Id));
            Assert.Contains(new GridPosition(4, 2), SpawnedCells(events));
        }

        /// <summary>
        /// Verifies the spawned diamonds survive a blast inside their recovered 500 ms kill lock: a
        /// bomb in the ring is worked at 200 ms and its own blast reaches the diamonds at 400 ms.
        /// </summary>
        [Fact]
        public void SpawnedDiamondsSurviveABlastInsideTheirKillLock()
        {
            (SimulationWorld simulation, WorldGrid world) = Create();
            _ = Add(world, "OMagic", 4, 2);
            _ = Add(world, "WBomb", 5, 2);
            _ = Add(world, "ORock01", 4, 0);

            List<SimulationEvent> events = [.. simulation.Advance(TimeSpan.FromMilliseconds(560))];
            events.AddRange(simulation.Advance(TimeSpan.FromMilliseconds(400)));

            EntityId[] spawned = [.. events.OfType<MagicDiamondSpawned>().Select(item => item.DiamondEntityId)];
            Assert.NotEmpty(spawned);
            Assert.Contains(events, item => item is ExplosionCellReached { Position: { X: 4, Y: 2 } });
            Assert.All(spawned, id => Assert.Contains(world.Entities, entity => entity.Id == id));
        }

        private static GridPosition[] SpawnedCells(IEnumerable<SimulationEvent> events)
        {
            return [.. events.OfType<MagicDiamondSpawned>().Select(item => item.Position)];
        }

        private static Entity Add(WorldGrid world, string type, int x, int y)
        {
            Entity entity = LevelMaterializer.CreateRuntimeObject(
                EntityId.New(),
                new GridPosition(x, y),
                type,
                DifficultySpeeds.Standard);
            world.Add(entity);
            return entity;
        }

        /// <summary>Builds a 9x4 world with a metal floor on its bottom row.</summary>
        private static (SimulationWorld Simulation, WorldGrid World) Create()
        {
            WorldGrid world = new(9, 4);
            for (int x = 0; x < 9; x++)
            {
                _ = Add(world, "CellsMetal01", x, 3);
            }

            SimulationWorld simulation = new(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                Undrained.Standard);
            return (simulation, world);
        }
    }
}
