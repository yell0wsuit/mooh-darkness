using System;

using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the camera keeps the player at the original's <c>CNT_WrdBase</c>, easing toward it
    /// with <c>CNT_ScrPlFitK</c>.
    /// </summary>
    public sealed class SceneCameraTests
    {
        /// <summary>Verifies a snapped camera draws the target at the screen center.</summary>
        [Fact]
        public void ASnappedCameraCentresTheTarget()
        {
            SceneCamera camera = new();

            camera.Snap(3, 2);

            Assert.Equal((400.0, 300.0), camera.ToScreen(3, 2));
            Assert.Equal((480.0, 375.0), camera.ToScreen(4, 3));
        }

        /// <summary>
        /// Verifies a following camera closes 1 - e^(-1) of the distance in 250 ms, the time constant
        /// of <c>CNT_ScrPlFitK = -0.004</c> per millisecond.
        /// </summary>
        [Fact]
        public void AFollowingCameraEasesTowardTheTarget()
        {
            SceneCamera camera = new();
            camera.Snap(0, 0);

            camera.Follow(1, 0, 0.25);

            (double x, _) = camera.ToScreen(1, 0);
            Assert.Equal(400 + (80 * Math.Exp(-1)), x, 6);
        }
    }
}
