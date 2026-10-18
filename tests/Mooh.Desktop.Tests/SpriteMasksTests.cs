using System;
using System.IO;
using System.Linq;

using Mooh.Presentation.Sprites;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Verifies the atlas-backed solid pixels the pickup sparks sample.</summary>
    public sealed class SpriteMasksTests
    {
        /// <summary>Verifies a coin's frame has solid pixels inside it and none outside the atlas.</summary>
        [Fact]
        public void ACoinFrameHasSolidPixels()
        {
            using SpriteMasks masks = new(Path.Combine(AppContext.BaseDirectory, "assets", "sprites"));

            SpriteFrame frame = Assert.IsType<SpriteFrame>(masks.FrameOf("SpriteObjCoin", "O1St00"));

            Assert.Contains(
                Enumerable.Range(0, frame.Height).SelectMany(row => Enumerable.Range(0, frame.Width).Select(column => (column, row))),
                point => masks.IsSolid("SpriteObjCoin", frame.X + point.column, frame.Y + point.row));
            Assert.False(masks.IsSolid("SpriteObjCoin", -1, -1));
            Assert.Null(masks.FrameOf("SpriteNoSuchPage", "O1St00"));
        }
    }
}
