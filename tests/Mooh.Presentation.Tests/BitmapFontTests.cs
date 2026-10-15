using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Presentation.Text;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the recovered <c>.fnt</c> bitmap fonts and the native text layout,
    /// <c>sub_406FA0</c> and <c>sub_407D40</c>.
    /// </summary>
    public sealed class BitmapFontTests
    {
        /// <summary>Verifies the panel font covers the codes from <c>!</c> through 255.</summary>
        [Fact]
        public void ThePanelFontCoversItsCodeRange()
        {
            BitmapFont font = Load("PanelDigFnt");

            Assert.Equal("PanelDigFnt", font.Name);
            Assert.Equal(33, font.FirstCode);
            Assert.Equal(255, font.LastCode);
        }

        /// <summary>
        /// Verifies a glyph sits at the pen plus its fixed-point offset, rounded down, with its
        /// bottom edge there and its atlas rectangle from the record.
        /// </summary>
        [Fact]
        public void AGlyphSitsAtThePenPlusItsOffset()
        {
            IReadOnlyList<GlyphPlacement> glyphs = Load("PanelDigFnt").Layout("H", 10, 50, TextAlignment.Left);

            GlyphPlacement glyph = Assert.Single(glyphs);
            Assert.Equal(new GlyphPlacement(119, 171, 16, 22, 7, 31), glyph);
        }

        /// <summary>
        /// Verifies the pen moves by each glyph's advance, and by the font's default advance for a
        /// code outside its range such as the space.
        /// </summary>
        [Fact]
        public void ThePenMovesByEachAdvance()
        {
            BitmapFont font = Load("PanelDigFnt");

            IReadOnlyList<GlyphPlacement> glyphs = font.Layout("1 0", 10, 50, TextAlignment.Left);

            Assert.Equal(2, glyphs.Count);
            Assert.Equal(8, glyphs[0].X);
            Assert.Equal(26, glyphs[1].X);
            Assert.Equal(38, font.Width("1 0"));
        }

        /// <summary>Verifies centered text starts half its width, rounded down, left of the point.</summary>
        [Fact]
        public void CenteredTextStartsHalfItsWidthLeft()
        {
            IReadOnlyList<GlyphPlacement> glyphs = Load("PanelDigFnt").Layout("1 0", 400, 50, TextAlignment.Center);

            Assert.Equal(400 - 19 - 2, glyphs[0].X);
        }

        /// <summary>Verifies right-aligned text ends at the point.</summary>
        [Fact]
        public void RightAlignedTextEndsAtThePoint()
        {
            IReadOnlyList<GlyphPlacement> glyphs = Load("PanelDigFnt").Layout("1 0", 400, 50, TextAlignment.Right);

            Assert.Equal(400 - 38 - 2, glyphs[0].X);
        }

        /// <summary>Verifies a curly apostrophe draws its Windows-1252 glyph, code 0x92.</summary>
        [Fact]
        public void ACurlyApostropheDrawsItsCodePageGlyph()
        {
            GlyphPlacement glyph = Assert.Single(Load("PanelDigFnt").Layout("’", 0, 0, TextAlignment.Left));

            Assert.Equal(219, glyph.SourceX);
            Assert.Equal(271, glyph.SourceY);
        }

        /// <summary>Verifies data without the recovered signature is rejected.</summary>
        [Fact]
        public void DataWithoutTheSignatureIsRejected()
        {
            using MemoryStream stream = new(new byte[16]);

            _ = Assert.Throws<InvalidDataException>(() => BitmapFont.Load("Broken", stream));
        }

        private static BitmapFont Load(string name)
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "fonts", name + ".fnt"));
            return BitmapFont.Load(name, stream);
        }
    }
}
