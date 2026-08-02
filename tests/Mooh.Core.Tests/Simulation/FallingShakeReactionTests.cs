using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Shakes;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>Verifies the recovered per-type reactions to an ordinary or hard landing shake.</summary>
    public sealed class FallingShakeReactionTests
    {
        /// <summary>Verifies a shaken explosive detonates its own blast instead of being ignored.</summary>
        [Fact]
        public void OrdinaryShakeDetonatesExplosiveOccupant()
        {
            WorldGrid world = new(3, 5);
            Entity bottle = NewShakeable(
                new GridPosition(1, 3),
                new ShakeBody(ShakeReaction.Detonate3x3, ShakeReaction.Detonate3x3),
                new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            world.Add(bottle);
            world.Add(NewFalling(new GridPosition(1, 1), GravityImpactStrength.Ordinary));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Contains(events, item => item == new ExplosionStarted(bottle.Id, new GridPosition(1, 3)));
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(bottle.Id));
        }

        /// <summary>Verifies a shaken bubble is destroyed without producing a blast.</summary>
        [Fact]
        public void OrdinaryShakeDestroysBubbleWithoutBlast()
        {
            WorldGrid world = new(3, 5);
            Entity bubble = NewShakeable(
                new GridPosition(1, 3),
                new ShakeBody(ShakeReaction.Destroy, ShakeReaction.Destroy));
            world.Add(bubble);
            world.Add(NewFalling(new GridPosition(1, 1), GravityImpactStrength.Ordinary));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Contains(events, item => item is EntityDestroyed destroyed
                && destroyed.EntityId == bubble.Id);
            Assert.DoesNotContain(events, item => item is ExplosionStarted);
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(bubble.Id));
        }

        /// <summary>
        /// Verifies treasure and food survive an ordinary landing but not a hard one, because the
        /// recovered types define only <c>OnShakeHard</c>.
        /// </summary>
        /// <param name="hard">Whether the falling object requests the hard shake.</param>
        /// <param name="expectDestroyed">Whether the occupant should be destroyed.</param>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void OnlyHardShakeDestroysTreasure(bool hard, bool expectDestroyed)
        {
            WorldGrid world = new(3, 5);
            Entity coin = NewShakeable(
                new GridPosition(1, 3),
                new ShakeBody(ShakeReaction.None, ShakeReaction.Destroy));
            world.Add(coin);
            world.Add(NewFalling(
                new GridPosition(1, 1),
                hard ? GravityImpactStrength.Hard : GravityImpactStrength.Ordinary));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(
                expectDestroyed,
                events.OfType<EntityDestroyed>().Any(item => item.EntityId == coin.Id));
            Assert.Equal(expectDestroyed, !world.Entities.Any(entity => entity.Id == coin.Id));
        }

        /// <summary>Verifies an occupant with no recovered handler survives either landing.</summary>
        /// <param name="hard">Whether the falling object requests the hard shake.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ShakeLeavesUnreactingOccupantInPlace(bool hard)
        {
            WorldGrid world = new(3, 5);
            Entity rock = NewShakeable(new GridPosition(1, 3), shakeBody: null);
            world.Add(rock);
            world.Add(NewFalling(
                new GridPosition(1, 1),
                hard ? GravityImpactStrength.Hard : GravityImpactStrength.Ordinary));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.DoesNotContain(events, item => item is EntityDestroyed or ExplosionStarted);
            Assert.Equal(new GridPosition(1, 3), rock.Position);
        }

        /// <summary>Verifies a deferred recovered handler takes no action rather than guessing one.</summary>
        [Fact]
        public void DeferredShakeReactionTakesNoAction()
        {
            WorldGrid world = new(3, 5);
            Entity barrel = NewShakeable(
                new GridPosition(1, 3),
                new ShakeBody(ShakeReaction.Deferred, ShakeReaction.Deferred));
            world.Add(barrel);
            world.Add(NewFalling(new GridPosition(1, 1), GravityImpactStrength.Ordinary));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.DoesNotContain(events, item => item is EntityDestroyed or ExplosionStarted);
            Assert.Equal(new GridPosition(1, 3), barrel.Position);
        }

        private static Entity NewShakeable(
            GridPosition position,
            ShakeBody? shakeBody,
            ExplosionBody? explosionBody = null)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked,
                explosionBody: explosionBody,
                shakeBody: shakeBody);
        }

        private static Entity NewFalling(GridPosition position, GravityImpactStrength strength)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.None,
                new GravityBody(2, canSlide: false, strength));
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
