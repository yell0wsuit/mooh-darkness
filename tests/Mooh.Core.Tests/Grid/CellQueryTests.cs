using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;

using Xunit;

namespace Mooh.Core.Tests.Grid
{
    /// <summary>Checks non-mutating cell queries without creating occupancy snapshots.</summary>
    public sealed class CellQueryTests
    {
        /// <summary>Boolean queries retain masks and motion rules without per-call allocations.</summary>
        [Fact]
        public void BooleanQueriesDoNotAllocateAfterWarmup()
        {
            WorldGrid world = new(4, 4);
            GridPosition cell = new(1, 1);
            Entity solid = new(EntityId.New(), cell, InteractionMask.None);
            Entity shadow = new(EntityId.New(), cell,
                InteractionMask.OccupancyTransparent | InteractionMask.MonsterTransparent);
            Entity monster = new(EntityId.New(), new GridPosition(0, 0), InteractionMask.None);
            world.Add(solid);
            world.Add(shadow);
            KinematicsSystem.Start(shadow, MotionCause.Falling, Direction.Left, 1);

            for (int i = 0; i < 100; i++)
            {
                _ = Query(world, cell, monster);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            int result = 0;
            for (int i = 0; i < 1000; i++)
            {
                result += Query(world, cell, monster);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(4000, result);
            Assert.Equal(0, allocated);
            Assert.True(world.Remove(solid.Id));
            Assert.False(CellQuery.IsOccupied(world, cell));
            Assert.True(CellQuery.CanMonsterEnter(world, monster, cell));
            Assert.False(CellQuery.HoldsBlastSurvivor(world, cell));
            Assert.False(CellQuery.HasCounterMotion(world, new GridPosition(-1, 0), Direction.Right));
        }

        private static int Query(WorldGrid world, GridPosition cell, Entity monster)
        {
            return (CellQuery.IsOccupied(world, cell) ? 1 : 0)
                + (!CellQuery.CanMonsterEnter(world, monster, cell) ? 1 : 0)
                + (CellQuery.HoldsBlastSurvivor(world, cell) ? 1 : 0)
                + (CellQuery.HasCounterMotion(world, cell, Direction.Right) ? 1 : 0);
        }
    }
}
