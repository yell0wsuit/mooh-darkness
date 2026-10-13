using System;
using System.IO;

using Mooh.Presentation.Sprites;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies the recovered <c>anm2</c> sprite page frame tables.</summary>
    public sealed class SpritePageTests
    {
        /// <summary>Verifies the player page yields every frame with its rectangle and pivot.</summary>
        [Fact]
        public void ThePlayerPageYieldsItsFrames()
        {
            SpritePage page = Load("SpritePlayer");

            Assert.Equal("SpritePlayer", page.Name);
            Assert.Equal(681, page.Count);
            SpriteFrame cola = page["OCola00"];
            Assert.Equal(160, cola.X);
            Assert.Equal(3978, cola.Y);
            Assert.Equal(34, cola.Width);
            Assert.Equal(33, cola.Height);
            Assert.True(page.Contains("OWlkLeft10"));
        }

        /// <summary>Verifies the frame pivot is the two signed offsets the recovered record carries.</summary>
        [Fact]
        public void TheFramePivotIsSigned()
        {
            SpritePage page = Load("SpriteObjBoublle");

            SpriteFrame frame = page["OSt00"];

            Assert.True(frame.PivotX < 0 || frame.PivotY < 0);
        }

        /// <summary>Verifies data without the recovered signature is rejected.</summary>
        [Fact]
        public void DataWithoutTheSignatureIsRejected()
        {
            using MemoryStream stream = new(new byte[32]);

            _ = Assert.Throws<InvalidDataException>(() => SpritePage.Load("broken", stream));
        }

        private static SpritePage Load(string name)
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "sprites", name + ".seq"));
            return SpritePage.Load(name, stream);
        }
    }
}
