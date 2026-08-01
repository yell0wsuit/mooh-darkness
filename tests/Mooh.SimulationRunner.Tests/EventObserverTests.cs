using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies runtime events are normalized into stable scenario values.</summary>
    public sealed class EventObserverTests
    {
        /// <summary>Verifies every current event type and secondary entity reference.</summary>
        [Fact]
        public void ObservesEverySimulationEventType()
        {
            EntityId player = new(new Guid("00000000-0000-0000-0000-000000000001"));
            EntityId rock = new(new Guid("00000000-0000-0000-0000-000000000002"));
            Dictionary<EntityId, string> aliases = new()
            {
                [player] = "player",
                [rock] = "rock",
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
            ];

            IReadOnlyList<ObservedEvent> observed = EventObserver.Observe(events, aliases);

            Assert.Equal(13, observed.Count);
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
        }
    }
}
