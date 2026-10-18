using System.IO;
using System.Text;

using Mooh.Presentation.Sprites;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks TexturePacker Array and Hash frame geometry.</summary>
    public sealed class TexturePackerTests
    {
        /// <summary>Checks that trimmed frame offsets and normalized pivots preserve the logical anchor.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TrimAndPivotPreserveAnchor(bool array)
        {
            const string frame = "\"frame\":{\"x\":12,\"y\":34,\"w\":20,\"h\":30},\"rotated\":false,\"spriteSourceSize\":{\"x\":5,\"y\":7,\"w\":20,\"h\":30},\"sourceSize\":{\"w\":40,\"h\":60},\"pivot\":{\"x\":0.5,\"y\":0.5}";
            string frames = array ? "[{\"filename\":\"walk.png\"," + frame + "}]" : "{\"walk.png\":{" + frame + "}}";
            using MemoryStream stream = new(Encoding.UTF8.GetBytes("{\"frames\":" + frames + "}"));
            SpriteFrame sprite = SpritePage.Load("test", stream)["walk"];
            Assert.Equal(new SpriteFrame("walk", 12, 34, 20, 30, -15, -23), sprite);
        }
    }
}
