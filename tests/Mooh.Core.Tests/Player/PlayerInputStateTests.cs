using System;

using Mooh.Core.Grid;
using Mooh.Core.Player;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies held player input queries and direction priority.
    /// </summary>
    public sealed class PlayerInputStateTests
    {
        /// <summary>Verifies that held state can be queried by cardinal direction.</summary>
        /// <param name="direction">The direction to inspect.</param>
        /// <param name="expected">Whether that direction is held.</param>
        [Theory]
        [InlineData(Direction.Left, true)]
        [InlineData(Direction.Up, false)]
        [InlineData(Direction.Right, true)]
        [InlineData(Direction.Down, false)]
        public void IsHeldReturnsDirectionState(Direction direction, bool expected)
        {
            PlayerInputState input = new(Left: true, Up: false, Right: true, Down: false);

            Assert.Equal(expected, input.IsHeld(direction));
        }

        /// <summary>Verifies the fixed priority of simultaneously held directions.</summary>
        /// <param name="left">Whether left is held.</param>
        /// <param name="up">Whether up is held.</param>
        /// <param name="right">Whether right is held.</param>
        /// <param name="down">Whether down is held.</param>
        /// <param name="expected">The expected first direction.</param>
        [Theory]
        [InlineData(true, true, true, true, Direction.Left)]
        [InlineData(false, true, true, true, Direction.Up)]
        [InlineData(false, false, true, true, Direction.Right)]
        [InlineData(false, false, false, true, Direction.Down)]
        [InlineData(false, false, false, false, null)]
        public void FirstHeldDirectionUsesFixedPriority(
            bool left,
            bool up,
            bool right,
            bool down,
            Direction? expected)
        {
            PlayerInputState input = new(left, up, right, down);

            Assert.Equal(expected, input.FirstHeldDirection());
        }

        /// <summary>Verifies that querying a non-cardinal enum value is rejected.</summary>
        [Fact]
        public void IsHeldRejectsInvalidDirection()
        {
            PlayerInputState input = default;

            _ = Assert.Throws<ArgumentOutOfRangeException>(() => input.IsHeld((Direction)int.MaxValue));
        }
    }
}
