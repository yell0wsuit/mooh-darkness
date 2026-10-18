using System;

using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies continuous positions survive the original's stopped mid-cell turn.</summary>
    public sealed class DrawnPositionTests
    {
        /// <summary>Verifies a reversal freezes the physical offset instead of snapping to the committed cell.</summary>
        [Theory]
        [InlineData(Direction.Left)]
        [InlineData(Direction.Up)]
        [InlineData(Direction.Right)]
        [InlineData(Direction.Down)]
        public void AReversalKeepsTheDrawnPosition(Direction direction)
        {
            WorldGrid world = new(10, 10);
            Entity player = new(EntityId.New(), new GridPosition(5, 5), InteractionMask.None,
                playerBody: new PlayerBody(2, 1.5, direction));
            world.Add(player);
            SimulationWorld simulation = new(world, Direction.Down, new DefaultGravityCollisionPolicy());
            simulation.SetPlayerInput(Held(direction));
            _ = simulation.Advance(TimeSpan.FromMilliseconds(125));
            (double X, double Y) before = DrawnPosition.Of(player);

            simulation.SetPlayerInput(Held(direction.Opposite()));
            _ = simulation.Advance(TimeSpan.FromMilliseconds(50));

            Assert.NotNull(player.PlayerBody!.Rotation);
            Assert.Equal(before, DrawnPosition.Of(player));
            simulation.SetPlayerInput(Held(direction));
            _ = simulation.Advance(TimeSpan.FromMilliseconds(100));
            Assert.Equal(before, DrawnPosition.Of(player));
        }

        private static PlayerInputState Held(Direction direction)
        {
            return new PlayerInputState(direction == Direction.Left, direction == Direction.Up,
                direction == Direction.Right, direction == Direction.Down);
        }
    }
}
