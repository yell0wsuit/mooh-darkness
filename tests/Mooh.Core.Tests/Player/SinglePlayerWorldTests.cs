using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies that a world contains at most one player entity.
    /// </summary>
    public sealed class SinglePlayerWorldTests
    {
        /// <summary>Verifies that the registered player can be retrieved.</summary>
        [Fact]
        public void PlayerReturnsRegisteredPlayer()
        {
            WorldGrid world = new(5, 5);
            Entity player = NewPlayer(1, 1);

            world.Add(player);

            Assert.Same(player, world.Player);
        }

        /// <summary>Verifies that registering a second player fails without changing the world.</summary>
        [Fact]
        public void AddRejectsSecondPlayerWithoutRegisteringIt()
        {
            WorldGrid world = new(5, 5);
            Entity first = NewPlayer(1, 1);
            Entity second = NewPlayer(2, 2);
            world.Add(first);

            _ = Assert.Throws<InvalidOperationException>(() => world.Add(second));

            Assert.Same(first, world.Player);
            Assert.Equal([first], world.Entities);
            Assert.Empty(world.GetOccupants(second.Position));
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(second.Id));
        }

        /// <summary>Verifies that worlds without players may contain multiple ordinary entities.</summary>
        [Fact]
        public void WorldWithoutPlayerAcceptsMultipleEntities()
        {
            WorldGrid world = new(5, 5);
            Entity first = NewEntity(1, 1);
            Entity second = NewEntity(2, 2);

            world.Add(first);
            world.Add(second);

            Assert.Null(world.Player);
            Assert.Equal([first, second], world.Entities);
        }

        private static Entity NewPlayer(int x, int y)
        {
            return new Entity(
                EntityId.New(),
                new GridPosition(x, y),
                InteractionMask.None,
                playerBody: new PlayerBody(2, 1.5, Direction.Down));
        }

        private static Entity NewEntity(int x, int y)
        {
            return new Entity(EntityId.New(), new GridPosition(x, y), InteractionMask.None);
        }
    }
}
