using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>Verifies recovered delayed 3x3 explosion propagation.</summary>
    public sealed class ExplosionPropagationTests
    {
        private static readonly GridPosition[] NeighborOrder =
        [
            new(1, 2),
            new(3, 2),
            new(1, 1),
            new(2, 1),
            new(3, 1),
            new(1, 3),
            new(2, 3),
            new(3, 3),
        ];

        /// <summary>Verifies the center is immediate and neighbors resolve after 200 ms in source order.</summary>
        [Fact]
        public void CenterIsImmediateAndNeighborsAreDelayedInRecoveredOrder()
        {
            WorldGrid world = new(5, 5);
            Entity source = Add(world, new GridPosition(2, 2), ExplosionReaction.Detonate3x3);
            Entity center = Add(world, new GridPosition(2, 2), ExplosionReaction.Destroy);
            Entity[] neighbors = [.. NeighborOrder.Select(position => Add(world, position, ExplosionReaction.Destroy))];
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> immediate = simulation.Detonate(source.Id);

            Assert.Equal(
                [
                    new ExplosionStarted(source.Id, new GridPosition(2, 2)),
                    new EntityDestroyed(source.Id, source.Id),
                    new ExplosionCellReached(source.Id, new GridPosition(2, 2)),
                    new EntityDestroyed(center.Id, source.Id),
                ],
                immediate);
            AssertMissing(world, source.Id, center.Id);
            Assert.All(neighbors, entity => Assert.Same(entity, world.GetEntity(entity.Id)));

            Assert.Empty(simulation.Advance(TimeSpan.FromMilliseconds(199)));

            IReadOnlyList<SimulationEvent> delayed = simulation.Advance(TimeSpan.FromMilliseconds(1));

            List<SimulationEvent> expected = [];
            for (int index = 0; index < NeighborOrder.Length; index++)
            {
                expected.Add(new ExplosionCellReached(source.Id, NeighborOrder[index]));
                expected.Add(new EntityDestroyed(neighbors[index].Id, source.Id));
            }

            Assert.Equal(expected, delayed);
            AssertMissing(world, [.. neighbors.Select(entity => entity.Id)]);
        }

        /// <summary>Verifies a chain source schedules its own neighbors from the chain time.</summary>
        [Fact]
        public void ExplodableSourceChainsAnotherThreeByThreeExplosion()
        {
            WorldGrid world = new(5, 5);
            Entity source = Add(world, new GridPosition(1, 2), ExplosionReaction.Detonate3x3);
            Entity chain = Add(world, new GridPosition(2, 2), ExplosionReaction.Detonate3x3);
            Entity distant = Add(world, new GridPosition(3, 2), ExplosionReaction.Destroy);
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Detonate(source.Id);
            IReadOnlyList<SimulationEvent> firstWave = simulation.Advance(TimeSpan.FromMilliseconds(200));

            AssertMissing(world, chain.Id);
            Assert.Same(distant, world.GetEntity(distant.Id));
            Assert.Contains(new ExplosionStarted(chain.Id, chain.Position), firstWave);

            Assert.Empty(simulation.Advance(TimeSpan.FromMilliseconds(199)));
            IReadOnlyList<SimulationEvent> secondWave = simulation.Advance(TimeSpan.FromMilliseconds(1));

            AssertMissing(world, distant.Id);
            Assert.Contains(new EntityDestroyed(distant.Id, chain.Id), secondWave);
        }

        /// <summary>Verifies nested same-cell chains cannot redetonate a stale snapshot entry.</summary>
        [Fact]
        public void ColocatedChainSourcesEachDetonateOnce()
        {
            WorldGrid world = new(5, 5);
            GridPosition center = new(2, 2);
            Entity source = Add(world, center, ExplosionReaction.Detonate3x3);
            Entity firstChain = Add(world, center, ExplosionReaction.Detonate3x3);
            Entity secondChain = Add(world, center, ExplosionReaction.Detonate3x3);
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Detonate(source.Id);

            Assert.Equal(1, events.Count(value => value == new ExplosionStarted(source.Id, center)));
            Assert.Equal(1, events.Count(value => value == new ExplosionStarted(firstChain.Id, center)));
            Assert.Equal(1, events.Count(value => value == new ExplosionStarted(secondChain.Id, center)));
        }

        /// <summary>Verifies explosion damage comes from the active recovered difficulty.</summary>
        [Fact]
        public void DifficultyZeroExplosionDamagesPlayerByTen()
        {
            WorldGrid world = new(5, 5);
            Entity source = Add(world, new GridPosition(2, 2), ExplosionReaction.Detonate3x3);
            Entity player = new(
                EntityId.New(),
                new GridPosition(1, 2),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2, 1.5, Direction.Right),
                healthBody: new HealthBody(100, 100, TimeSpan.FromSeconds(1)));
            world.Add(player);
            SimulationWorld simulation = CreateSimulation(world, DifficultySpeeds.DifficultyZero);

            _ = simulation.Detonate(source.Id);
            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(200));

            Assert.Equal(90, player.HealthBody!.Current);
            Assert.Contains(new DamageApplied(player.Id, source.Id, DamageKind.Explosion, 10, 90), events);
        }

        /// <summary>Verifies reached cells block newly unsupported falling bodies for 500 ms.</summary>
        [Fact]
        public void ReachedCellBlocksFallingForFiveHundredMilliseconds()
        {
            WorldGrid world = new(5, 5);
            Entity source = Add(world, new GridPosition(2, 2), ExplosionReaction.Detonate3x3);
            Entity support = Add(world, new GridPosition(1, 1), ExplosionReaction.Destroy);
            support.Interactions = InteractionMask.BlocksMovement;
            Entity falling = new(
                EntityId.New(),
                new GridPosition(1, 0),
                InteractionMask.None,
                new GravityBody(1, canSlide: false));
            world.Add(falling);
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Detonate(source.Id);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(200));

            AssertMissing(world, support.Id);
            Assert.Null(falling.Motion);
            Assert.Empty(simulation.Advance(TimeSpan.FromMilliseconds(499)));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1));

            Assert.Equal([new FallStarted(falling.Id, Direction.Down)], events);
        }

        private static SimulationWorld CreateSimulation(
            WorldGrid world,
            DifficultySpeeds? difficulty = null)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                difficulty ?? DifficultySpeeds.Standard);
        }

        private static Entity Add(
            WorldGrid world,
            GridPosition position,
            ExplosionReaction reaction)
        {
            Entity entity = new(
                EntityId.New(),
                position,
                InteractionMask.None,
                explosionBody: new ExplosionBody(reaction, detonatesOnLanding: false));
            world.Add(entity);
            return entity;
        }

        private static void AssertMissing(WorldGrid world, params EntityId[] ids)
        {
            Assert.All(ids, id => Assert.Throws<KeyNotFoundException>(() => world.GetEntity(id)));
        }
    }
}
