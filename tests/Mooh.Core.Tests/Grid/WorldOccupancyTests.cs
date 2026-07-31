using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

using Xunit;

namespace Mooh.Core.Tests.Grid
{
    /// <summary>
    /// Verifies authoritative entity registration and ordered cell occupancy.
    /// </summary>
    public sealed class WorldOccupancyTests
    {
        /// <summary>Verifies that adding an entity registers it in its initial cell.</summary>
        [Fact]
        public void AddRegistersEntityInItsCell()
        {
            WorldGrid world = new(10, 10);
            Entity entity = NewEntity(new GridPosition(2, 3), InteractionMask.BlocksMovement);

            world.Add(entity);

            Assert.Same(entity, world.GetEntity(entity.Id));
            Assert.Equal([entity], world.GetOccupants(entity.Position));
        }

        /// <summary>Verifies that a cell preserves multiple occupants in insertion order.</summary>
        [Fact]
        public void CellSupportsMultipleEntitiesInInsertionOrder()
        {
            WorldGrid world = new(10, 10);
            GridPosition position = new(2, 3);
            Entity first = NewEntity(position, InteractionMask.None);
            Entity second = NewEntity(position, InteractionMask.PlayerEdible);

            world.Add(first);
            world.Add(second);

            Assert.Equal([first, second], world.GetOccupants(position));
        }

        /// <summary>Verifies that duplicate identities are rejected without changing occupancy.</summary>
        [Fact]
        public void AddRejectsDuplicateIdentityWithoutChangingOccupancy()
        {
            WorldGrid world = new(10, 10);
            EntityId id = EntityId.New();
            Entity first = new(id, new GridPosition(2, 3), InteractionMask.None);
            Entity duplicate = new(id, new GridPosition(5, 5), InteractionMask.None);
            world.Add(first);

            _ = Assert.Throws<InvalidOperationException>(() => world.Add(duplicate));
            Assert.Equal([first], world.GetOccupants(first.Position));
            Assert.Empty(world.GetOccupants(duplicate.Position));
        }

        /// <summary>Verifies that entities cannot be registered outside the world.</summary>
        [Fact]
        public void AddRejectsPositionOutsideWorld()
        {
            WorldGrid world = new(10, 10);
            Entity entity = NewEntity(new GridPosition(10, 0), InteractionMask.None);

            _ = Assert.Throws<ArgumentOutOfRangeException>(() => world.Add(entity));
            Assert.Empty(world.Entities);
        }

        private static Entity NewEntity(GridPosition position, InteractionMask mask)
        {
            return new Entity(EntityId.New(), position, mask);
        }
    }
}
