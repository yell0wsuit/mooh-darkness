using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Shakes;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>
    /// Verifies the recovered tunnel routing of the small beetle's <c>_OnCellCan</c>: a beetle may
    /// fly into a tunnel whose <c>DirMap</c> accepts its direction, and may leave a tunnel only
    /// through a side the tunnel opens.
    /// </summary>
    public sealed class BeetleTunnelTests
    {
        /// <summary>Verifies a beetle flies through a horizontal tunnel to reach the player.</summary>
        [Fact]
        public void ABeetleFliesThroughATunnelThatAcceptsIt()
        {
            (SimulationWorld simulation, Entity player, _) = Create("TonnelHor");

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(12));

            Assert.Contains(events, item => item is PlayerAte ate && ate.EntityId == player.Id);
        }

        /// <summary>Verifies a vertical tunnel does not accept a beetle flying sideways.</summary>
        [Fact]
        public void ABeetleCannotFlySidewaysIntoAVerticalTunnel()
        {
            (SimulationWorld simulation, Entity player, _) = Create("TonnelVert");

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(12));

            Assert.DoesNotContain(events, item => item is PlayerAte ate && ate.EntityId == player.Id);
        }

        /// <summary>
        /// Verifies each flight segment draws the recovered <c>Rand.FRange(0.9, 1.1)</c> speed
        /// jitter: a source pinned low flies at exactly nine tenths of the beetle's speed.
        /// </summary>
        [Fact]
        public void EachFlightSegmentDrawsTheRecoveredJitter()
        {
            (SimulationWorld simulation, _, WorldGrid world) = Create("TonnelHor", BioMassTests.ScriptedRandom.Lowest);
            Entity beetle = world.Entities.Single(entity => entity.SmallBeetleBody is not null);

            _ = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal(
                DifficultySpeeds.Standard.MonsterSpeed * 0.5 * 0.9,
                beetle.Motion!.Speed,
                9);
        }

        /// <summary>
        /// Builds a corridor on row 1 walled above and below, with a beetle spawned at x = 6 and the
        /// player at x = 1, and two tunnels of the given type at x = 3 and x = 4 in its path.
        /// </summary>
        private static (SimulationWorld Simulation, Entity Player, WorldGrid World) Create(string tunnelType, Random? random = null)
        {
            WorldGrid world = new(9, 3);
            Entity player = new(
                EntityId.New(),
                new GridPosition(1, 1),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right),
                healthBody: new HealthBody(100, 100, TimeSpan.FromMilliseconds(650)));
            world.Add(player);
            for (int x = 2; x <= 6; x++)
            {
                world.Add(Wall(x, 0));
                world.Add(Wall(x, 2));
            }

            world.Add(LevelMaterializer.CreateRuntimeObject(EntityId.New(), new GridPosition(3, 1), tunnelType, DifficultySpeeds.Standard));
            world.Add(LevelMaterializer.CreateRuntimeObject(EntityId.New(), new GridPosition(4, 1), tunnelType, DifficultySpeeds.Standard));
            Entity monster = new(
                EntityId.New(),
                new GridPosition(6, 1),
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked,
                greenMonsterBody: new GreenMonsterBody(1.5625, Direction.Up, TimeSpan.FromMilliseconds(250)),
                explosionBody: new ExplosionBody(ExplosionReaction.Destroy, false),
                shakeBody: new ShakeBody(ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3));
            world.Add(monster);
            Entity explosive = new(
                EntityId.New(),
                new GridPosition(6, 1),
                InteractionMask.None,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            world.Add(explosive);
            SimulationWorld simulation = new(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                random ?? new Random(0),
                0.5,
                Undrained.Standard);
            _ = simulation.Detonate(explosive.Id);
            _ = Assert.Single(world.Entities, entity => entity.SmallBeetleBody is not null);
            return (simulation, player, world);
        }

        private static Entity Wall(int x, int y)
        {
            return LevelMaterializer.CreateRuntimeObject(EntityId.New(), new GridPosition(x, y), "CellsMetal01", DifficultySpeeds.Standard);
        }
    }
}
