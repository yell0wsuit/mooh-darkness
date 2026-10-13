using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            Assert.True(page.Contains("OWlkLeft10"));
        }

        /// <summary>
        /// Verifies a frame's four signed values are its edges around the anchor: a walking player
        /// spans 32 px left of it to 19 px right, and 48 px above it to 33 px below.
        /// </summary>
        [Fact]
        public void AFramesEdgesSurroundItsAnchor()
        {
            SpriteFrame walk = Load("SpritePlayer")["OWlkLeft17"];

            Assert.Equal(972, walk.X);
            Assert.Equal(3524, walk.Y);
            Assert.Equal(-32, walk.PivotX);
            Assert.Equal(-48, walk.PivotY);
            Assert.Equal(51, walk.Width);
            Assert.Equal(81, walk.Height);
        }

        /// <summary>Verifies every frame lies inside its atlas image.</summary>
        [Fact]
        public void EveryFrameLiesInsideItsAtlas()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "assets", "sprites");
            List<string> outside = [];
            foreach (string table in Directory.EnumerateFiles(directory, "*.seq"))
            {
                string atlas = Path.ChangeExtension(table, ".png");
                if (!File.Exists(atlas))
                {
                    continue;
                }

                (int width, int height) = PngSize(atlas);
                SpritePage page = Load(Path.GetFileNameWithoutExtension(table));
                outside.AddRange(page.Frames
                    .Where(frame => frame.Width < 0 || frame.Height < 0 || frame.X + frame.Width > width || frame.Y + frame.Height > height)
                    .Select(frame => $"{page.Name}/{frame.Name}"));
            }

            Assert.Empty(outside);
        }

        private static (int Width, int Height) PngSize(string path)
        {
            Span<byte> header = stackalloc byte[24];
            using FileStream stream = File.OpenRead(path);
            stream.ReadExactly(header);
            return (BinaryPrimitives.ReadInt32BigEndian(header[16..]), BinaryPrimitives.ReadInt32BigEndian(header[20..]));
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
