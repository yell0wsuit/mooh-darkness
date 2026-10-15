using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

using Xunit;

namespace Mooh.Core.Tests.Grid
{
    /// <summary>
    /// Verifies stable world-level entity registration order.
    /// </summary>
    public sealed class WorldEntityOrderTests
    {
        /// <summary>Verifies that enumeration follows registration rather than cell order.</summary>
        [Fact]
        public void EntitiesFollowRegistrationOrder()
        {
            WorldGrid world = new(5, 5);
            Entity first = NewEntity(4, 4);
            Entity second = NewEntity(0, 0);
            Entity third = NewEntity(2, 3);
            world.Add(first);
            world.Add(second);
            world.Add(third);

            Assert.Equal([first, second, third], world.Entities);
        }

        /// <summary>Verifies that entity enumeration cannot mutate world registration.</summary>
        [Fact]
        public void EntitiesReturnsReadOnlySnapshot()
        {
            WorldGrid world = new(5, 5);
            Entity registered = NewEntity(1, 1);
            world.Add(registered);
            IReadOnlyList<Entity> snapshot = world.Entities;

            ICollection<Entity> collection = Assert.IsType<ICollection<Entity>>(snapshot, exactMatch: false);
            _ = Assert.Throws<NotSupportedException>(() => collection.Add(NewEntity(2, 2)));

            Assert.Equal([registered], world.Entities);
        }

        private static Entity NewEntity(int x, int y)
        {
            return new Entity(EntityId.New(), new GridPosition(x, y), InteractionMask.None);
        }
    }
}
