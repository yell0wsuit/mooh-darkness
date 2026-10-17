using System;
using System.IO;

using Mooh.Presentation.Text;

using Xunit;

namespace Mooh.Presentation.Tests
{
    public sealed class ComicNeueTests
    {
        [Fact]
        public void TrueTypeFontMeasuresGlyphsAndKeepsBaselineBounds()
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "fonts", "ComicNeue-Bold.ttf"));
            GameFont font = GameFont.Load("PanelDigScr", stream);
            Assert.True(font.Width("Level 3") > 0);
            Assert.True(font.Has('é'));
            Assert.True(font.Advance(' ').X > 0);
            Assert.True(font.Bounds("Level 3").Top < 0);
        }
    }
}
