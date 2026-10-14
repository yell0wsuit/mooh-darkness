using System;

using Mooh.Core.Simulation;
using Mooh.Desktop.Platform.Graphics;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Verifies the desktop command line.</summary>
    public sealed class DesktopOptionsTests
    {
        /// <summary>Verifies no arguments start the first level on the standard difficulty.</summary>
        [Fact]
        public void NoArgumentsStartTheFirstLevel()
        {
            DesktopOptions options = DesktopOptions.Parse([]);

            Assert.Equal(0, options.LevelIndex);
            Assert.Same(DifficultySpeeds.Standard, options.Difficulty);
            Assert.Null(options.Renderer);
            Assert.Null(options.QuitAfterFrames);
            Assert.Null(options.ScreenshotPath);
        }

        /// <summary>Verifies every option is read.</summary>
        [Fact]
        public void EveryOptionIsRead()
        {
            DesktopOptions options = DesktopOptions.Parse(
                ["--level", "3", "--difficulty", "2", "--renderer", "software", "--frames", "120", "--screenshot", "shot.png"]);

            Assert.Equal(2, options.LevelIndex);
            Assert.Same(DifficultySpeeds.Timed, options.Difficulty);
            Assert.Equal(GraphicsBackendKind.Software, options.Renderer);
            Assert.Equal(120, options.QuitAfterFrames);
            Assert.Equal("shot.png", options.ScreenshotPath);
        }

        /// <summary>Verifies an unknown or incomplete option is refused with a message.</summary>
        [Theory]
        [InlineData("--level")]
        [InlineData("--level", "0")]
        [InlineData("--renderer", "vulkan")]
        [InlineData("--bogus")]
        public void ABadOptionIsRefused(params string[] arguments)
        {
            _ = Assert.Throws<ArgumentException>(() => DesktopOptions.Parse(arguments));
        }
    }
}
