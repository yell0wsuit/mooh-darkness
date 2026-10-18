using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Player;

using Xunit;

namespace Mooh.Core.Tests.Grid
{
    /// <summary>
    /// Verifies the authoritative half of the recovered <c>GMod.WorldRotStart</c>: the world's
    /// dimensions swap and every entity is remapped in one atomic step. The original reallocates
    /// its cell array, copies each cell across and rewrites every object's cell before returning,
    /// so no gameplay ever observes a half-rotated world.
    /// </summary>
    public sealed class WorldGridRotationTests
    {
        /// <summary>Verifies the recovered <c>ulSizeX</c>/<c>ulSizeY</c> swap.</summary>
        [Fact]
        public void RotatingSwapsTheWorldDimensions()
        {
            WorldGrid world = new(40, 15);

            world.Rotate(WorldRotationDirection.Left);

            Assert.Equal(15, world.Width);
            Assert.Equal(40, world.Height);
        }

        /// <summary>
        /// Verifies every entity moves to its mapped cell, which is the native loop over the object
        /// list rewriting <c>obj[58]</c> and <c>obj[59]</c>.
        /// </summary>
        [Fact]
        public void RotatingRemapsEveryEntity()
        {
            WorldGrid world = new(7, 5);
            Entity corner = NewBlock(new GridPosition(0, 0));
            Entity opposite = NewBlock(new GridPosition(6, 4));
            Entity middle = NewBlock(new GridPosition(2, 1));
            world.Add(corner);
            world.Add(opposite);
            world.Add(middle);

            world.Rotate(WorldRotationDirection.Left);

            Assert.Equal(new GridPosition(4, 0), corner.Position);
            Assert.Equal(new GridPosition(0, 6), opposite.Position);
            Assert.Equal(new GridPosition(3, 2), middle.Position);
        }

        /// <summary>
        /// Verifies a remapped entity is reachable at its new cell and gone from its old one, so
        /// occupancy follows the entity rather than being rebuilt from stale keys.
        /// </summary>
        [Fact]
        public void RotatingMovesOccupancyToTheMappedCell()
        {
            WorldGrid world = new(7, 5);
            Entity block = NewBlock(new GridPosition(2, 1));
            world.Add(block);

            world.Rotate(WorldRotationDirection.Left);

            Assert.Equal(block, Assert.Single(world.GetOccupants(new GridPosition(3, 2))));
            Assert.Empty(world.GetOccupants(new GridPosition(2, 1)));
        }

        /// <summary>
        /// Verifies a shared cell keeps its newest-arrival-first order through a rotation. The
        /// original relinks nothing: it copies each cell's list head across intact, so the LIFO
        /// order that decides interaction precedence survives.
        /// </summary>
        [Fact]
        public void RotatingPreservesCellOccupancyOrder()
        {
            WorldGrid world = new(7, 5);
            Entity first = NewBlock(new GridPosition(2, 1));
            Entity second = NewBlock(new GridPosition(2, 1));
            Entity third = NewBlock(new GridPosition(2, 1));
            world.Add(first);
            world.Add(second);
            world.Add(third);
            List<EntityId> before = [.. world.GetOccupants(new GridPosition(2, 1)).Select(e => e.Id)];

            world.Rotate(WorldRotationDirection.Left);

            List<EntityId> after = [.. world.GetOccupants(new GridPosition(3, 2)).Select(e => e.Id)];
            Assert.Equal(before, after);
            Assert.Equal(third.Id, after[0]);
        }

        /// <summary>
        /// Verifies the player is remapped like any other object. The original keeps the player in
        /// the same world list, so it is rewritten by the same loop.
        /// </summary>
        [Fact]
        public void RotatingRemapsThePlayer()
        {
            WorldGrid world = new(7, 5);
            Entity player = new(
                EntityId.New(),
                new GridPosition(6, 0),
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right));
            world.Add(player);

            world.Rotate(WorldRotationDirection.Right);

            Assert.Equal(new GridPosition(0, 0), player.Position);
            Assert.Equal(player, world.Player);
        }

        /// <summary>
        /// Verifies a second rotation reads the swapped dimensions, so turning one way and back
        /// restores the world exactly. Seven of the sixteen levels that place a rotator place more
        /// than one, and <c>ma_33</c> places eight, so repeated turns are the normal case.
        /// </summary>
        [Fact]
        public void TurningLeftThenRightRestoresTheWorld()
        {
            WorldGrid world = new(7, 5);
            Entity block = NewBlock(new GridPosition(2, 1));
            world.Add(block);

            world.Rotate(WorldRotationDirection.Left);
            world.Rotate(WorldRotationDirection.Right);

            Assert.Equal(7, world.Width);
            Assert.Equal(5, world.Height);
            Assert.Equal(new GridPosition(2, 1), block.Position);
        }

        /// <summary>
        /// Verifies a teleporter's links are remapped with the world, which is the recovered
        /// <c>WorldRotStopCBack</c> pass over every <c>CLinkCoordBase.LinkCoords</c>. Links are
        /// absolute cells, so without this a rotation would silently repoint every teleporter.
        /// </summary>
        [Fact]
        public void RotatingRemapsTeleporterLinks()
        {
            WorldGrid world = new(7, 5);
            TeleporterBody links = new([new GridPosition(6, 4), new GridPosition(2, 1)]);
            world.Add(new Entity(
                EntityId.New(),
                new GridPosition(0, 0),
                InteractionMask.OccupancyTransparent,
                teleporterBody: links));

            world.Rotate(WorldRotationDirection.Left);

            Assert.Equal(
                [new GridPosition(0, 6), new GridPosition(3, 2)],
                links.Links);
        }

        /// <summary>
        /// Verifies the link order survives the remap, because the recovered list comprehension
        /// rebuilds it in place and <c>PlCtrl</c> hands out <c>LinkCoords[0]</c> in turn.
        /// </summary>
        [Fact]
        public void RotatingKeepsTheLinkCycleOrder()
        {
            WorldGrid world = new(7, 5);
            TeleporterBody links = new([
                new GridPosition(1, 0),
                new GridPosition(2, 0),
                new GridPosition(3, 0),
            ]);
            world.Add(new Entity(
                EntityId.New(),
                new GridPosition(0, 0),
                InteractionMask.OccupancyTransparent,
                teleporterBody: links));

            world.Rotate(WorldRotationDirection.Left);

            Assert.Equal(new GridPosition(4, 1), links.TakeNextDestination(new GridPosition(0, 0)));
            Assert.Equal(new GridPosition(4, 2), links.TakeNextDestination(new GridPosition(0, 0)));
            Assert.Equal(new GridPosition(4, 3), links.TakeNextDestination(new GridPosition(0, 0)));
        }

        private static Entity NewBlock(GridPosition position)
        {
            return new Entity(EntityId.New(), position, InteractionMask.BlocksMovement);
        }
    }
}
