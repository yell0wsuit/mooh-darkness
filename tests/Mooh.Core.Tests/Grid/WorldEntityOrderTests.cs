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

        /// <summary>Repeated reads share a snapshot until registration changes.</summary>
        [Fact]
        public void EntitiesReusesSnapshotUntilMembershipChanges()
        {
            WorldGrid world = new(5, 5);
            Entity first = NewEntity(1, 1);
            Entity second = NewEntity(2, 2);
            world.Add(first);
            IReadOnlyList<Entity> initial = world.Entities;
            Assert.Same(initial, world.Entities);

            world.Add(second);
            IReadOnlyList<Entity> added = world.Entities;
            Assert.NotSame(initial, added);
            Assert.Equal([first], initial);
            Assert.Equal([first, second], added);
            Assert.Same(added, world.Entities);

            Assert.True(world.Remove(first.Id));
            Assert.Equal([second], world.Entities);
            Assert.Equal([first, second], added);
            Assert.Same(world.Entities, world.Entities);
            Assert.False(world.Remove(first.Id));
            Assert.Same(world.Entities, world.Entities);
        }

        /// <summary>A cached snapshot cannot have its entries replaced by a caller.</summary>
        [Fact]
        public void EntitiesSnapshotRejectsEntryReplacement()
        {
            WorldGrid world = new(5, 5);
            Entity first = NewEntity(1, 1);
            world.Add(first);
            IList<Entity> snapshot = Assert.IsAssignableFrom<IList<Entity>>(world.Entities);

            _ = Assert.Throws<NotSupportedException>(() => snapshot[0] = NewEntity(2, 2));
            Assert.Same(first, world.Entities[0]);
        }

        /// <summary>Removal during enumeration keeps the original registration order.</summary>
        [Fact]
        public void SnapshotSupportsRemovingEveryEntityDuringEnumeration()
        {
            WorldGrid world = new(5, 5);
            world.Add(NewEntity(1, 1));
            world.Add(NewEntity(2, 2));
            IReadOnlyList<Entity> snapshot = world.Entities;
            foreach (Entity entity in snapshot)
            {
                Assert.True(world.Remove(entity.Id));
            }

            Assert.Equal(2, snapshot.Count);
            Assert.Empty(world.Entities);
        }

        private static Entity NewEntity(int x, int y)
        {
            return new Entity(EntityId.New(), new GridPosition(x, y), InteractionMask.None);
        }
    }
}
