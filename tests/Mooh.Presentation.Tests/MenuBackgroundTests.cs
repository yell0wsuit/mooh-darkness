using System;
using System.IO;

using Mooh.Presentation.Screens;
using Mooh.Presentation.Sprites;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies the initialized menu background and the original cursor's state timing.</summary>
    public sealed class MenuBackgroundTests
    {
        /// <summary>The shipped menu initializes no puff, leaf, grass, beetle or logo overlays.</summary>
        [Fact]
        public void TheShippedBackgroundHasNoAmbientObjects()
        {
            MenuBackground background = new(new Random(1));
            background.Advance(60);
            Assert.Empty(background.Elements);
        }

        /// <summary>The idle cursor plays twenty frames at fifty milliseconds and drops loop overshoot.</summary>
        [Fact]
        public void IdleCursorUsesTheRecoveredFrameRate()
        {
            MenuBackground background = new(new Random(1));
            Assert.Equal(new ScreenSprite("SpriteMenuPrt", "PCursor00", 123, 234), background.Cursor(123, 234, false));
            background.Advance(0.050);
            Assert.Equal("PCursor01", background.Cursor(0, 0, false).Frame);
            background.Advance(0.950);
            Assert.Equal("PCursor00", background.Cursor(0, 0, false).Frame);
            background.Advance(1.125);
            Assert.Equal("PCursor00", background.Cursor(0, 0, false).Frame);
        }

        /// <summary>The click animation finishes after release and returns to idle even while held.</summary>
        [Fact]
        public void PressedCursorFinishesIndependentlyOfTheHeldButton()
        {
            MenuBackground background = new(new Random(1));
            Assert.Equal("PCursorPrs00", background.Cursor(0, 0, true).Frame);
            background.Advance(0.035);
            Assert.Equal("PCursorPrs01", background.Cursor(0, 0, false).Frame);
            Assert.Equal("PCursorPrs01", background.Cursor(0, 0, true).Frame);
            background.Advance(0.315);
            Assert.Equal("PCursor00", background.Cursor(0, 0, true).Frame);
            background.Advance(0.050);
            Assert.Equal("PCursor01", background.Cursor(0, 0, true).Frame);
            _ = background.Cursor(0, 0, false);
            Assert.Equal("PCursorPrs00", background.Cursor(0, 0, true).Frame);
        }

        /// <summary>Every idle and pressed cursor frame names an actual recovered atlas frame.</summary>
        [Fact]
        public void EveryCursorFrameExistsInTheRecoveredPage()
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "sprites", "SpriteMenuPrt.seq"));
            SpritePage page = SpritePage.Load("SpriteMenuPrt", stream);
            MenuBackground background = new(new Random(1));
            for (int frame = 0; frame < 20; frame++)
            {
                Assert.True(page.Contains(background.Cursor(0, 0, false).Frame));
                background.Advance(0.050);
            }

            _ = background.Cursor(0, 0, true);
            for (int frame = 0; frame < 10; frame++)
            {
                Assert.True(page.Contains(background.Cursor(0, 0, true).Frame));
                background.Advance(0.035);
            }
        }
    }
}
