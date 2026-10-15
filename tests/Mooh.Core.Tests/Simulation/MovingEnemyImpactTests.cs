using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Motion;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>Checks Python arrival callbacks against downward-moving enemy occupants.</summary>
    public sealed class MovingEnemyImpactTests
    {
        /// <summary>Every falling pickup/object dispatches a landing shake against moving support.</summary>
        [Theory]
        [InlineData("ORock01", "MGrB")]
        [InlineData("ORock01", "MBlueB")]
        [InlineData("OWeight", "MGrB")]
        [InlineData("OWeight", "MBlueB")]
        [InlineData("OCoin01", "MGrB")]
        [InlineData("OCoin01", "MBlueB")]
        [InlineData("ODiam01", "MGrB")]
        [InlineData("ODiam01", "MBlueB")]
        [InlineData("OEat01", "MGrB")]
        [InlineData("OEat01", "MBlueB")]
        [InlineData("WBomb", "MGrB")]
        [InlineData("WBomb", "MBlueB")]
        [InlineData("WGrBox", "MGrB")]
        [InlineData("WGrBox", "MBlueB")]
        [InlineData("WROne", "MGrB")]
        [InlineData("WROne", "MBlueB")]
        [InlineData("WRBox", "MGrB")]
        [InlineData("WRBox", "MBlueB")]
        [InlineData("WMinesBox", "MGrB")]
        [InlineData("WMinesBox", "MBlueB")]
        [InlineData("WBBomb", "MGrB")]
        [InlineData("WBBomb", "MBlueB")]
        [InlineData("BCola", "MGrB")]
        [InlineData("BCola", "MBlueB")]
        [InlineData("BIce", "MGrB")]
        [InlineData("BIce", "MBlueB")]
        public void FallingObjectsHitAnEnemyThatIsStillMovingDown(string objectType, string enemyType)
        {
            WorldGrid world = Corridor(12);
            Entity enemy = Runtime(world, enemyType, new GridPosition(3, 2));
            SimulationWorld simulation = Simulate(world);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(Direction.Down, enemy.Motion!.TravelDirection);
            Entity falling = Runtime(world, objectType, new GridPosition(3, 1));

            List<SimulationEvent> events = [];
            Direction? lastDirection = null;
            double lastProgress = 0;
            for (int frame = 0; frame < 1250 && world.Entities.Contains(enemy); frame++)
            {
                lastDirection = enemy.Motion?.TravelDirection;
                lastProgress = enemy.Motion?.Progress ?? 0;
                events.AddRange(simulation.Advance(TimeSpan.FromMilliseconds(1)));
            }

            Assert.Contains(events, item => item is Landed landed && landed.EntityId == falling.Id);
            Assert.Contains(events, item => item is EntityDestroyed destroyed && destroyed.EntityId == enemy.Id);
            Assert.DoesNotContain(world.Entities, entity => entity.Id == enemy.Id);
            Assert.Equal(Direction.Down, lastDirection);
            Assert.InRange(lastProgress, 0.001, 0.999);
        }

        /// <summary>An already-falling body continues through an empty cell regardless of its deadline.</summary>
        [Fact]
        public void FallingArrivalDoesNotWaitOnAnEmptyCellLock()
        {
            WorldGrid world = new(5, 7);
            Entity rock = Runtime(world, "ORock01", new GridPosition(2, 0));
            SimulationWorld simulation = Simulate(world);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(1));
            world.LockCell(new GridPosition(2, 2), TimeSpan.FromSeconds(5));

            _ = simulation.Advance(TimeSpan.FromMilliseconds(535));

            Assert.Equal(new GridPosition(2, 2), rock.Position);
            Assert.Equal(MotionCause.Falling, rock.Motion!.Cause);
        }

        /// <summary>Hard falling weights crush diggable support before deciding whether they landed.</summary>
        [Theory]
        [InlineData("OWeight", true)]
        [InlineData("ORock01", false)]
        public void OnlyHardFallingObjectsCrushDiggableCells(string type, bool crushes)
        {
            WorldGrid world = new(5, 7);
            Entity falling = Runtime(world, type, new GridPosition(2, 0));
            Entity earth = Runtime(world, "CellsEat01", new GridPosition(2, 2));
            SimulationWorld simulation = Simulate(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(540));

            Assert.Equal(crushes, events.Any(item => item is EntityDestroyed destroyed && destroyed.EntityId == earth.Id));
            if (crushes)
            {
                Assert.True(falling.Position.Y >= 2);
                Assert.NotNull(falling.Motion);
            }
            else
            {
                Assert.Equal(new GridPosition(2, 1), falling.Position);
                Assert.Null(falling.Motion);
            }
        }

        /// <summary>A struck rock reaches its stop callback while the contacted enemy is moving down.</summary>
        [Theory]
        [InlineData("MGrB")]
        [InlineData("MBlueB")]
        public void AStruckRockKillsADownwardMovingEnemy(string type)
        {
            WorldGrid world = new(8, 5);
            for (int x = 0; x < 8; x++)
            {
                world.Add(new Entity(EntityId.New(), new GridPosition(x, 2), InteractionMask.BlocksMovement));
            }

            Entity player = new(EntityId.New(), new GridPosition(0, 1), InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right), healthBody: new HealthBody(100, 100, TimeSpan.Zero));
            world.Add(player);
            Entity rock = Runtime(world, "ORock01", new GridPosition(1, 1));
            Entity enemy = Runtime(world, type, new GridPosition(3, 0));
            world.Add(new Entity(EntityId.New(), new GridPosition(4, 0), InteractionMask.BlocksMovement));
            SimulationWorld simulation = Simulate(world);
            simulation.SetPlayerInput(new PlayerInputState(false, false, false, false, Fire: true));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(500));

            Assert.Contains(events, item => item is StrikePushStopped stopped && stopped.EntityId == rock.Id);
            Assert.Contains(events, item => item is EntityDestroyed destroyed && destroyed.EntityId == enemy.Id);
        }

        /// <summary>Moving occupants still block weapon placement; the fire control chooses a hand strike.</summary>
        [Theory]
        [InlineData("MGrB")]
        [InlineData("MBlueB")]
        public void MovingEnemiesBlockGrenadePlacement(string type)
        {
            WorldGrid world = new(8, 6);
            Entity player = new(EntityId.New(), new GridPosition(1, 2), InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right), healthBody: new HealthBody(100, 100, TimeSpan.Zero));
            world.Add(player);
            Entity enemy = Runtime(world, type, new GridPosition(2, 1));
            world.Add(new Entity(EntityId.New(), new GridPosition(3, 1), InteractionMask.BlocksMovement));
            SimulationWorld simulation = Simulate(world);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(new GridPosition(2, 2), enemy.Position);
            simulation.SetPlayerInput(new PlayerInputState(false, false, false, false, Fire: true));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.DoesNotContain(events, item => item is PlayerGrenadeThrowStarted);
            Assert.Equal(PlayerActionKind.StrikeWindup, player.PlayerBody!.Action!.Kind);
        }

        private static Entity Runtime(WorldGrid world, string type, GridPosition cell)
        {
            Entity entity = LevelMaterializer.CreateRuntimeObject(EntityId.New(), cell, type, DifficultySpeeds.Standard);
            world.Add(entity);
            return entity;
        }

        private static WorldGrid Corridor(int height)
        {
            WorldGrid world = new(7, height);
            for (int y = 0; y < height; y++)
            {
                world.Add(new Entity(EntityId.New(), new GridPosition(2, y), InteractionMask.BlocksMovement));
                world.Add(new Entity(EntityId.New(), new GridPosition(4, y), InteractionMask.BlocksMovement));
            }

            return world;
        }

        private static SimulationWorld Simulate(WorldGrid world)
        {
            return new SimulationWorld(world, Direction.Down, new DefaultGravityCollisionPolicy(),
                new Random(1), 0.5, DifficultySpeeds.Standard with
                {
                    HealthDrainPerSecond = 0
                });
        }
    }
}
