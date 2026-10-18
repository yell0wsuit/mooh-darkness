using System;
using System.IO;

using Mooh.Presentation.Text;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks the bundled TrueType font metrics and font weights.</summary>
    public sealed class ComicNeueTests
    {
        /// <summary>Checks visible glyph coverage, space advances and bounds relative to the baseline.</summary>
        [Fact]
        public void TrueTypeFontMeasuresGlyphsAndKeepsBaselineBounds()
        {
            using FileStream stream = Open("PanelDigScr");
            using GameFont font = GameFont.Load("PanelDigScr", stream);
            Assert.True(font.Width("Level 3") > 0);
            Assert.True(font.Has('é'));
            Assert.True(font.Advance(' ').X > 0);
            Assert.True(font.Bounds("Level 3").Top < 0);
        }

        /// <summary>Menus, map controls and HUD values select the bold face.</summary>
        [Theory]
        [InlineData("MenuFntMapTxt")]
        [InlineData("MapButtonFont")]
        [InlineData("MenuFntBtnNrm")]
        [InlineData("MenuFntBtnSel")]
        [InlineData("MenuFntBtnPrs")]
        [InlineData("MenuFntTit")]
        [InlineData("PanelDigFnt")]
        [InlineData("PanelDigTextFnt")]
        [InlineData("PanelDigScr")]
        public void MenusAndHudUseBoldVariant(string style)
        {
            using FileStream stream = Open(style);
            using GameFont font = GameFont.Load(style, stream);
            Assert.Equal(700, font.Font.Typeface.FontWeight);
        }

        /// <summary>Stories, explanatory text, tooltips and fine print retain the regular face.</summary>
        [Theory]
        [InlineData("MapStoryFont")]
        [InlineData("MenuFntEncTxt")]
        [InlineData("MenuFontRegTxt")]
        [InlineData("PanelInstrBlackFnt")]
        [InlineData("MenuFontToolTip")]
        [InlineData("MenuTTipFont")]
        [InlineData("VersionFont")]
        public void StoriesAndTooltipsUseRegularVariant(string style)
        {
            using FileStream stream = Open(style);
            using GameFont font = GameFont.Load(style, stream);
            Assert.Equal(400, font.Font.Typeface.FontWeight);
        }

        /// <summary>Empty text has no visible glyph box.</summary>
        [Fact]
        public void EmptyTextHasNoVisibleBounds()
        {
            using FileStream stream = Open("MenuFntBtnNrm");
            using GameFont font = GameFont.Load("MenuFntBtnNrm", stream);
            Assert.Equal((0, 0, 0, 0), font.Bounds(string.Empty));
        }

        private static FileStream Open(string style)
        {
            return File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "fonts", GameFont.FileName(style)));
        }
    }
}
