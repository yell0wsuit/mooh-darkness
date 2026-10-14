using System;

using Mooh.Core.Grid;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the spin follows the native <c>ListRenderSort</c> matrix: positions laid out at
    /// cell steps easing between 80 and 75, moved by the pivot at the original steps, turned by
    /// the angle and moved back at the eased steps. Sprites themselves stay upright.
    /// </summary>
    public sealed class WorldSpinTests
    {
        /// <summary>Verifies no turn leaves every position where it is.</summary>
        [Fact]
        public void NoTurnLeavesPositionsAlone()
        {
            WorldSpin spin = new(new GridPosition(2, 1), 0);

            Assert.Equal((3.25, 4.5), spin.Transform(3.25, 4.5));
        }

        /// <summary>
        /// Verifies a full left quarter turn sends a cell to the right of the pivot below it, the
        /// way <c>WorldRotation.Map</c> does, at the swapped steps.
        /// </summary>
        [Fact]
        public void AFullLeftTurnSendsRightToBelow()
        {
            WorldSpin spin = new(new GridPosition(2, 1), 1);

            (double x, double y) = spin.Transform(3, 1);

            // p = (3 * 75, 1 * 80); minus the pivot at the original steps (160, 75) is (65, 5);
            // turned a quarter it is (-5, 65); plus the pivot at the eased steps (150, 80).
            Assert.Equal(145 / 80.0, x, 9);
            Assert.Equal(145 / 75.0, y, 9);
        }

        /// <summary>Verifies a right turn goes the other way.</summary>
        [Fact]
        public void ARightTurnGoesTheOtherWay()
        {
            WorldSpin spin = new(new GridPosition(2, 1), -1);

            (double x, double y) = spin.Transform(3, 1);

            Assert.Equal(155 / 80.0, x, 9);
            Assert.Equal(15 / 75.0, y, 9);
        }

        /// <summary>Verifies the angle follows the turn's progress and direction.</summary>
        [Fact]
        public void TheAngleFollowsTheProgress()
        {
            Assert.Equal(0.25, WorldSpin.Of(new GridPosition(0, 0), WorldRotationDirection.Left, 0.25).Angle);
            Assert.Equal(-0.5, WorldSpin.Of(new GridPosition(0, 0), WorldRotationDirection.Right, 0.5).Angle);
            Assert.Equal(Math.PI / 4, WorldSpin.Of(new GridPosition(0, 0), WorldRotationDirection.Left, 0.5).Radians, 9);
        }
    }
}
