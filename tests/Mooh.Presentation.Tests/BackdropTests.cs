using System;
using System.Linq;

using Mooh.Presentation.Animation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies the recovered <c>CBackLayer</c> backdrop.</summary>
    public sealed class BackdropTests
    {
        /// <summary>Verifies <c>LvlBackType</c>: the style most of the level's objects vote for wins.</summary>
        [Fact]
        public void TheStyleMostObjectsVoteForWins()
        {
            BackdropStyle style = Backdrop.StyleOf(["JCellsEat01", "JCellsSolid01", "CellsEat01", "OBubble"], SceneHarness.Library);

            Assert.Equal(BackdropStyle.Tropical, style);
        }

        /// <summary>Verifies a level nobody votes on gets the normal backdrop.</summary>
        [Fact]
        public void ALevelWithoutVotesGetsTheNormalBackdrop()
        {
            Assert.Equal(BackdropStyle.Normal, Backdrop.StyleOf(["CellsMetal01"], SceneHarness.Library));
        }

        /// <summary>
        /// Verifies the layers' parallax: the image moves at 0.5 or less, so it never runs out
        /// across the level, and the tiles at 0.75; both are drawn from the screen center's offset.
        /// </summary>
        [Fact]
        public void TheLayersMoveAtTheirParallax()
        {
            Backdrop backdrop = new(BackdropStyle.Normal, 10, 5, (1024, 768), new Random(1));

            BackdropLayout centered = backdrop.Layout(400, 300);
            BackdropLayout scrolled = backdrop.Layout(400 - 800, 300 - 375);

            Assert.Equal((0, 0), (centered.ImageX, centered.ImageY));
            Assert.Equal((-224, -168), (scrolled.ImageX, scrolled.ImageY));
            BackdropSprite firstTile = scrolled.Sprites.First(sprite => !sprite.Additive);
            Assert.Equal((-600, -281), (firstTile.X, firstTile.Y));
        }

        /// <summary>Verifies the tile grid covers the level plus one screen, with the style's tiles.</summary>
        [Fact]
        public void TheTilesCoverTheLevelAndOneScreen()
        {
            Backdrop backdrop = new(BackdropStyle.Desert, 10, 5, (1024, 768), new Random(1));

            BackdropSprite[] tiles = [.. backdrop.Layout(400, 300).Sprites.Where(sprite => sprite.Page == "SpriteBackDesert")];

            Assert.Equal(7 * 5, tiles.Length);
            Assert.All(tiles, tile => Assert.StartsWith("BackDesert_2_0", tile.Frame, StringComparison.Ordinal));
        }

        /// <summary>Verifies lamps sit on the tiles and each draws its additive glow.</summary>
        [Fact]
        public void LampsDrawWithTheirGlow()
        {
            Backdrop backdrop = new(BackdropStyle.Normal, 40, 30, (1024, 768), new Random(3));

            BackdropSprite[] lamps = [.. backdrop.Layout(400, 300).Sprites.Where(sprite => sprite.Page == "SpriteBackLamp")];

            Assert.NotEmpty(lamps);
            Assert.Equal(lamps.Count(lamp => lamp.Additive), lamps.Count(lamp => !lamp.Additive));
            Assert.All(lamps.Where(lamp => !lamp.Additive), lamp => Assert.StartsWith("OLamp", lamp.Frame, StringComparison.Ordinal));
        }
    }
}
