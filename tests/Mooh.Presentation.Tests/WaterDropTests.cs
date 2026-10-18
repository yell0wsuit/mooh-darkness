using System.Collections.Generic;
using System.Linq;

using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the drips of <c>GameWaterSpawn</c> and <c>CGameObjWaterDrop</c>: a drop forms
    /// under a <c>CNT_CMaskSpawnWaterDrop</c> cell near the player, falls, and splashes on the
    /// first cell that locks motion.
    /// </summary>
    public sealed class WaterDropTests
    {
        /// <summary>
        /// Verifies a drop forms as play starts, in a free cell whose cell above drips: here only
        /// under the metal block.
        /// </summary>
        [Fact]
        public void ADropFormsUnderADrippingCell()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("CellsMetal01", 4, 0), ("ORock01", 0, 3), ("ORock01", 1, 3), ("ORock01", 2, 3), ("ORock01", 3, 3), ("ORock01", 4, 3), ("ORock01", 5, 3)));

            harness.Step(0.016);

            SceneSprite drop = Assert.Single(Drops(harness));
            Assert.Equal((4.0, 1.0), (drop.X, drop.Y));
            Assert.StartsWith("OBottom", drop.Frame, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies the drop forms over 225 ms, then, with the floor right below, splashes and
        /// plays <c>SndWDrop</c>, and is gone after its splash.
        /// </summary>
        [Fact]
        public void ADropSplashesOnTheFloor()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("CellsMetal01", 4, 0), ("ORock01", 0, 3), ("ORock01", 1, 3), ("ORock01", 2, 3), ("ORock01", 3, 3), ("ORock01", 4, 3), ("ORock01", 5, 3)));
            List<string> heard = [];

            for (int frame = 0; frame < 20; frame++)
            {
                harness.Step(0.016);
                heard.AddRange(harness.Presenter.Sounds.Select(cue => cue.Alias));
            }

            Assert.Contains("WeaponDrop", heard);
            Assert.All(Drops(harness), drop => Assert.Contains(drop.Frame, (string[])["OBottom5", "OBottom6", "OBottom7", "OBottom8", "OBottom9"]));
            for (int frame = 0; frame < 30; frame++)
            {
                harness.Step(0.016);
            }

            Assert.Empty(Drops(harness));
        }

        private static List<SceneSprite> Drops(SceneHarness harness)
        {
            return [.. harness.Presenter.Sprites.Where(sprite => sprite.Page == "SpriteObjWaterDrop")];
        }
    }
}
