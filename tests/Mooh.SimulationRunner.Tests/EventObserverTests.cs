using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies runtime events are normalized into stable scenario values.</summary>
    public sealed class EventObserverTests
    {
        /// <summary>Runtime creations use the same stable identifier in events and final rosters.</summary>
        [Fact]
        public void ObservesRuntimeSpawnWithStableAlias()
        {
            EntityId id = new(new Guid("00000000-0000-0000-0000-000000000099"));

            ObservedEvent observed = Assert.Single(EventObserver.Observe([new Landed(id, Direction.Down)], new Dictionary<EntityId, string>()));

            Assert.Equal(new ObservedEvent("Landed", "spawned:00000000-0000-0000-0000-000000000099", direction: Direction.Down), observed);
        }

        /// <summary>Verifies every current event type and secondary entity reference.</summary>
        [Fact]
        public void ObservesEverySimulationEventType()
        {
            EntityId player = new(new Guid("00000000-0000-0000-0000-000000000001"));
            EntityId rock = new(new Guid("00000000-0000-0000-0000-000000000002"));
            EntityId monster = new(new Guid("00000000-0000-0000-0000-000000000003"));
            Dictionary<EntityId, string> aliases = new()
            {
                [player] = "player",
                [rock] = "rock",
                [monster] = "monster",
            };
            SimulationEvent[] events =
            [
                new FallStarted(rock, Direction.Down),
                new SlideStarted(rock, Direction.Left),
                new CellEntered(rock, new GridPosition(1, 2)),
                new Landed(rock, Direction.Down),
                new PlayerMoveStarted(player, Direction.Right),
                new PlayerReversed(player, Direction.Left),
                new PlayerReturned(player, new GridPosition(1, 1)),
                new PlayerBlocked(player, Direction.Up),
                new PlayerStopped(player),
                new PushStarted(rock, Direction.Right),
                new PlayerAte(player, rock),
                new PlayerRotationStarted(player, Direction.Right, Direction.Down),
                new PlayerRotationCompleted(player, Direction.Down),
                new EnemyMoveStarted(
                    monster,
                    new GridPosition(0, 1),
                    new GridPosition(1, 1)),
                new EnemyTurnStarted(
                    monster,
                    Direction.Right,
                    Direction.Down,
                    TimeSpan.FromMilliseconds(250)),
                new EnemyTurnCompleted(monster, Direction.Down),
                new EnemyAttackStarted(
                    monster,
                    new GridPosition(1, 1),
                    TimeSpan.FromMilliseconds(625)),
                new EnemyStrike(monster, new GridPosition(1, 1)),
                new EnemyFrozen(monster),
                new EnemyThawed(monster),
                new EnemyTransformationStarted(
                    monster,
                    true,
                    TimeSpan.FromMilliseconds(375)),
                new EnemyTransformationCompleted(monster, true),
                new SmallBeetleSpawned(
                    monster,
                    EntityId.New(),
                    new GridPosition(2, 2),
                    SmallBeetleColor.Green),
                new DamageApplied(player, monster, DamageKind.Strike, 20, 80),
                new DamageIgnored(
                    player,
                    monster,
                    DamageKind.Strike,
                    DamageResult.IgnoredImmune),
                new PlayerShockStarted(player, TimeSpan.FromMilliseconds(700)),
                new PlayerShockEnded(player),
                new PlayerDied(player),
                new ExplosionStarted(rock, new GridPosition(2, 2)),
                new ExplosionCellReached(rock, new GridPosition(1, 2)),
                new EntityDestroyed(monster, rock),
                new ShakeImpact(rock, 1, 2.5),
            ];

            IReadOnlyList<ObservedEvent> observed = EventObserver.Observe(events, aliases);

            Assert.Equal(32, observed.Count);
            Assert.Equal(new ObservedEvent("ShakeImpact", "rock"), observed[31]);
            Assert.Equal(new ObservedEvent("FallStarted", "rock", direction: Direction.Down), observed[0]);
            Assert.Equal(new ObservedEvent("SlideStarted", "rock", direction: Direction.Left), observed[1]);
            Assert.Equal(
                new ObservedEvent("CellEntered", "rock", position: new GridPosition(1, 2)),
                observed[2]);
            Assert.Equal(new ObservedEvent("PlayerAte", "player", eatenEntity: "rock"), observed[10]);
            Assert.Equal(
                new ObservedEvent(
                    "PlayerRotationStarted",
                    "player",
                    from: Direction.Right,
                    to: Direction.Down),
                observed[11]);
            Assert.Equal(
                new ObservedEvent("PlayerRotationCompleted", "player", direction: Direction.Down),
                observed[12]);
            Assert.Equal(
                new ObservedEvent(
                    "EnemyMoveStarted",
                    "monster",
                    fromPosition: new GridPosition(0, 1),
                    toPosition: new GridPosition(1, 1)),
                observed[13]);
            Assert.Equal(
                new ObservedEvent(
                    "DamageApplied",
                    "player",
                    sourceEntity: "monster",
                    damageKind: DamageKind.Strike,
                    amount: 20,
                    remainingHealth: 80),
                observed[23]);
            Assert.Equal(
                new ObservedEvent(
                    "PlayerShockStarted",
                    "player",
                    durationMilliseconds: 700),
                observed[25]);
            Assert.Equal(
                new ObservedEvent("ExplosionStarted", "rock", position: new GridPosition(2, 2)),
                observed[28]);
            Assert.Equal(
                new ObservedEvent("ExplosionCellReached", "rock", position: new GridPosition(1, 2)),
                observed[29]);
            Assert.Equal(
                new ObservedEvent("EntityDestroyed", "monster", sourceEntity: "rock"),
                observed[30]);
            Assert.Equal(new ObservedEvent("EnemyFrozen", "monster"), observed[18]);
            Assert.Equal(new ObservedEvent("EnemyThawed", "monster"), observed[19]);
            Assert.Equal(
                new ObservedEvent(
                    "EnemyTransformationStarted",
                    "monster",
                    isSmall: true,
                    durationMilliseconds: 375),
                observed[20]);
            Assert.Equal(
                new ObservedEvent("EnemyTransformationCompleted", "monster", isSmall: true),
                observed[21]);
            Assert.Equal(
                new ObservedEvent(
                    "SmallBeetleSpawned",
                    "monster",
                    position: new GridPosition(2, 2)),
                observed[22]);
        }
    }
}
