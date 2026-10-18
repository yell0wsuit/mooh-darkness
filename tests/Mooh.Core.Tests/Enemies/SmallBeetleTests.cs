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
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>
    /// Verifies the recovered small-beetle behavior from <c>GameObjSmBeetle.py</c>: it flies at the
    /// player, is absorbed on contact, is briefly immune to blasts, and removes itself after
    /// <c>CNT_GPrm_SmallTime</c>.
    /// </summary>
    public sealed class SmallBeetleTests
    {
        /// <summary>
        /// Verifies the recovered mask. The beetle registers
        /// <c>ShadowObj | LvlEndLock | CellCanExplode | MonShadow</c>, so it neither locks its
        /// cell nor is eaten out of one, and monster navigation ignores it.
        /// </summary>
        [Fact]
        public void SpawnedBeetleCarriesTheRecoveredMask()
        {
            (WorldGrid world, _, Entity beetle, _, _) = SpawnBeetle(new GridPosition(4, 2));

            Assert.Equal(
                InteractionMask.MonsterTransparent
                    | InteractionMask.OccupancyTransparent
                    | InteractionMask.LevelEndLock,
                beetle.Interactions);
            Assert.Equal(ExplosionReaction.Destroy, beetle.ExplosionBody!.Reaction);
            Assert.Null(beetle.GravityBody);
            Assert.Same(beetle, world.GetEntity(beetle.Id));
        }

        /// <summary>Verifies both monster families spawn a beetle of their own color.</summary>
        /// <param name="blue">Whether the defeated monster is a Blue Monster.</param>
        /// <param name="expected">The color the recovered <c>_OnKillInt</c> spawns.</param>
        [Theory]
        [InlineData(false, SmallBeetleColor.Green)]
        [InlineData(true, SmallBeetleColor.Blue)]
        public void EachMonsterFamilySpawnsItsOwnBeetleColor(bool blue, SmallBeetleColor expected)
        {
            (_, _, Entity beetle, _, IReadOnlyList<SimulationEvent> events) =
                SpawnBeetle(new GridPosition(4, 2), blue);

            Assert.Equal(expected, beetle.SmallBeetleBody!.Color);
            Assert.Equal(expected, Assert.Single(events.OfType<SmallBeetleSpawned>()).Color);
        }

        /// <summary>
        /// Verifies the beetle closes on the player along the dominant axis of the offset, which is
        /// what <c>GameBase.PntDirGet</c> resolves first.
        /// </summary>
        [Fact]
        public void BeetleFliesTowardThePlayerOnTheDominantAxis()
        {
            (_, _, Entity beetle, SimulationWorld simulation, _) = SpawnBeetle(new GridPosition(5, 1));

            _ = simulation.Advance(TimeSpan.FromMilliseconds(10));

            Assert.Equal(SmallBeetleState.Flying, beetle.SmallBeetleBody!.State);
            Assert.Equal(Direction.Left, beetle.Motion!.SegmentDirection);
            Assert.Equal(new GridPosition(4, 1), beetle.Position);
        }

        /// <summary>
        /// Verifies flight ignores every occupant except terrain. Only
        /// <c>CNT_CMaskPartsCollide</c> carriers stop the beetle, so a rock in the way does not.
        /// </summary>
        [Fact]
        public void BeetleFliesOverAnObjectButNotThroughTerrain()
        {
            (WorldGrid world, _, Entity beetle, SimulationWorld simulation, _) =
                SpawnBeetle(new GridPosition(5, 1));
            _ = AddBlocker(world, new GridPosition(4, 1), InteractionMask.BlocksMovement);

            _ = simulation.Advance(TimeSpan.FromMilliseconds(10));

            Assert.Equal(new GridPosition(4, 1), beetle.Position);
        }

        /// <summary>
        /// Verifies a beetle boxed in by terrain waits out the recovered
        /// <c>CNT_OnCellReCheck</c> delay instead of moving.
        /// </summary>
        [Fact]
        public void FullyBlockedBeetleWaitsForTheRecoveredRecheckDelay()
        {
            (WorldGrid world, _, Entity beetle, SimulationWorld simulation, _) =
                SpawnBeetle(new GridPosition(5, 1));
            foreach (Direction direction in new[]
            {
                Direction.Left, Direction.Right, Direction.Up, Direction.Down,
            })
            {
                _ = AddBlocker(world, new GridPosition(5, 1).Offset(direction), InteractionMask.BlocksFlight);
            }

            _ = simulation.Advance(TimeSpan.FromMilliseconds(10));

            Assert.Equal(SmallBeetleState.Waiting, beetle.SmallBeetleBody!.State);
            Assert.Null(beetle.Motion);
            Assert.Equal(
                SmallBeetleBody.RecheckDelay - TimeSpan.FromMilliseconds(10),
                beetle.SmallBeetleBody.RemainingRecheck);
        }

        /// <summary>
        /// Verifies contact absorbs the beetle, awarding <c>CNT_Score_SmBeetle</c> and healing
        /// <c>Compl.HSmall</c> rather than requiring the player to eat it out of a cell.
        /// </summary>
        [Fact]
        public void ReachingThePlayerAbsorbsTheBeetleForScoreAndHealth()
        {
            (_, Entity player, Entity beetle, SimulationWorld simulation, _) =
                SpawnBeetle(new GridPosition(3, 1));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(5));

            PlayerAte absorbed = Assert.Single(events.OfType<PlayerAte>());
            Assert.Equal(beetle.Id, absorbed.EatenEntityId);
            // 10 for the monster kill that spawned it, then CNT_Score_SmBeetle for the absorb.
            Assert.Equal(100, player.PlayerBody!.Progress.Score);
            Assert.Equal(52, player.HealthBody!.Current);
        }

        /// <summary>
        /// Verifies the recovered <c>CNT_GPrm_SmallTime</c> lifetime. A beetle the player can never
        /// reach still expires, so a level whose last lock carrier spawned one can still be won.
        /// </summary>
        [Fact]
        public void UnreachableBeetleExpiresAfterTheRecoveredLifetime()
        {
            (WorldGrid world, _, Entity beetle, SimulationWorld simulation, _) =
                SpawnBeetle(new GridPosition(5, 1));
            foreach (Direction direction in new[]
            {
                Direction.Left, Direction.Right, Direction.Up, Direction.Down,
            })
            {
                _ = AddBlocker(world, new GridPosition(5, 1).Offset(direction), InteractionMask.BlocksFlight);
            }

            IReadOnlyList<SimulationEvent> beforeExpiry = simulation.Advance(
                SmallBeetleBody.Lifetime - TimeSpan.FromMilliseconds(1));

            Assert.Empty(beforeExpiry.OfType<EntityDestroyed>());

            IReadOnlyList<SimulationEvent> atDeadline = simulation.Advance(TimeSpan.FromMilliseconds(1));
            Assert.DoesNotContain(atDeadline, item => item is EntityDestroyed);
            Assert.Same(beetle, world.GetEntity(beetle.Id));
            IReadOnlyList<SimulationEvent> afterExpiry = simulation.Advance(SmallBeetleBody.RecheckDelay);

            Assert.Equal(beetle.Id, Assert.Single(afterExpiry.OfType<EntityDestroyed>()).EntityId);
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(beetle.Id));
            _ = Assert.Single(afterExpiry, item => item is SmallBeetleKilled);
        }

        /// <summary>
        /// Verifies the recovered <c>CNT_ImTime</c> gate on <c>OnExpl</c>. A blast within the
        /// immunity window leaves the beetle alone, which is what stops a monster's own death
        /// explosion from killing the child it just spawned.
        /// </summary>
        [Fact]
        public void BlastWithinTheImmunityWindowDoesNotKillTheBeetle()
        {
            (WorldGrid world, _, Entity beetle, SimulationWorld simulation, _) =
                SpawnBeetle(new GridPosition(5, 1));
            Entity second = AddExplosive(world, new GridPosition(5, 2));

            _ = simulation.Detonate(second.Id);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(250));

            Assert.True(beetle.SmallBeetleBody!.RemainingImmunity > TimeSpan.Zero);
            Assert.Same(beetle, world.GetEntity(beetle.Id));
        }

        /// <summary>Verifies a blast after the immunity window destroys the beetle.</summary>
        [Fact]
        public void BlastAfterTheImmunityWindowKillsTheBeetle()
        {
            (WorldGrid world, _, Entity beetle, SimulationWorld simulation, _) =
                SpawnBeetle(new GridPosition(5, 1));
            foreach (Direction direction in new[]
            {
                Direction.Left, Direction.Right, Direction.Up, Direction.Down,
            })
            {
                _ = AddBlocker(world, new GridPosition(5, 1).Offset(direction), InteractionMask.BlocksFlight);
            }

            _ = simulation.Advance(SmallBeetleBody.ExplosionImmunity);
            Assert.Equal(TimeSpan.Zero, beetle.SmallBeetleBody!.RemainingImmunity);

            _ = simulation.Advance(TimeSpan.FromMilliseconds(1));
            Entity second = AddExplosive(world, new GridPosition(5, 1));
            List<SimulationEvent> events = [.. simulation.Detonate(second.Id)];
            events.AddRange(simulation.Advance(TimeSpan.FromMilliseconds(10)));

            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(beetle.Id));
            _ = Assert.Single(events, item => item is SmallBeetleKilled);
        }

        /// <summary>A lifetime deadline cannot remove a beetle in the middle of a flight segment.</summary>
        [Fact]
        public void LifetimeIsCheckedAtTheNextFlightDecision()
        {
            WorldGrid world = new(5, 5);
            _ = AddPlayer(world, new GridPosition(1, 1));
            Entity beetle = new(EntityId.New(), new GridPosition(3, 2), InteractionMask.LevelEndLock,
                smallBeetleBody: new SmallBeetleBody(SmallBeetleColor.Green, 0.01));
            world.Add(beetle);
            SimulationWorld simulation = new(world, Direction.Down, new DefaultGravityCollisionPolicy(), new Random(1));
            IReadOnlyList<SimulationEvent> beforeArrival = simulation.Advance(TimeSpan.FromSeconds(31));
            Assert.Same(beetle, world.GetEntity(beetle.Id));
            Assert.NotNull(beetle.Motion);
            Assert.DoesNotContain(beforeArrival, item => item is SmallBeetleKilled);
            IReadOnlyList<SimulationEvent> arrival = simulation.Advance(TimeSpan.FromSeconds(100));
            Assert.DoesNotContain(world.Entities, entity => entity.Id == beetle.Id);
            _ = Assert.Single(arrival, item => item is SmallBeetleKilled);
        }

        /// <summary>The original's strict immunity comparison includes the exact1500ms boundary.</summary>
        [Fact]
        public void BlastAtTheExactImmunityDeadlineLeavesTheBeetleAlive()
        {
            (WorldGrid world, _, Entity beetle, SimulationWorld simulation, _) = SpawnBeetle(new GridPosition(5, 1));
            foreach (Direction direction in new[] { Direction.Left, Direction.Right, Direction.Up, Direction.Down })
            {
                _ = AddBlocker(world, beetle.Position.Offset(direction), InteractionMask.BlocksFlight);
            }

            _ = simulation.Advance(SmallBeetleBody.ExplosionImmunity);
            Entity bomb = AddExplosive(world, beetle.Position);
            IReadOnlyList<SimulationEvent> events = simulation.Detonate(bomb.Id);
            Assert.Same(beetle, world.GetEntity(beetle.Id));
            Assert.DoesNotContain(events, item => item is SmallBeetleKilled);
        }

        /// <summary>
        /// Builds a world whose only monster is destroyed immediately, leaving one spawned beetle.
        /// The player sits at the origin so every chase test has a fixed target.
        /// </summary>
        private static Spawn SpawnBeetle(GridPosition monsterPosition, bool blue = false)
        {
            WorldGrid world = new(9, 7);
            Entity player = AddPlayer(world, new GridPosition(1, 1));
            Entity monster = AddMonster(world, monsterPosition, blue);
            // The explosive shares the monster's cell, so the blast centre resolves during
            // Detonate and the fixture hands back a beetle whose timers have not started running.
            Entity explosive = AddExplosive(world, monsterPosition);
            SimulationWorld simulation = new(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                Undrained.Standard);

            IReadOnlyList<SimulationEvent> events = simulation.Detonate(explosive.Id);
            _ = monster;
            SmallBeetleSpawned spawned = events.OfType<SmallBeetleSpawned>().Single();
            return new Spawn(world, player, world.GetEntity(spawned.SpawnedEntityId), simulation, events);
        }

        /// <summary>Holds one prepared beetle-spawn fixture.</summary>
        private sealed record Spawn(
            WorldGrid World,
            Entity Player,
            Entity Beetle,
            SimulationWorld Simulation,
            IReadOnlyList<SimulationEvent> Events);

        private static Entity AddPlayer(WorldGrid world, GridPosition position)
        {
            Entity player = new(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right),
                // Starts below full health so the recovered Compl.HSmall heal is observable
                // rather than being clamped away.
                healthBody: new HealthBody(100, 50, TimeSpan.FromSeconds(1)));
            world.Add(player);
            return player;
        }

        private static Entity AddMonster(WorldGrid world, GridPosition position, bool blue)
        {
            Entity monster = new(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement
                    | InteractionMask.MovementLocked
                    | InteractionMask.LevelEndLock,
                greenMonsterBody: blue
                    ? null
                    : new GreenMonsterBody(1.5625, Direction.Right, TimeSpan.FromMilliseconds(250)),
                blueMonsterBody: blue
                    ? new BlueMonsterBody(1.5625, Direction.Right, TimeSpan.FromMilliseconds(300))
                    : null,
                explosionBody: new ExplosionBody(ExplosionReaction.Destroy, false));
            world.Add(monster);
            return monster;
        }

        private static Entity AddExplosive(WorldGrid world, GridPosition position)
        {
            Entity explosive = new(
                EntityId.New(),
                position,
                InteractionMask.None,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            world.Add(explosive);
            return explosive;
        }

        private static Entity AddBlocker(WorldGrid world, GridPosition position, InteractionMask mask)
        {
            Entity blocker = new(EntityId.New(), position, mask | InteractionMask.MovementLocked);
            world.Add(blocker);
            return blocker;
        }
    }
}
