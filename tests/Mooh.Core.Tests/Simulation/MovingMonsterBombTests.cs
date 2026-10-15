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
    /// <summary>Checks bomb contact and blasts against the original's moving monster occupants.</summary>
    public sealed class MovingMonsterBombTests
    {
        /// <summary>Verifies downward travel does not grant immunity to a grenade landing above the monster.</summary>
        [Theory]
        [InlineData("MGrB")]
        [InlineData("MBlueB")]
        public void AFallingGrenadeKillsADownwardMovingMonster(string type)
        {
            WorldGrid world = new(7, 16);
            Entity monster = LevelMaterializer.CreateRuntimeObject(EntityId.New(), new GridPosition(3, 2), type, DifficultySpeeds.Standard);
            Entity bomb = LevelMaterializer.CreateRuntimeObject(EntityId.New(), new GridPosition(3, 0), "WBomb", DifficultySpeeds.Standard);
            world.Add(monster);
            world.Add(bomb);
            for (int y = 0; y < world.Height; y++)
            {
                world.Add(new Entity(EntityId.New(), new GridPosition(2, y), InteractionMask.BlocksMovement));
                world.Add(new Entity(EntityId.New(), new GridPosition(4, y), InteractionMask.BlocksMovement));
            }

            SimulationWorld simulation = new(world, Direction.Down, new DefaultGravityCollisionPolicy(), new Random(1), 0.5, DifficultySpeeds.Standard);
            List<SimulationEvent> events = [.. simulation.Advance(TimeSpan.FromMilliseconds(2700))];
            Assert.DoesNotContain(events, item => item is ExplosionStarted started && started.EntityId == bomb.Id);
            for (int frame = 0; frame < 200 && world.Entities.Contains(monster); frame++)
            {
                events.AddRange(simulation.Advance(TimeSpan.FromMilliseconds(50)));
            }

            Assert.Contains(events, item => item is Landed landed && landed.EntityId == bomb.Id);
            Assert.Contains(events, item => item is EntityDestroyed destroyed && destroyed.EntityId == monster.Id);
        }

        /// <summary>Verifies ice stopping the support monster resolves a pending landing in that step.</summary>
        [Fact]
        public void FreezingTheSupportCompletesAPendingLanding()
        {
            WorldGrid world = new(7, 12);
            Entity player = new(EntityId.New(), new GridPosition(0, 0), InteractionMask.None,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right),
                healthBody: new HealthBody(100, 100, TimeSpan.FromMilliseconds(650)));
            Entity monster = LevelMaterializer.CreateRuntimeObject(EntityId.New(), new GridPosition(3, 2), "MGrB", DifficultySpeeds.Standard);
            Entity bomb = new(EntityId.New(), new GridPosition(3, 0), InteractionMask.BlocksMovement,
                gravityBody: new GravityBody(3, canSlide: false),
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, detonatesOnLanding: true));
            world.Add(player);
            world.Add(monster);
            world.Add(bomb);
            world.Add(LevelMaterializer.CreateRuntimeObject(EntityId.New(), new GridPosition(1, 0), "BIce", DifficultySpeeds.Standard));
            world.Add(new Entity(EntityId.New(), new GridPosition(1, 1), InteractionMask.BlocksMovement));
            SimulationWorld simulation = new(world, Direction.Down, new DefaultGravityCollisionPolicy());
            for (int frame = 0; frame < 300 && !WaitingOnMovingSupport(); frame++)
            {
                _ = simulation.Advance(TimeSpan.FromMilliseconds(10));
            }

            Assert.True(WaitingOnMovingSupport());
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Contains(events, item => item is EnemyFrozen frozen && frozen.EntityId == monster.Id);
            Assert.Contains(events, item => item is Landed landed && landed.EntityId == bomb.Id);
            Assert.Contains(events, item => item is ExplosionStarted started && started.EntityId == bomb.Id);

            bool WaitingOnMovingSupport()
            {
                return bomb.Motion is null && monster.Motion?.Progress is >= 0.5
                    && bomb.Position.Offset(Direction.Down) == monster.Position;
            }
        }

        /// <summary>Verifies a blast kills a moving monster when it reaches the monster's committed cell.</summary>
        [Theory]
        [InlineData("MGrB")]
        [InlineData("MBlueB")]
        public void ABlastKillsADownwardMovingMonster(string type)
        {
            WorldGrid world = new(7, 7);
            Entity monster = LevelMaterializer.CreateRuntimeObject(EntityId.New(), new GridPosition(3, 2), type, DifficultySpeeds.Standard);
            world.Add(monster);
            world.Add(new Entity(EntityId.New(), new GridPosition(4, 2), InteractionMask.BlocksMovement));
            SimulationWorld simulation = new(world, Direction.Down, new DefaultGravityCollisionPolicy());
            _ = simulation.Advance(TimeSpan.FromMilliseconds(10));
            Assert.Equal(Direction.Down, monster.Motion!.TravelDirection);
            Assert.True(monster.Motion.Progress < 0.5);
            Entity bomb = new(EntityId.New(), monster.Position.Offset(Direction.Left), InteractionMask.None,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            world.Add(bomb);

            _ = simulation.Detonate(bomb.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(201));

            Assert.Contains(events, item => item is EntityDestroyed destroyed && destroyed.EntityId == monster.Id);
        }
    }
}
